﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeCleanup;
using Microsoft.CodeAnalysis.CSharp.CodeStyle.TypeStyle;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Simplification;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.IntroduceVariable;

using static CSharpSyntaxTokens;
using static SyntaxFactory;

internal sealed partial class CSharpIntroduceVariableService
{
    protected override Document IntroduceLocal(
        SemanticDocument document,
        CodeCleanupOptions options,
        ExpressionSyntax expression,
        bool allOccurrences,
        bool isConstant,
        CancellationToken cancellationToken)
    {
        var globalStatement = expression.GetAncestor<GlobalStatementSyntax>();

        var containerToGenerateInto = globalStatement != null
            ? (CompilationUnitSyntax)globalStatement.GetRequiredParent()
            : expression.Ancestors().FirstOrDefault(s => s is BlockSyntax or ArrowExpressionClauseSyntax or LambdaExpressionSyntax);

        var newLocalNameToken = GenerateUniqueLocalName(
            document, expression, isConstant, containerToGenerateInto, cancellationToken);
        var newLocalName = IdentifierName(newLocalNameToken);

        var modifiers = isConstant
            ? TokenList(ConstKeyword)
            : default;

        var updatedExpression = expression.WithoutTrivia();
        var simplifierOptions = (CSharpSimplifierOptions)options.SimplifierOptions;

        // If the implicit-object-creation is preferred and "var" is not preferred under any circumstance, then we use
        // the implicit creation form when it it available.
        if (simplifierOptions.ImplicitObjectCreationWhenTypeIsApparent.Value &&
            simplifierOptions.GetUseVarPreference() == UseVarPreference.None &&
            updatedExpression is ObjectCreationExpressionSyntax objectCreationExpression &&
            document.Root.SyntaxTree.Options.LanguageVersion() >= LanguageVersion.CSharp9)
        {
            var (newKeyword, argumentList) = objectCreationExpression.ArgumentList is null
                ? (objectCreationExpression.NewKeyword.WithoutTrailingTrivia(), ArgumentList().WithoutLeadingTrivia().WithTrailingTrivia(objectCreationExpression.NewKeyword.TrailingTrivia))
                : (objectCreationExpression.NewKeyword, objectCreationExpression.ArgumentList);
            updatedExpression = ImplicitObjectCreationExpression(
                newKeyword, argumentList, objectCreationExpression.Initializer);
        }

        var declarationStatement = LocalDeclarationStatement(
            modifiers,
            VariableDeclaration(
                GetTypeSyntax(document, expression, cancellationToken),
                [VariableDeclarator(
                    newLocalNameToken.WithAdditionalAnnotations(RenameAnnotation.Create()),
                    argumentList: null,
                    EqualsValueClause(updatedExpression))]));

        switch (containerToGenerateInto)
        {
            case CompilationUnitSyntax compilationUnit:
                return IntroduceLocalDeclarationIntoCompilationUnit(
                    document, compilationUnit, expression, newLocalName, declarationStatement, allOccurrences, cancellationToken);

            case BlockSyntax block:
                return IntroduceLocalDeclarationIntoBlock(
                    document, block, expression, newLocalName, declarationStatement, allOccurrences, cancellationToken);

            case ArrowExpressionClauseSyntax arrowExpression:
                // this will be null for expression-bodied properties & indexer (not for individual getters & setters, those do have a symbol),
                // both of which are a shorthand for the getter and always return a value
                var method = document.SemanticModel.GetDeclaredSymbol(arrowExpression.GetRequiredParent(), cancellationToken) as IMethodSymbol;
                var createReturnStatement = true;

                if (method is not null)
                    createReturnStatement = !method.ReturnsVoid && !method.IsAsyncReturningVoidTask(document.SemanticModel.Compilation);

                return RewriteExpressionBodiedMemberAndIntroduceLocalDeclaration(
                    document, arrowExpression, expression, newLocalName,
                    declarationStatement, allOccurrences, createReturnStatement, cancellationToken);

            case LambdaExpressionSyntax lambda:
                return IntroduceLocalDeclarationIntoLambda(
                    document, lambda, expression, newLocalName, declarationStatement,
                    allOccurrences, cancellationToken);
        }

        throw new InvalidOperationException();
    }

    private Document IntroduceLocalDeclarationIntoLambda(
        SemanticDocument document,
        LambdaExpressionSyntax oldLambda,
        ExpressionSyntax expression,
        IdentifierNameSyntax newLocalName,
        LocalDeclarationStatementSyntax declarationStatement,
        bool allOccurrences,
        CancellationToken cancellationToken)
    {
        var oldBody = (ExpressionSyntax)oldLambda.Body;
        var isEntireLambdaBodySelected = oldBody.Equals(expression.WalkUpParentheses());

        var rewrittenBody = Rewrite(
            document, expression, newLocalName, document, oldBody, allOccurrences, cancellationToken);

        var shouldIncludeReturnStatement = ShouldIncludeReturnStatement(document, oldLambda, cancellationToken);
        var newBody = GetNewBlockBodyForLambda(
            declarationStatement, isEntireLambdaBodySelected, rewrittenBody, shouldIncludeReturnStatement);

        // Add an elastic newline so that the formatter will place this new lambda body across multiple lines.
        newBody = newBody
            .WithOpenBraceToken(newBody.OpenBraceToken.WithAppendedTrailingTrivia(ElasticCarriageReturnLineFeed))
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.Document.WithSyntaxRoot(
            document.Root.ReplaceNode(oldLambda, oldLambda.WithBody(newBody)));
    }

    private static bool ShouldIncludeReturnStatement(
        SemanticDocument document,
        LambdaExpressionSyntax oldLambda,
        CancellationToken cancellationToken)
    {
        if (document.SemanticModel.GetTypeInfo(oldLambda, cancellationToken).ConvertedType is INamedTypeSymbol delegateType &&
            delegateType.DelegateInvokeMethod != null)
        {
            if (delegateType.DelegateInvokeMethod.ReturnsVoid)
            {
                return false;
            }

            // Async lambdas with a Task or ValueTask return type don't need a return statement.
            // e.g.:
            //     Func<int, Task> f = async x => await M2();
            //
            // After refactoring:
            //     Func<int, Task> f = async x =>
            //     {
            //         Task task = M2();
            //         await task;
            //     };
            var compilation = document.SemanticModel.Compilation;
            var delegateReturnType = delegateType.DelegateInvokeMethod.ReturnType;
            if (oldLambda.AsyncKeyword != default && delegateReturnType != null)
            {
                if ((compilation.TaskType() != null && delegateReturnType.Equals(compilation.TaskType())) ||
                    (compilation.ValueTaskType() != null && delegateReturnType.Equals(compilation.ValueTaskType())))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static BlockSyntax GetNewBlockBodyForLambda(
        LocalDeclarationStatementSyntax declarationStatement,
        bool isEntireLambdaBodySelected,
        ExpressionSyntax rewrittenBody,
        bool includeReturnStatement)
    {
        if (includeReturnStatement)
        {
            // Case 1: The lambda has a non-void return type.
            // e.g.:
            //     Func<int, int> f = x => [|x + 1|];
            //
            // After refactoring:
            //     Func<int, int> f = x =>
            //     {
            //         var v = x + 1;
            //         return v;
            //     };
            return Block(declarationStatement, ReturnStatement(rewrittenBody));
        }

        // For lambdas with void return types, we don't need to include the rewritten body if the entire lambda body
        // was originally selected for refactoring, as the rewritten body should already be encompassed within the
        // declaration statement.
        if (isEntireLambdaBodySelected)
        {
            // Case 2a: The lambda has a void return type, and the user selects the entire lambda body.
            // e.g.:
            //     Action<int> goo = x => [|x.ToString()|];
            //
            // After refactoring:
            //     Action<int> goo = x =>
            //     {
            //         string v = x.ToString();
            //     };
            return Block(declarationStatement);
        }

        // Case 2b: The lambda has a void return type, and the user didn't select the entire lambda body.
        // e.g.:
        //     Task.Run(() => File.Copy("src", [|Path.Combine("dir", "file")|]));
        //
        // After refactoring:
        //     Task.Run(() =>
        //     {
        //         string destFileName = Path.Combine("dir", "file");
        //         File.Copy("src", destFileName);
        //     });
        return Block(
            declarationStatement,
            ExpressionStatement(rewrittenBody, SemicolonToken));
    }

    private static TypeSyntax GetTypeSyntax(SemanticDocument document, ExpressionSyntax expression, CancellationToken cancellationToken)
        => GetTypeSymbol(document, expression, cancellationToken).GenerateTypeSyntax();

    private Document RewriteExpressionBodiedMemberAndIntroduceLocalDeclaration(
        SemanticDocument document,
        ArrowExpressionClauseSyntax arrowExpression,
        ExpressionSyntax expression,
        NameSyntax newLocalName,
        LocalDeclarationStatementSyntax declarationStatement,
        bool allOccurrences,
        bool createReturnStatement,
        CancellationToken cancellationToken)
    {
        var oldBody = arrowExpression;
        var oldParentingNode = oldBody.GetRequiredParent();
        var leadingTrivia = oldBody.GetLeadingTrivia()
                                   .AddRange(oldBody.ArrowToken.TrailingTrivia);

        var newExpression = Rewrite(document, expression, newLocalName, document, oldBody.Expression, allOccurrences, cancellationToken);

        var newBody = Block(
            declarationStatement,
            createReturnStatement
                ? ReturnStatement(newExpression)
                : ExpressionStatement(newExpression))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(oldBody.GetTrailingTrivia());

        // Add an elastic newline so that the formatter will place this new block across multiple lines.
        newBody = newBody
            .WithOpenBraceToken(newBody.OpenBraceToken.WithAppendedTrailingTrivia(ElasticCarriageReturnLineFeed))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = document.Root.ReplaceNode(oldParentingNode, WithBlockBody(oldParentingNode, newBody).WithTriviaFrom(oldParentingNode));
        return document.Document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode WithBlockBody(SyntaxNode node, BlockSyntax body)
        => node switch
        {
            BasePropertyDeclarationSyntax baseProperty => baseProperty
                .TryWithExpressionBody(null)
                .WithAccessorList(AccessorList([AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, body)]))
                .TryWithSemicolonToken(Token(SyntaxKind.None)),
            AccessorDeclarationSyntax accessor => accessor
                .WithExpressionBody(null)
                .WithBody(body)
                .WithSemicolonToken(Token(SyntaxKind.None)),
            BaseMethodDeclarationSyntax baseMethod => baseMethod
                .WithExpressionBody(null)
                .WithBody(body)
                .WithSemicolonToken(Token(SyntaxKind.None)),
            LocalFunctionStatementSyntax localFunction => localFunction
                .WithExpressionBody(null)
                .WithBody(body)
                .WithSemicolonToken(Token(SyntaxKind.None)),
            _ => throw ExceptionUtilities.UnexpectedValue(node),
        };

    private Document IntroduceLocalDeclarationIntoCompilationUnit(
        SemanticDocument document,
        CompilationUnitSyntax compilationUnit,
        ExpressionSyntax expression,
        NameSyntax newLocalName,
        LocalDeclarationStatementSyntax declarationStatement,
        bool allOccurrences,
        CancellationToken cancellationToken)
    {
        declarationStatement = declarationStatement.WithAdditionalAnnotations(Formatter.Annotation);

        SyntaxNode scope = compilationUnit;

        // If we're within a non-static local function, our scope for the new local declaration is expanded to include
        // the enclosing member.
        var localFunction = expression.GetAncestor<LocalFunctionStatementSyntax>();
        if (localFunction is { Body: not null } && localFunction.Modifiers.Any(SyntaxKind.StaticKeyword))
            scope = localFunction.Body;

        var matches = FindMatches(
            document, expression, document,
            scope is ICompilationUnitSyntax
                ? scope.ChildNodes().OfType<GlobalStatementSyntax>()
                : [scope],
            allOccurrences, cancellationToken);
        Debug.Assert(matches.Contains(expression));

        var firstAffectedExpression = matches.OrderBy(m => m.SpanStart).First();

        var editor = new SyntaxEditor(compilationUnit, document.Project.Solution.Services);

        // Parenthesize the variable, and go and replace anything we find with it. NOTE: we do not want elastic trivia
        // as we want to just replace the existing code as is, while preserving the trivia there.  We do not want to
        // update it.
        var replacement = editor.Generator.AddParentheses(newLocalName, includeElasticTrivia: false);
        foreach (var match in matches)
            editor.ReplaceNode(match, replacement);

        if (scope is BlockSyntax block)
        {
            var firstAffectedStatement = block.Statements.Single(s => firstAffectedExpression.GetAncestorOrThis<StatementSyntax>()!.Contains(s));
            var firstAffectedStatementIndex = block.Statements.IndexOf(firstAffectedStatement);
            editor.ReplaceNode(
                block,
                (current, generator) =>
                {
                    var currentBlock = (BlockSyntax)current;
                    return currentBlock.WithStatements(
                        currentBlock.Statements.Insert(firstAffectedStatementIndex, declarationStatement));
                });
        }
        else
        {
            var firstAffectedGlobalStatement = compilationUnit.Members.OfType<GlobalStatementSyntax>().Single(s => firstAffectedExpression.GetAncestorOrThis<GlobalStatementSyntax>()!.Contains(s));
            var firstAffectedGlobalStatementIndex = compilationUnit.Members.IndexOf(firstAffectedGlobalStatement);
            editor.ReplaceNode(
                compilationUnit,
                (current, generator) =>
                {
                    var currentCompilationUnit = (CompilationUnitSyntax)current;
                    return currentCompilationUnit.WithMembers(
                        currentCompilationUnit.Members.Insert(firstAffectedGlobalStatementIndex, GlobalStatement(declarationStatement)));
                });
        }

        return document.Document.WithSyntaxRoot(editor.GetChangedRoot());
    }

#nullable disable

    private Document IntroduceLocalDeclarationIntoBlock(
        SemanticDocument document,
        BlockSyntax block,
        ExpressionSyntax expression,
        NameSyntax newLocalName,
        LocalDeclarationStatementSyntax declarationStatement,
        bool allOccurrences,
        CancellationToken cancellationToken)
    {
        declarationStatement = declarationStatement.WithAdditionalAnnotations(Formatter.Annotation);

        SyntaxNode scope = block;

        // If we're within a non-static local function, our scope for the new local declaration is expanded to include the enclosing member.
        var localFunction = block.GetAncestor<LocalFunctionStatementSyntax>();
        if (localFunction != null && !localFunction.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            scope = block.GetAncestor<MemberDeclarationSyntax>();
        }

        var matches = FindMatches(document, expression, document, [scope], allOccurrences, cancellationToken);
        Debug.Assert(matches.Contains(expression));

        var root = document.Root;
        ISet<StatementSyntax> allAffectedStatements = new HashSet<StatementSyntax>(matches.SelectMany(expr => GetApplicableStatementAncestors(expr)));

        SyntaxNode innermostCommonBlock;

        var innermostStatements = new HashSet<StatementSyntax>(matches.Select(expr => GetApplicableStatementAncestors(expr).First()));
        if (innermostStatements.Count == 1)
        {
            // if there was only one match, or all the matches came from the same statement
            var statement = innermostStatements.Single();

            // and the statement is an embedded statement without a block, we want to generate one
            // around this statement rather than continue going up to find an actual block
            if (!IsBlockLike(statement.Parent))
            {
                root = root.TrackNodes(allAffectedStatements.Concat(new SyntaxNode[] { expression, statement }));
                root = root.ReplaceNode(root.GetCurrentNode(statement),
                    Block(root.GetCurrentNode(statement)).WithAdditionalAnnotations(Formatter.Annotation));

                expression = root.GetCurrentNode(expression);
                statement = root.GetCurrentNode(statement);

                allAffectedStatements = allAffectedStatements.Select(root.GetCurrentNode).ToSet();
            }

            innermostCommonBlock = statement.Parent;
        }
        else
        {
            innermostCommonBlock = innermostStatements.FindInnermostCommonNode(IsBlockLike);
        }

        var firstStatementAffectedIndex = GetFirstStatementAffectedIndex(innermostCommonBlock, matches, GetStatements(innermostCommonBlock).IndexOf(allAffectedStatements.Contains));

        var newInnerMostBlock = Rewrite(
            document, expression, newLocalName, document, innermostCommonBlock, allOccurrences, cancellationToken);

        var statements = InsertWithinTriviaOfNext(GetStatements(newInnerMostBlock), declarationStatement, firstStatementAffectedIndex);
        var finalInnerMostBlock = WithStatements(newInnerMostBlock, statements);

        var newRoot = root.ReplaceNode(innermostCommonBlock, finalInnerMostBlock);
        return document.Document.WithSyntaxRoot(newRoot);
    }

#nullable restore

    private static IEnumerable<StatementSyntax> GetApplicableStatementAncestors(ExpressionSyntax expr)
    {
        foreach (var statement in expr.GetAncestorsOrThis<StatementSyntax>())
        {
            // When determining where to put a local, we don't want to put it between the `else`
            // and `if` of a compound if-statement.
            if (statement is IfStatementSyntax { Parent: ElseClauseSyntax })
                continue;

            yield return statement;
        }
    }

    private static int GetFirstStatementAffectedIndex(SyntaxNode innermostCommonBlock, ISet<ExpressionSyntax> matches, int firstStatementAffectedIndex)
    {
        // If a local function is involved, we have to make sure the new declaration is placed:
        //     1. Before all calls to local functions that use the variable.
        //     2. Before the local function(s) themselves.
        //     3. Before all matches, i.e. places in the code where the new declaration will replace existing code.
        // Cases (2) and (3) are already covered by the 'firstStatementAffectedIndex' parameter. Thus, all we have to do is ensure we consider (1) when
        // determining where to place our new declaration.

        // Find all the local functions within the scope that will use the new declaration.
        var localFunctions = innermostCommonBlock.DescendantNodes().Where(node => node.IsKind(SyntaxKind.LocalFunctionStatement) && matches.Any(match => match.Span.OverlapsWith(node.Span)));

        if (localFunctions.IsEmpty())
        {
            return firstStatementAffectedIndex;
        }

        var localFunctionIdentifiers = localFunctions.Select(node => ((LocalFunctionStatementSyntax)node).Identifier.ValueText);

        // Find all calls to the applicable local functions within the scope.
        var localFunctionCalls = innermostCommonBlock.DescendantNodes().Where(
            node => node is InvocationExpressionSyntax invocationExpression &&
            invocationExpression.Expression.GetRightmostName() is { } rightmostName &&
            !invocationExpression.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression) &&
            localFunctionIdentifiers.Contains(rightmostName.Identifier.ValueText));

        if (localFunctionCalls.IsEmpty())
        {
            return firstStatementAffectedIndex;
        }

        // Find which call is the earliest.
        var earliestLocalFunctionCall = localFunctionCalls.Min(node => node.SpanStart);

        var statementsInBlock = GetStatements(innermostCommonBlock);

        // Check if our earliest call is before all local function declarations and all matches, and if so, place our new declaration there.
        var earliestLocalFunctionCallIndex = statementsInBlock.IndexOf(s => s.Span.Contains(earliestLocalFunctionCall));
        return Math.Min(earliestLocalFunctionCallIndex, firstStatementAffectedIndex);
    }

    private static SyntaxList<StatementSyntax> InsertWithinTriviaOfNext(
        SyntaxList<StatementSyntax> oldStatements,
        StatementSyntax newStatement,
        int statementIndex)
    {
        var nextStatement = oldStatements.ElementAtOrDefault(statementIndex);
        if (nextStatement == null)
            return oldStatements.Insert(statementIndex, newStatement);

        var priorToken = nextStatement.GetFirstToken().GetPreviousToken();
        if (!priorToken.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia) &&
            !nextStatement.GetLastToken().TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia))
        {
            // A single statement that is on the same line as the construct that owns it.  In this case, just place the
            // new statement in front of it.
            return oldStatements.ReplaceRange(
                nextStatement,
                [newStatement.WithoutLeadingTrivia().WithTrailingTrivia(Space), nextStatement]);
        }

        // Grab all the trivia before the line the next statement is on and move it to the new node.

        // If the next statement is on its own line, then move it's leading trivia (up through the new line) to the new
        // statement (keeping the trivia after that with the next statement).
        var nextStatementLeading = nextStatement.GetLeadingTrivia();
        var precedingEndOfLine = nextStatementLeading.LastOrDefault(t => t.Kind() == SyntaxKind.EndOfLineTrivia);
        if (precedingEndOfLine != default)
        {
            return oldStatements.ReplaceRange(
                nextStatement,
                [
                    newStatement.WithLeadingTrivia(nextStatementLeading).WithTrailingTrivia(precedingEndOfLine),
                    nextStatement.WithLeadingTrivia(nextStatementLeading.Skip(nextStatementLeading.IndexOf(precedingEndOfLine) + 1)),
                ]);
        }

        // Otherwise, the next statement has no leading new-line.  Try to figure out how to place the new statement.

        // Attempt to indent by the same amount as the next statement.
        if (nextStatementLeading is [(kind: SyntaxKind.WhitespaceTrivia) indentation])
            newStatement = newStatement.WithLeadingTrivia(indentation);

        // Attempt to use the same end of line as the next statement.  Fall back to an elastic newline if not present.
        newStatement = newStatement.WithTrailingTrivia(
            nextStatement.GetTrailingTrivia() is [.., (kind: SyntaxKind.EndOfLineTrivia) endOfLine] ? endOfLine : ElasticCarriageReturnLineFeed);

        return oldStatements.ReplaceRange(
            nextStatement, [newStatement, nextStatement]);
    }

    private static bool IsBlockLike(SyntaxNode node) => node is BlockSyntax or SwitchSectionSyntax;

    private static SyntaxList<StatementSyntax> GetStatements(SyntaxNode blockLike)
        => blockLike switch
        {
            BlockSyntax block => block.Statements,
            SwitchSectionSyntax switchSection => switchSection.Statements,
            _ => throw ExceptionUtilities.UnexpectedValue(blockLike),
        };

    private static SyntaxNode WithStatements(SyntaxNode blockLike, SyntaxList<StatementSyntax> statements)
        => blockLike switch
        {
            BlockSyntax block => block.WithStatements(statements),
            SwitchSectionSyntax switchSection => switchSection.WithStatements(statements),
            _ => throw ExceptionUtilities.UnexpectedValue(blockLike),
        };
}

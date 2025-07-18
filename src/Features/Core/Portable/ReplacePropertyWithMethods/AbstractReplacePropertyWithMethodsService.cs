﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.LanguageService;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.CodeAnalysis.ReplacePropertyWithMethods;

internal abstract class AbstractReplacePropertyWithMethodsService<TIdentifierNameSyntax, TExpressionSyntax, TCrefSyntax, TStatementSyntax, TPropertySyntax>
    : IReplacePropertyWithMethodsService
    where TIdentifierNameSyntax : TExpressionSyntax
    where TExpressionSyntax : SyntaxNode
    where TCrefSyntax : SyntaxNode
    where TStatementSyntax : SyntaxNode
    where TPropertySyntax : SyntaxNode
{
    public abstract SyntaxNode GetPropertyNodeToReplace(SyntaxNode propertyDeclaration);
    public abstract Task<ImmutableArray<SyntaxNode>> GetReplacementMembersAsync(
        Document document, IPropertySymbol property, SyntaxNode propertyDeclaration, IFieldSymbol? propertyBackingField, string desiredGetMethodName, string desiredSetMethodName, CancellationToken cancellationToken);

    protected abstract TCrefSyntax? TryGetCrefSyntax(TIdentifierNameSyntax identifierName);
    protected abstract TCrefSyntax CreateCrefSyntax(TCrefSyntax originalCref, SyntaxToken identifierToken, SyntaxNode? parameterType);

    protected abstract TExpressionSyntax UnwrapCompoundAssignment(SyntaxNode compoundAssignment, TExpressionSyntax readExpression);
    public async Task<SyntaxNode?> GetPropertyDeclarationAsync(CodeRefactoringContext context)
        => await context.TryGetRelevantNodeAsync<TPropertySyntax>().ConfigureAwait(false);

    protected static SyntaxNode GetFieldReference(SyntaxGenerator generator, IFieldSymbol propertyBackingField)
    {
        var memberName = generator.IdentifierName(propertyBackingField.Name);
        if (propertyBackingField.IsStatic)
        {
            return propertyBackingField.ContainingType == null
                ? memberName
                : generator.MemberAccessExpression(
                    generator.TypeExpression(propertyBackingField.ContainingType),
                    memberName);
        }

        return generator.MemberAccessExpression(generator.ThisExpression(), memberName);
    }

    public async Task ReplaceReferenceAsync(
        Document document,
        SyntaxEditor editor, SyntaxNode identifierName,
        IPropertySymbol property, IFieldSymbol? propertyBackingField,
        string desiredGetMethodName, string desiredSetMethodName,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var semanticFacts = document.GetRequiredLanguageService<ISemanticFactsService>();
        var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();

        var referenceReplacer = new ReferenceReplacer(
            this, semanticModel, syntaxFacts, semanticFacts, editor,
            (TIdentifierNameSyntax)identifierName, property, propertyBackingField,
            desiredGetMethodName, desiredSetMethodName, cancellationToken);
        referenceReplacer.Do();
    }

    private delegate TExpressionSyntax GetWriteValue(ReferenceReplacer replacer, SyntaxNode parent);

    private readonly struct ReferenceReplacer
    {
        private readonly AbstractReplacePropertyWithMethodsService<TIdentifierNameSyntax, TExpressionSyntax, TCrefSyntax, TStatementSyntax, TPropertySyntax> _service;
        private readonly SemanticModel _semanticModel;
        private readonly ISyntaxFactsService _syntaxFacts;
        private readonly ISemanticFactsService _semanticFacts;
        private readonly SyntaxEditor _editor;
        private readonly IPropertySymbol _property;
        private readonly IFieldSymbol? _propertyBackingField;
        private readonly string _desiredGetMethodName;
        private readonly string _desiredSetMethodName;

        private readonly TIdentifierNameSyntax _identifierName;
        private readonly TExpressionSyntax _expression;
        private readonly TCrefSyntax? _cref;
        private readonly CancellationToken _cancellationToken;

        public ReferenceReplacer(
            AbstractReplacePropertyWithMethodsService<TIdentifierNameSyntax, TExpressionSyntax, TCrefSyntax, TStatementSyntax, TPropertySyntax> service,
            SemanticModel semanticModel,
            ISyntaxFactsService syntaxFacts,
            ISemanticFactsService semanticFacts,
            SyntaxEditor editor,
            TIdentifierNameSyntax identifierName,
            IPropertySymbol property,
            IFieldSymbol? propertyBackingField,
            string desiredGetMethodName,
            string desiredSetMethodName,
            CancellationToken cancellationToken)
        {
            _service = service;
            _semanticModel = semanticModel;
            _syntaxFacts = syntaxFacts;
            _semanticFacts = semanticFacts;
            _editor = editor;
            _identifierName = identifierName;
            _property = property;
            _propertyBackingField = propertyBackingField;
            _desiredGetMethodName = desiredGetMethodName;
            _desiredSetMethodName = desiredSetMethodName;
            _cancellationToken = cancellationToken;

            _expression = _identifierName;
            _cref = _service.TryGetCrefSyntax(_identifierName);
            if (_syntaxFacts.IsNameOfSimpleMemberAccessExpression(_expression) ||
                _syntaxFacts.IsNameOfMemberBindingExpression(_expression))
            {
                _expression = (TExpressionSyntax)_expression.Parent!;
            }

            Contract.ThrowIfNull(_expression.Parent, $"Parent of {_expression} is null.");
        }

        // To avoid allocating lambdas each time we hit a reference, we instead
        // statically cache the lambdas here and invoke them on demand with the 
        // data they need when we hit a reference.

        // Note: the reason for these lambdas is so that as we rewrite the tree
        // we see the results of the rewrite as we go higher up.  For example,
        // if we have:
        //
        //      this.Prop = this.Prop + 1;
        //
        // then when we hit "this.Prop" on the left of the equals, we'll want
        // to see the results of the tree *after* we've replaced "this.Prop + 1"
        // with "this.GetProp() + 1".  If we don't do this, and instead examine
        // the "this.Prop" in the original tree, then we won't see that other
        // rewrite.
        //
        // The SyntaxEditor API works by passing in these callbacks when we 
        // replace a node N.  It will call us back with what N looks like after
        // all the rewrites that occurred underneath it.
        // 
        // In order to avoid allocating each time we hit a reference, we just
        // create these statically and pass them in.

        private static readonly GetWriteValue s_getWriteValueForLeftSideOfAssignment =
            (replacer, parent) =>
            {
                return (TExpressionSyntax)replacer._syntaxFacts.GetRightHandSideOfAssignment(parent)!;
            };

        private static readonly GetWriteValue s_getWriteValueForIncrementOrDecrement =
            (replacer, parent) =>
            {
                // We're being read from and written to (i.e. Prop++), we need to replace with a
                // Get and a Set call.
                var readExpression = replacer.GetReadExpression(
                    keepTrivia: false, conflictMessage: null);
                var literalOne = replacer.Generator.LiteralExpression(1);

                var writeValue = replacer._syntaxFacts.IsOperandOfIncrementExpression(replacer._expression)
                    ? replacer.Generator.AddExpression(readExpression, literalOne)
                    : replacer.Generator.SubtractExpression(readExpression, literalOne);

                return (TExpressionSyntax)writeValue;
            };

        private static readonly GetWriteValue getWriteValueForCompoundAssignment =
            (replacer, parent) =>
            {
                // We're being read from and written to from a compound assignment 
                // (i.e. Prop *= X), we need to replace with a Get and a Set call.

                var readExpression = replacer.GetReadExpression(
                    keepTrivia: false, conflictMessage: null);

                // Convert "Prop *= X" into "Prop * X".
                return replacer._service.UnwrapCompoundAssignment(parent, readExpression);
            };

        private static readonly Func<SyntaxNode, SyntaxGenerator, ReplaceParentArgs, SyntaxNode> replaceParentCallback =
            (parent, generator, args) =>
            {
                var replacer = args.Replacer;

                var writeValue = args.GetWriteValue(replacer, parent);
                var writeExpression = replacer.GetWriteExpression(
                    writeValue, args.KeepTrivia, args.ConflictMessage);
                if (replacer._expression.Parent is TStatementSyntax)
                {
                    writeExpression = replacer.Generator.ExpressionStatement(writeExpression);
                }

                return writeExpression;
            };

        private SyntaxGenerator Generator => _editor.Generator;

        public void Do()
        {
            if (_cref != null)
            {
                // We're in a documentation comment. Replace with a reference to the getter if one exists,
                // otherwise to the setter.
                _editor.ReplaceNode(_cref, GetCrefReference(_cref));
            }
            else if (_semanticFacts.IsInOutContext(_semanticModel, _expression, _cancellationToken) ||
                _semanticFacts.IsInRefContext(_semanticModel, _expression, _cancellationToken))
            {
                // Code wasn't legal (you can't reference a property in an out/ref position in C#).
                // Just replace this with a simple GetCall, but mark it so it's clear there's an error.
                ReplaceRead(
                    keepTrivia: true,
                    conflictMessage: FeaturesResources.Property_cannot_safely_be_replaced_with_a_method_call);
            }
            else if (_syntaxFacts.IsAttributeNamedArgumentIdentifier(_expression))
            {
                // Can't replace a property used in an attribute argument.
                var newIdentifierName = AddConflictAnnotation(_identifierName,
                    FeaturesResources.Property_cannot_safely_be_replaced_with_a_method_call);

                _editor.ReplaceNode(_identifierName, newIdentifierName);
            }
            else if (_syntaxFacts.IsLeftSideOfAssignment(_expression))
            {
                // We're only being written to here.  This is safe to replace with a call to the 
                // setter.
                ReplaceWrite(
                    s_getWriteValueForLeftSideOfAssignment,
                    keepTrivia: true,
                    conflictMessage: null);
            }
            else if (_syntaxFacts.IsLeftSideOfAnyAssignment(_expression))
            {
                ReplaceWrite(
                    getWriteValueForCompoundAssignment,
                    keepTrivia: true,
                    conflictMessage: null);
            }
            else if (_syntaxFacts.IsOperandOfIncrementOrDecrementExpression(_expression))
            {
                ReplaceWrite(
                    s_getWriteValueForIncrementOrDecrement,
                    keepTrivia: true,
                    conflictMessage: null);
            }
            else if (_syntaxFacts.IsInferredAnonymousObjectMemberDeclarator(_expression.Parent)) //.IsParentKind(SyntaxKind.AnonymousObjectMemberDeclarator))
            {
                // If we have:   new { this.Prop }.  We need ot convert it to:
                //               new { Prop = this.GetProp() }
                var declarator = _expression.Parent;
                var readExpression = GetReadExpression(keepTrivia: true, conflictMessage: null);

                var newDeclarator = Generator.NamedAnonymousObjectMemberDeclarator(
                    _identifierName.WithoutTrivia(),
                    readExpression);

                // We know declarator isn't null due to the earlier call to IsInferredAnonymousObjectMemberDeclarator
                _editor.ReplaceNode(declarator!, newDeclarator);
            }
            else if (_syntaxFacts.IsRightOfQualifiedName(_identifierName))
            {
                // Found a reference in a qualified name.  This happens for VB explicit interface
                // names.  We don't want to update this.  (The "Implement IGoo.Bar" clause will be
                // updated when we generate the actual Get/Set methods.
                return;
            }
            else
            {
                // No writes.  Replace this with an appropriate read.
                ReplaceRead(keepTrivia: true, conflictMessage: null);
            }
        }

        private void ReplaceRead(bool keepTrivia, string? conflictMessage)
        {
            _editor.ReplaceNode(
                _expression,
                GetReadExpression(keepTrivia, conflictMessage));
        }

        private void ReplaceWrite(
            GetWriteValue getWriteValue,
            bool keepTrivia,
            string? conflictMessage)
        {
            Contract.ThrowIfNull(_expression.Parent, $"Parent of {_expression} is null.");

            // Call this overload so we can see this node after already replacing any 
            // references in the writing side of it.
            _editor.ReplaceNode(
                _expression.Parent,
                replaceParentCallback,
                new ReplaceParentArgs(this, getWriteValue, keepTrivia, conflictMessage));
        }

        private TCrefSyntax GetCrefReference(TCrefSyntax originalCref)
        {
            SyntaxToken newIdentifierToken;
            SyntaxNode? parameterType;
            if (_property.GetMethod != null)
            {
                newIdentifierToken = Generator.Identifier(_desiredGetMethodName);
                parameterType = null;
            }
            else
            {
                newIdentifierToken = Generator.Identifier(_desiredSetMethodName);
                parameterType = Generator.TypeExpression(_property.Type);
            }

            return _service.CreateCrefSyntax(originalCref, newIdentifierToken, parameterType);
        }

        private SyntaxNode QualifyIfAppropriate(IFieldSymbol propertyBackingField, SyntaxNode newIdentifierName)
        {
            // See if already qualified appropriate.
            if (_expression is TIdentifierNameSyntax)
            {
                var container = propertyBackingField.IsStatic
                    ? Generator.TypeExpression(_property.ContainingType)
                    : Generator.ThisExpression();

                return Generator.MemberAccessExpression(container, newIdentifierName)
                                .WithAdditionalAnnotations(Simplifier.Annotation);
            }

            return newIdentifierName;
        }

        private TExpressionSyntax GetReadExpression(
            bool keepTrivia, string? conflictMessage)
        {
            if (ShouldReadFromBackingField())
            {
                var newIdentifierToken = AddConflictAnnotation(Generator.Identifier(_propertyBackingField.Name), conflictMessage);
                var newIdentifierName = QualifyIfAppropriate(_propertyBackingField, Generator.IdentifierName(newIdentifierToken));

                if (keepTrivia)
                {
                    newIdentifierName = newIdentifierName.WithTriviaFrom(_identifierName);
                }

                return _expression.ReplaceNode(_identifierName, newIdentifierName);
            }
            else
            {
                return GetGetInvocationExpression(keepTrivia, conflictMessage);
            }
        }

        private SyntaxNode GetWriteExpression(
            TExpressionSyntax writeValue,
            bool keepTrivia,
            string? conflictMessage)
        {
            if (ShouldWriteToBackingField())
            {
                var newIdentifierToken = AddConflictAnnotation(Generator.Identifier(_propertyBackingField.Name), conflictMessage);
                var newIdentifierName = QualifyIfAppropriate(_propertyBackingField, Generator.IdentifierName(newIdentifierToken));

                if (keepTrivia)
                {
                    newIdentifierName = newIdentifierName.WithTriviaFrom(_identifierName);
                }

                return Generator.AssignmentStatement(
                    _expression.ReplaceNode(_identifierName, newIdentifierName),
                    writeValue);
            }
            else
            {
                return GetSetInvocationExpression(writeValue, keepTrivia, conflictMessage);
            }
        }

        private TExpressionSyntax GetGetInvocationExpression(
            bool keepTrivia, string? conflictMessage)
        {
            return GetInvocationExpression(_desiredGetMethodName, argument: null, keepTrivia, conflictMessage);
        }

        private TExpressionSyntax GetInvocationExpression(
            string desiredName, SyntaxNode? argument, bool keepTrivia, string? conflictMessage)
        {
            var newIdentifier = AddConflictAnnotation(
                Generator.Identifier(desiredName), conflictMessage);

            var newIdentifierName = Generator.IdentifierName(newIdentifier);
            if (keepTrivia)
            {
                newIdentifierName = newIdentifierName.WithLeadingTrivia(_identifierName.GetLeadingTrivia());
            }

            var updatedExpression = _expression.ReplaceNode(_identifierName, newIdentifierName);

            var arguments = argument == null
                ? []
                : SpecializedCollections.SingletonEnumerable(argument);

            var invocation = Generator.InvocationExpression(updatedExpression, arguments);
            if (keepTrivia)
            {
                invocation = invocation.WithTrailingTrivia(_identifierName.GetTrailingTrivia());
            }

            return (TExpressionSyntax)invocation;
        }

        [MemberNotNullWhen(true, nameof(_propertyBackingField))]
        private bool ShouldReadFromBackingField()
            => _propertyBackingField != null && _property.GetMethod == null;

        private SyntaxNode GetSetInvocationExpression(
            TExpressionSyntax writeValue, bool keepTrivia, string? conflictMessage)
        {
            return GetInvocationExpression(_desiredSetMethodName,
                argument: Generator.Argument(writeValue),
                keepTrivia: keepTrivia,
                conflictMessage: conflictMessage);
        }

        [MemberNotNullWhen(true, nameof(_propertyBackingField))]
        private bool ShouldWriteToBackingField()
            => _propertyBackingField != null && _property.SetMethod == null;

        private static TIdentifierNameSyntax AddConflictAnnotation(TIdentifierNameSyntax name, string conflictMessage)
        {
            return name.ReplaceToken(
                name.GetFirstToken(),
                AddConflictAnnotation(name.GetFirstToken(), conflictMessage));
        }

        private static SyntaxToken AddConflictAnnotation(SyntaxToken token, string? conflictMessage)
        {
            if (conflictMessage != null)
            {
                token = token.WithAdditionalAnnotations(ConflictAnnotation.Create(conflictMessage));
            }

            return token;
        }

        private readonly struct ReplaceParentArgs(ReferenceReplacer replacer, GetWriteValue getWriteValue, bool keepTrivia, string? conflictMessage)
        {
            public readonly ReferenceReplacer Replacer = replacer;
            public readonly GetWriteValue GetWriteValue = getWriteValue;
            public readonly bool KeepTrivia = keepTrivia;
            public readonly string? ConflictMessage = conflictMessage;
        }
    }
}

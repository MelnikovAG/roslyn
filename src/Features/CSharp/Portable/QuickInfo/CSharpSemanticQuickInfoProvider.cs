﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Copilot;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.GoToDefinition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.CSharp.QuickInfo;

[ExportQuickInfoProvider(QuickInfoProviderNames.Semantic, LanguageNames.CSharp), Shared]
internal sealed class CSharpSemanticQuickInfoProvider : CommonSemanticQuickInfoProvider
{
    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public CSharpSemanticQuickInfoProvider()
    {
    }

    /// <summary>
    /// If the token is the '=>' in a lambda, or the 'delegate' in an anonymous function,
    /// return the syntax for the lambda or anonymous function.
    /// </summary>
    protected override bool GetBindableNodeForTokenIndicatingLambda(SyntaxToken token, [NotNullWhen(returnValue: true)] out SyntaxNode? found)
    {
        if (token.IsKind(SyntaxKind.EqualsGreaterThanToken)
            && token.Parent is (kind: SyntaxKind.ParenthesizedLambdaExpression or SyntaxKind.SimpleLambdaExpression))
        {
            // () =>
            found = token.Parent;
            return true;
        }
        else if (token.IsKind(SyntaxKind.DelegateKeyword) && token.Parent.IsKind(SyntaxKind.AnonymousMethodExpression))
        {
            // delegate (...) { ... }
            found = token.Parent;
            return true;
        }

        found = null;
        return false;
    }

    protected override bool GetBindableNodeForTokenIndicatingPossibleIndexerAccess(SyntaxToken token, [NotNullWhen(returnValue: true)] out SyntaxNode? found)
    {
        if (token.Kind() is SyntaxKind.CloseBracketToken or SyntaxKind.OpenBracketToken &&
            token.Parent?.Parent.IsKind(SyntaxKind.ElementAccessExpression) == true)
        {
            found = token.Parent.Parent;
            return true;
        }

        found = null;
        return false;
    }

    protected override bool GetBindableNodeForTokenIndicatingMemberAccess(SyntaxToken token, out SyntaxToken found)
    {
        if (token.IsKind(SyntaxKind.DotToken) &&
            token.Parent is MemberAccessExpressionSyntax memberAccess)
        {
            found = memberAccess.Name.Identifier;
            return true;
        }

        found = default;
        return false;
    }

    protected override bool ShouldCheckPreviousToken(SyntaxToken token)
        => !token.Parent.IsKind(SyntaxKind.XmlCrefAttribute);

    protected override (NullableAnnotation, NullableFlowState) GetNullabilityAnalysis(SemanticModel semanticModel, ISymbol symbol, SyntaxNode node, CancellationToken cancellationToken)
    {
        // Anything less than C# 8 we just won't show anything, even if the compiler could theoretically give analysis
        var parseOptions = (CSharpParseOptions)semanticModel.SyntaxTree!.Options;
        if (parseOptions.LanguageVersion < LanguageVersion.CSharp8)
        {
            return default;
        }

        // If the user doesn't have nullable enabled, don't show anything. For now we're not trying to be more precise if the user has just annotations or just
        // warnings. If the user has annotations off then things that are oblivious might become non-null (which is a lie) and if the user has warnings off then
        // that probably implies they're not actually trying to know if their code is correct. We can revisit this if we have specific user scenarios.
        var nullableContext = semanticModel.GetNullableContext(node.SpanStart);
        if (!nullableContext.WarningsEnabled() || !nullableContext.AnnotationsEnabled())
        {
            return default;
        }

        // Although GetTypeInfo can return nullability for uses of all sorts of things, it's not always useful for quick info.
        // For example, if you have a call to a method with a nullable return, the fact it can be null is already captured
        // in the return type shown -- there's no flow analysis information there.
        switch (symbol)
        {
            // Ignore constant values for nullability flow state
            case IFieldSymbol { HasConstantValue: true }: return default;
            case ILocalSymbol { HasConstantValue: true }: return default;

            // Symbols with useful quick info
            case IFieldSymbol:
            case ILocalSymbol:
            case IParameterSymbol:
            case IPropertySymbol:
            case IRangeVariableSymbol:
                break;

            // Although methods have no nullable flow state,
            // we still want to show when they are "not nullable aware".
            case IMethodSymbol { ReturnsVoid: false }:
                break;

            default:
                return default;
        }

        if (symbol.GetMemberType() is { IsValueType: false, NullableAnnotation: NullableAnnotation.None })
        {
            return (NullableAnnotation.None, NullableFlowState.NotNull);
        }

        var typeInfo = semanticModel.GetTypeInfo(node, cancellationToken);

        // Nullability is a reference type only feature, value types can use
        // something like "int?"  to be nullable but that ends up encasing as
        // Nullable<int>, which isn't exactly the same. To avoid confusion and
        // extra noise, we won't show nullable flow state for value types
        if (typeInfo.Type?.IsValueType == true)
        {
            return default;
        }

        var nullability = typeInfo.Nullability;
        return (nullability.Annotation, nullability.FlowState);
    }

    protected override async Task<OnTheFlyDocsInfo?> GetOnTheFlyDocsInfoAsync(QuickInfoContext context, CancellationToken cancellationToken)
    {
        var document = context.Document;
        var position = context.Position;

        if (document.GetLanguageService<ICopilotCodeAnalysisService>() is not { } copilotService ||
            !await copilotService.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (document.GetLanguageService<ICopilotOptionsService>() is not { } service ||
            !await service.IsOnTheFlyDocsOptionEnabledAsync().ConfigureAwait(false))
        {
            return null;
        }
        if (document.IsRazorDocument())
        {
            return null;
        }

        var symbolService = document.GetRequiredLanguageService<IGoToDefinitionSymbolService>();
        var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var (symbol, _, _) = await symbolService.GetSymbolProjectAndBoundSpanAsync(
            document, semanticModel, position, cancellationToken).ConfigureAwait(false);

        // Don't show on-the-fly-docs for namespace symbols.
        if (symbol is null or INamespaceSymbol)
            return null;

        if (symbol.MetadataToken != 0)
        {
            OnTheFlyDocsLogger.LogHoveredMetadataSymbol();
        }
        else
        {
            OnTheFlyDocsLogger.LogHoveredSourceSymbol();
        }

        if (symbol.DeclaringSyntaxReferences.Length == 0)
        {
            return null;
        }

        // Checks to see if any of the files containing the symbol are excluded.
        var hasContentExcluded = false;
        var symbolFilePaths = symbol.DeclaringSyntaxReferences.Select(reference => reference.SyntaxTree.FilePath);
        foreach (var symbolFilePath in symbolFilePaths)
        {
            if (await copilotService.IsFileExcludedAsync(symbolFilePath, cancellationToken).ConfigureAwait(false))
            {
                hasContentExcluded = true;
                Logger.Log(FunctionId.Copilot_On_The_Fly_Docs_Content_Excluded, logLevel: LogLevel.Information);
                break;
            }
        }

        var solution = document.Project.Solution;
        var declarationCode = symbol.DeclaringSyntaxReferences.Select(reference =>
        {
            var span = reference.Span;
            var syntaxReferenceDocument = solution.GetDocument(reference.SyntaxTree);
            if (syntaxReferenceDocument is not null)
            {
                return new OnTheFlyDocsRelevantFileInfo(syntaxReferenceDocument, span);
            }

            return null;
        }).ToImmutableArray();

        var additionalContext = OnTheFlyDocsUtilities.GetAdditionalOnTheFlyDocsContext(solution, symbol);

        return new OnTheFlyDocsInfo(symbol.ToDisplayString(), declarationCode, symbol.Language, hasContentExcluded, additionalContext);
    }
}

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using FixAllScope = Microsoft.CodeAnalysis.CodeFixes.FixAllScope;

namespace Microsoft.CodeAnalysis.CodeRefactorings;

internal abstract partial class SyntaxEditorBasedCodeRefactoringProvider : CodeRefactoringProvider
{
    protected static readonly ImmutableArray<FixAllScope> DefaultFixAllScopes = [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution];
    protected static readonly ImmutableArray<FixAllScope> AllFixAllScopes = [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution, FixAllScope.ContainingType, FixAllScope.ContainingMember];

    protected abstract ImmutableArray<FixAllScope> SupportedFixAllScopes { get; }
    protected virtual CodeActionCleanup Cleanup => CodeActionCleanup.Default;

    internal sealed override FixAllProvider? GetFixAllProvider()
    {
        if (SupportedFixAllScopes.IsEmpty)
            return null;

        return FixAllProvider.Create(
            async (fixAllContext, document, fixAllSpans) =>
                await this.FixAllAsync(document, fixAllSpans, fixAllContext.CodeActionEquivalenceKey, fixAllContext.CancellationToken).ConfigureAwait(false),
            SupportedFixAllScopes,
            this.Cleanup);
    }

    protected Task<Document> FixAsync(
        Document document,
        TextSpan fixAllSpan,
        string? equivalenceKey,
        CancellationToken cancellationToken)
    {
        return FixAllWithEditorAsync(document,
            editor => FixAllAsync(document, [fixAllSpan], editor, equivalenceKey, cancellationToken),
            cancellationToken);
    }

    protected Task<Document> FixAllAsync(
        Document document,
        Optional<ImmutableArray<TextSpan>> fixAllSpans,
        string? equivalenceKey,
        CancellationToken cancellationToken)
    {
        return FixAllWithEditorAsync(document, FixAllAsync, cancellationToken);

        // Local functions
        Task FixAllAsync(SyntaxEditor editor)
        {
            // Fix the entire document if there are no sub-spans to fix.
            var spans = fixAllSpans.HasValue ? fixAllSpans.Value : [editor.OriginalRoot.FullSpan];
            return this.FixAllAsync(document, spans, editor, equivalenceKey, cancellationToken);
        }
    }

    internal static async Task<Document> FixAllWithEditorAsync(
        Document document,
        Func<SyntaxEditor, Task> editAsync,
        CancellationToken cancellationToken)
    {
        var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var editor = new SyntaxEditor(root, document.Project.Solution.Services);

        await editAsync(editor).ConfigureAwait(false);

        var newRoot = editor.GetChangedRoot();
        return document.WithSyntaxRoot(newRoot);
    }

    protected abstract Task FixAllAsync(
        Document document,
        ImmutableArray<TextSpan> fixAllSpans,
        SyntaxEditor editor,
        string? equivalenceKey,
        CancellationToken cancellationToken);
}

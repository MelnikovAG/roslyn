﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Editor.UnitTests.Utilities;
using Microsoft.CodeAnalysis.FindReferences;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.UnitTests.FindReferences;

[UseExportProvider]
public sealed class FindReferencesCommandHandlerTests
{
    private sealed class MockFindUsagesContext : FindUsagesContext
    {
        public readonly List<DefinitionItem> Result = [];

        public override ValueTask OnDefinitionFoundAsync(DefinitionItem definition, CancellationToken cancellationToken)
        {
            lock (Result)
            {
                Result.Add(definition);
            }

            return default;
        }
    }

    private sealed class MockStreamingFindUsagesPresenter : IStreamingFindUsagesPresenter
    {
        private readonly FindUsagesContext _context;

        public MockStreamingFindUsagesPresenter(FindUsagesContext context)
            => _context = context;

        public void ClearAll()
        {
        }

        public (FindUsagesContext, CancellationToken) StartSearch(string title, StreamingFindUsagesPresenterOptions options)
            => (_context, CancellationToken.None);
    }

    [WpfFact, Trait(Traits.Feature, Traits.Features.FindReferences)]
    public async Task TestFindReferencesAsynchronousCall()
    {
        using var workspace = EditorTestWorkspace.CreateCSharp("class C { C() { new C(); } }");
        var context = new MockFindUsagesContext();
        var presenter = new MockStreamingFindUsagesPresenter(context);

        var listenerProvider = workspace.ExportProvider.GetExportedValue<IAsynchronousOperationListenerProvider>();

        var handler = new FindReferencesCommandHandler(
            new FindReferencesNavigationService(
                workspace.ExportProvider.GetExportedValue<IThreadingContext>(),
                presenter,
                listenerProvider,
                workspace.GlobalOptions));

        var textView = workspace.Documents[0].GetTextView();
        textView.Caret.MoveTo(new SnapshotPoint(textView.TextSnapshot, 7));
        handler.ExecuteCommand(new FindReferencesCommandArgs(
            textView,
            textView.TextBuffer), TestCommandExecutionContext.Create());

        var waiter = listenerProvider.GetWaiter(FeatureAttribute.FindReferences);
        await waiter.ExpeditedWaitAsync();
        AssertResult(context.Result, "C.C()", "class C");
    }

    private static void AssertResult(
        List<DefinitionItem> result,
        params string[] definitions)
    {
        Assert.Equal(result.Select(kvp => kvp.DisplayParts.JoinText()).OrderBy(a => a),
                     definitions.OrderBy(a => a));
    }
}

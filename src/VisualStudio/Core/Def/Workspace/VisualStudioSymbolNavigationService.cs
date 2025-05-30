﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MetadataAsSource;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServices.Implementation.Library;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation;

[ExportWorkspaceService(typeof(ISymbolNavigationService), ServiceLayer.Host), Shared]
[method: ImportingConstructor]
[method: Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
internal sealed partial class VisualStudioSymbolNavigationService(
    SVsServiceProvider serviceProvider,
    IGlobalOptionService globalOptions,
    IThreadingContext threadingContext,
    IVsEditorAdaptersFactoryService editorAdaptersFactory,
    IMetadataAsSourceFileService metadataAsSourceFileService,
    VisualStudioWorkspace workspace) : ISymbolNavigationService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IGlobalOptionService _globalOptions = globalOptions;
    private readonly IThreadingContext _threadingContext = threadingContext;
    private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory = editorAdaptersFactory;
    private readonly IMetadataAsSourceFileService _metadataAsSourceFileService = metadataAsSourceFileService;
    private readonly VisualStudioWorkspace _workspace = workspace;

    public async Task<INavigableLocation?> GetNavigableLocationAsync(
        ISymbol symbol, Project project, CancellationToken cancellationToken)
    {
        if (project == null || symbol == null)
            return null;

        var solution = project.Solution;
        symbol = symbol.OriginalDefinition;

        // Prefer visible source locations if possible.
        var sourceLocations = symbol.Locations.Where(loc => loc.IsInSource);
        var visibleSourceLocations = sourceLocations.Where(loc => loc.IsVisibleSourceLocation());
        var sourceLocation = visibleSourceLocations.FirstOrDefault() ?? sourceLocations.FirstOrDefault();

        if (sourceLocation != null)
        {
            var targetDocument = solution.GetDocument(sourceLocation.SourceTree);
            if (targetDocument != null)
            {
                var navigationService = solution.Services.GetRequiredService<IDocumentNavigationService>();
                return await navigationService.GetLocationForPositionAsync(
                    solution.Workspace, targetDocument.Id, sourceLocation.SourceSpan.Start, cancellationToken).ConfigureAwait(false);
            }
        }

        // We don't have a source document, so show the Metadata as Source view in a preview tab.

        if (!_metadataAsSourceFileService.IsNavigableMetadataSymbol(symbol))
        {
            return null;
        }

        // See if there's another .Net language service that can handle navigating to this metadata symbol (for example, F#).
        var docCommentId = symbol.GetDocumentationCommentId();
        var assemblyName = symbol.ContainingAssembly.Identity.Name;
        if (docCommentId != null && assemblyName != null)
        {
            foreach (var lazyService in solution.Services.ExportProvider.GetExports<ICrossLanguageSymbolNavigationService>())
            {
                var crossLanguageService = lazyService.Value;
                var crossLanguageLocation = await crossLanguageService.TryGetNavigableLocationAsync(
                    assemblyName, docCommentId, cancellationToken).ConfigureAwait(false);
                if (crossLanguageLocation != null)
                    return crossLanguageLocation;
            }
        }

        // Should we prefer navigating to the Object Browser over metadata-as-source?
        if (_globalOptions.GetOption(VisualStudioNavigationOptionsStorage.NavigateToObjectBrowser, project.Language))
        {
            var libraryService = project.Services.GetService<ILibraryService>();
            if (libraryService == null)
            {
                return null;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            var navInfo = libraryService.NavInfoFactory.CreateForSymbol(symbol, project, compilation);
            navInfo ??= libraryService.NavInfoFactory.CreateForProject(project);

            if (navInfo != null)
            {
                var navigationTool = _serviceProvider.GetServiceOnMainThread<SVsObjBrowser, IVsNavigationTool>();
                return new NavigableLocation(async (options, cancellationToken) =>
                {
                    await _threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                    return navigationTool.NavigateToNavInfo(navInfo) == VSConstants.S_OK;
                });
            }

            // Note: we'll fallback to Metadata-As-Source if we fail to get IVsNavInfo, but that should never happen.
        }

        // Generate new source or retrieve existing source for the symbol in question
        return await GetNavigableLocationForMetadataAsync(project, symbol, cancellationToken).ConfigureAwait(false);
    }

    private async Task<INavigableLocation?> GetNavigableLocationForMetadataAsync(
        Project project, ISymbol symbol, CancellationToken cancellationToken)
    {
        var masOptions = _globalOptions.GetMetadataAsSourceOptions();

        var result = await _metadataAsSourceFileService.GetGeneratedFileAsync(_workspace, project, symbol, signaturesOnly: false, options: masOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new NavigableLocation(async (options, cancellationToken) =>
        {
            await _threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var vsRunningDocumentTable4 = _serviceProvider.GetServiceOnMainThread<SVsRunningDocumentTable, IVsRunningDocumentTable4>();
            var fileAlreadyOpen = vsRunningDocumentTable4.IsMonikerValid(result.FilePath);

            var openDocumentService = _serviceProvider.GetServiceOnMainThread<SVsUIShellOpenDocument, IVsUIShellOpenDocument>();
            openDocumentService.OpenDocumentViaProject(result.FilePath, VSConstants.LOGVIEWID.TextView_guid, out _, out _, out _, out var windowFrame);

            var documentCookie = vsRunningDocumentTable4.GetDocumentCookie(result.FilePath);

            var vsTextBuffer = (IVsTextBuffer)vsRunningDocumentTable4.GetDocumentData(documentCookie);

            var textBuffer = _editorAdaptersFactory.GetDataBuffer(vsTextBuffer);

            if (!fileAlreadyOpen)
            {
                ErrorHandler.ThrowOnFailure(windowFrame.SetProperty((int)__VSFPROPID5.VSFPROPID_IsProvisional, true));
                ErrorHandler.ThrowOnFailure(windowFrame.SetProperty((int)__VSFPROPID5.VSFPROPID_OverrideCaption, result.DocumentTitle));
                ErrorHandler.ThrowOnFailure(windowFrame.SetProperty((int)__VSFPROPID5.VSFPROPID_OverrideToolTip, result.DocumentTooltip));
            }

            // Subtle issue.  We may already be in a provisional-tab. 'Showing' the window frame here will cause it to
            // to take over the curren provisional-tab, cause a wait-indicators in the original to be dismissed (causing
            // cancellation).   To avoid that problem, we disable cancellation from this point.  While not ideal, it is
            // not problematic as we already forced the document to be opened here.  So actually navigating to the
            // location in it is effectively free.
            windowFrame.Show();
            cancellationToken = default;

            var openedDocument = textBuffer?.AsTextContainer().GetRelatedDocuments().FirstOrDefault();
            if (openedDocument != null)
            {
                var editorWorkspace = openedDocument.Project.Solution.Workspace;
                var navigationService = editorWorkspace.Services.GetRequiredService<IDocumentNavigationService>();

                await navigationService.TryNavigateToPositionAsync(
                    _threadingContext,
                    editorWorkspace,
                    openedDocument.Id,
                    result.IdentifierLocation.SourceSpan.Start,
                    options with { PreferProvisionalTab = true },
                    cancellationToken).ConfigureAwait(false);
            }

            return true;
        });
    }

    public async Task<bool> TrySymbolNavigationNotifyAsync(ISymbol symbol, Project project, CancellationToken cancellationToken)
    {
        await _threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var definitionItem = symbol.ToNonClassifiedDefinitionItem(project.Solution, includeHiddenLocations: true);
        definitionItem.Properties.TryGetValue(DefinitionItem.RQNameKey1, out var rqName);

        var result = await TryGetNavigationAPIRequiredArgumentsAsync(definitionItem, rqName, cancellationToken).ConfigureAwait(true);
        if (result is not var (hierarchy, itemID, navigationNotify))
            return false;

        var returnCode = navigationNotify.OnBeforeNavigateToSymbol(
            hierarchy,
            itemID,
            rqName,
            out var navigationHandled);

        return returnCode == VSConstants.S_OK && navigationHandled == 1;
    }

    public async Task<(string filePath, LinePosition linePosition)?> GetExternalNavigationSymbolLocationAsync(
        DefinitionItem definitionItem, CancellationToken cancellationToken)
    {
        definitionItem.Properties.TryGetValue(DefinitionItem.RQNameKey1, out var rqName1);
        definitionItem.Properties.TryGetValue(DefinitionItem.RQNameKey2, out var rqName2);

        return await GetExternalNavigationLocationForSpecificSymbolAsync(definitionItem, rqName1, cancellationToken).ConfigureAwait(false) ??
               await GetExternalNavigationLocationForSpecificSymbolAsync(definitionItem, rqName2, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(string filePath, LinePosition linePosition)?> GetExternalNavigationLocationForSpecificSymbolAsync(
        DefinitionItem definitionItem, string? rqName, CancellationToken cancellationToken)
    {
        if (rqName == null)
            return null;

        var values = await TryGetNavigationAPIRequiredArgumentsAsync(
            definitionItem, rqName, cancellationToken).ConfigureAwait(false);
        if (values is not var (hierarchy, itemID, navigationNotify))
            return null;

        var navigateToTextSpan = new Microsoft.VisualStudio.TextManager.Interop.TextSpan[1];

        await _threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var queryNavigateStatusCode = navigationNotify.QueryNavigateToSymbol(
            hierarchy,
            itemID,
            rqName,
            out var navigateToHierarchy,
            out var navigateToItem,
            navigateToTextSpan,
            out var wouldNavigate);

        if (queryNavigateStatusCode != VSConstants.S_OK || wouldNavigate != 1)
            return null;

        navigateToHierarchy.GetCanonicalName(navigateToItem, out var filePath);
        var lineNumber = navigateToTextSpan[0].iStartLine;
        var charOffset = navigateToTextSpan[0].iStartIndex;

        return (filePath, new LinePosition(lineNumber, charOffset));
    }

    private async Task<(IVsHierarchy hierarchy, uint itemId, IVsSymbolicNavigationNotify navigationNotify)?> TryGetNavigationAPIRequiredArgumentsAsync(
        DefinitionItem definitionItem,
        string? rqName,
        CancellationToken cancellationToken)
    {
        if (rqName == null)
            return null;

        var sourceLocations = definitionItem.SourceSpans;
        if (!sourceLocations.Any())
            return null;

        using var _ = ArrayBuilder<Document>.GetInstance(out var documentsBuilder);
        foreach (var loc in sourceLocations)
            documentsBuilder.AddIfNotNull(loc.Document);

        var documents = documentsBuilder.ToImmutable();

        // We can only pass one itemid to IVsSymbolicNavigationNotify, so prefer itemids from
        // documents we consider to be "generated" to give external language services the best
        // chance of participating.

        var generatedDocuments = documents.WhereAsArray(d => d.IsGeneratedCode(cancellationToken));

        var documentToUse = generatedDocuments.FirstOrDefault() ?? documents.First();

        await _threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (!VisualStudioWorkspaceUtilities.TryGetVsHierarchyAndItemId(documentToUse, out var hierarchy, out var itemID))
            return null;

        if (hierarchy is not IVsSymbolicNavigationNotify navigationNotify)
            return null;

        return (hierarchy, itemID, navigationNotify);
    }
}

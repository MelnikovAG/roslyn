﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.LanguageServer.Handler.SemanticTokens;
using Microsoft.CodeAnalysis.Options;
using Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.ExternalAccess.Razor;

[Method(SemanticRangesMethodName)]
internal sealed class SemanticTokensRangesHandler(
    IGlobalOptionService globalOptions,
    SemanticTokensRefreshQueue semanticTokensRefreshQueue)
    : ILspServiceDocumentRequestHandler<SemanticTokensRangesParams, SemanticTokens>
{
    public const string SemanticRangesMethodName = "roslyn/semanticTokenRanges";

    private readonly IGlobalOptionService _globalOptions = globalOptions;
    private readonly SemanticTokensRefreshQueue _semanticTokenRefreshQueue = semanticTokensRefreshQueue;

    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(SemanticTokensRangesParams request)
    {
        Contract.ThrowIfNull(request.TextDocument);
        return request.TextDocument;
    }

    public async Task<SemanticTokens> HandleRequestAsync(
        SemanticTokensRangesParams request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        Contract.ThrowIfNull(request.TextDocument, "TextDocument is null.");

        var tokensData = await SemanticTokensHelpers.HandleRequestHelperAsync(
            _globalOptions, _semanticTokenRefreshQueue, request.Ranges, context, cancellationToken).ConfigureAwait(false);
        return new SemanticTokens { Data = tokensData };
    }
}

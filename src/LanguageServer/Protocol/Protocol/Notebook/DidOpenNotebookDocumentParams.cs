﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Roslyn.LanguageServer.Protocol;

/// <summary>
/// The params sent in <c>notebookDocument/didOpen</c> notification.
/// <para>
/// See the <see href="https://microsoft.github.io/language-server-protocol/specifications/specification-current/#didOpenNotebookDocumentParams">Language Server Protocol specification</see> for additional information.
/// </para>
/// </summary>
/// <remarks>Since LSP 3.17</remarks>
internal sealed class DidOpenNotebookDocumentParams
{
    /// <summary>
    /// The notebook document that got opened.
    /// </summary>
    [JsonPropertyName("notebookDocument")]
    [JsonRequired]
    public NotebookDocument NotebookDocument { get; init; }

    /// <summary>
    /// The text documents that represent the content of a notebook cell.
    /// </summary>
    [JsonPropertyName("cellTextDocuments")]
    [JsonRequired]
    public TextDocumentItem[] CellTextDocuments { get; init; }
}

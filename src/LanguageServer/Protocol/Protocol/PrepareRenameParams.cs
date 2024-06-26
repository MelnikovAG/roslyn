﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Roslyn.LanguageServer.Protocol
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class representing the parameters for the 'textDocument/prepare' request.
    ///
    /// See the <see href="https://microsoft.github.io/language-server-protocol/specifications/specification-current/#prepareRenameParams">Language Server Protocol specification</see> for additional information.
    /// </summary>
    internal class PrepareRenameParams : ITextDocumentPositionParams
    {
        /// <summary>
        /// Gets or sets the value which identifies the document.
        /// </summary>
        [JsonPropertyName("textDocument")]
        public TextDocumentIdentifier TextDocument
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the position in which the rename is requested.
        /// </summary>
        [JsonPropertyName("position")]
        public Position Position
        {
            get;
            set;
        }
    }
}

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Roslyn.LanguageServer.Protocol;

using System.Text.Json.Serialization;
using Roslyn.Text.Adornments;

/// <summary>
/// Extension class for signature help information which contains colorized label information.
/// </summary>
internal sealed class VSInternalSignatureInformation : SignatureInformation
{
    /// <summary>
    /// Gets or sets the value representing the colorized label.
    /// </summary>
    [JsonPropertyName("_vs_colorizedLabel")]
    [JsonConverter(typeof(ClassifiedTextElementConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClassifiedTextElement? ColorizedLabel
    {
        get;
        set;
    }
}

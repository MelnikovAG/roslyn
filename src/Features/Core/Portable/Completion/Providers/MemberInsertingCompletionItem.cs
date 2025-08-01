﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.LanguageService;

namespace Microsoft.CodeAnalysis.Completion.Providers;

internal sealed class MemberInsertionCompletionItem
{
    public static CompletionItem Create(
        string displayText,
        string displayTextSuffix,
        DeclarationModifiers modifiers,
        int line,
        ISymbol symbol,
        SyntaxToken token,
        int descriptionPosition,
        CompletionItemRules rules)
    {
        return SymbolCompletionItem.CreateWithSymbolId(
            displayText: displayText,
            displayTextSuffix: displayTextSuffix,
            symbols: [symbol],
            contextPosition: descriptionPosition,
            properties: [
                KeyValuePair.Create("Line", line.ToString()),
                KeyValuePair.Create("Modifiers", modifiers.ToString()),
                KeyValuePair.Create("TokenSpanEnd", token.Span.End.ToString())],
            rules: rules,
            isComplexTextEdit: true);
    }

    public static Task<CompletionDescription> GetDescriptionAsync(CompletionItem item, Document document, SymbolDescriptionOptions options, CancellationToken cancellationToken)
        => SymbolCompletionItem.GetDescriptionAsync(item, document, options, cancellationToken);

    public static DeclarationModifiers GetModifiers(CompletionItem item)
    {
        if (item.TryGetProperty("Modifiers", out var text) &&
            DeclarationModifiers.TryParse(text, out var modifiers))
        {
            return modifiers;
        }

        return default;
    }

    public static int GetLine(CompletionItem item)
    {
        if (item.TryGetProperty("Line", out var text)
            && int.TryParse(text, out var number))
        {
            return number;
        }

        return 0;
    }

    public static int GetTokenSpanEnd(CompletionItem item)
    {
        if (item.TryGetProperty("TokenSpanEnd", out var text)
            && int.TryParse(text, out var number))
        {
            return number;
        }

        return 0;
    }
}

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Navigation;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.NavigateTo;

internal readonly record struct OmniSharpNavigateToSearchResult(
    string AdditionalInformation,
    string Kind,
    OmniSharpNavigateToMatchKind MatchKind,
    bool IsCaseSensitive,
    string Name,
    ImmutableArray<TextSpan> NameMatchSpans,
    string SecondarySort,
    string Summary,
    OmniSharpNavigableItem NavigableItem);

internal enum OmniSharpNavigateToMatchKind
{
    Exact = 0,
    Prefix = 1,
    Substring = 2,
    Regular = 3,
    None = 4,
    CamelCaseExact = 5,
    CamelCasePrefix = 6,
    CamelCaseNonContiguousPrefix = 7,
    CamelCaseSubstring = 8,
    CamelCaseNonContiguousSubstring = 9,
    Fuzzy = 10
}

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle.TypeStyle;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.CodeAnalysis.CSharp.Simplification;

[DataContract]
internal sealed record class CSharpSimplifierOptions : SimplifierOptions, IEquatable<CSharpSimplifierOptions>
{
    private static readonly CodeStyleOption2<PreferBracesPreference> s_defaultPreferBraces =
        new(PreferBracesPreference.Always, NotificationOption2.Silent);

    public static readonly CSharpSimplifierOptions Default = new();

    [DataMember] public CodeStyleOption2<bool> VarForBuiltInTypes { get; init; } = CodeStyleOption2.FalseWithSilentEnforcement;
    [DataMember] public CodeStyleOption2<bool> VarWhenTypeIsApparent { get; init; } = CodeStyleOption2.FalseWithSilentEnforcement;
    [DataMember] public CodeStyleOption2<bool> VarElsewhere { get; init; } = CodeStyleOption2.FalseWithSilentEnforcement;
    [DataMember] public CodeStyleOption2<bool> PreferSimpleDefaultExpression { get; init; } = CodeStyleOption2.TrueWithSuggestionEnforcement;
    [DataMember] public CodeStyleOption2<bool> PreferImplicitlyTypedLambdaExpression { get; init; } = CodeStyleOption2.TrueWithSuggestionEnforcement;
    [DataMember] public CodeStyleOption2<bool> PreferParameterNullChecking { get; init; } = CodeStyleOption2.TrueWithSuggestionEnforcement;
    [DataMember] public CodeStyleOption2<bool> AllowEmbeddedStatementsOnSameLine { get; init; } = CodeStyleOption2.TrueWithSilentEnforcement;
    [DataMember] public CodeStyleOption2<PreferBracesPreference> PreferBraces { get; init; } = s_defaultPreferBraces;
    [DataMember] public CodeStyleOption2<bool> PreferThrowExpression { get; init; } = CodeStyleOption2.TrueWithSuggestionEnforcement;
    [DataMember] public CodeStyleOption2<bool> ImplicitObjectCreationWhenTypeIsApparent { get; init; } = CodeStyleOption2.FalseWithSilentEnforcement;

    public CSharpSimplifierOptions()
    {
    }

    public CSharpSimplifierOptions(IOptionsReader options)
        : base(options, LanguageNames.CSharp)
    {
        VarForBuiltInTypes = options.GetOption(CSharpCodeStyleOptions.VarForBuiltInTypes);
        VarWhenTypeIsApparent = options.GetOption(CSharpCodeStyleOptions.VarWhenTypeIsApparent);
        VarElsewhere = options.GetOption(CSharpCodeStyleOptions.VarElsewhere);
        PreferSimpleDefaultExpression = options.GetOption(CSharpCodeStyleOptions.PreferSimpleDefaultExpression);
        PreferImplicitlyTypedLambdaExpression = options.GetOption(CSharpCodeStyleOptions.PreferImplicitlyTypedLambdaExpression);
        AllowEmbeddedStatementsOnSameLine = options.GetOption(CSharpCodeStyleOptions.AllowEmbeddedStatementsOnSameLine);
        PreferBraces = options.GetOption(CSharpCodeStyleOptions.PreferBraces);
        PreferThrowExpression = options.GetOption(CSharpCodeStyleOptions.PreferThrowExpression);
        ImplicitObjectCreationWhenTypeIsApparent = options.GetOption(CSharpCodeStyleOptions.ImplicitObjectCreationWhenTypeIsApparent);
    }

    public UseVarPreference GetUseVarPreference()
    {
        var styleForIntrinsicTypes = this.VarForBuiltInTypes;
        var styleForApparent = this.VarWhenTypeIsApparent;
        var styleForElsewhere = this.VarElsewhere;

        var stylePreferences = UseVarPreference.None;

        if (styleForIntrinsicTypes.Value)
            stylePreferences |= UseVarPreference.ForBuiltInTypes;

        if (styleForApparent.Value)
            stylePreferences |= UseVarPreference.WhenTypeIsApparent;

        if (styleForElsewhere.Value)
            stylePreferences |= UseVarPreference.Elsewhere;

        return stylePreferences;
    }
}

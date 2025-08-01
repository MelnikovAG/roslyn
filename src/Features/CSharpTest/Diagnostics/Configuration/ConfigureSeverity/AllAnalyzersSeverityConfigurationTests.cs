﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeFixes.Configuration.ConfigureSeverity;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.UnitTests.Diagnostics;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics.Configuration.ConfigureSeverity;

public abstract partial class AllAnalyzersSeverityConfigurationTests : AbstractSuppressionDiagnosticTest_NoEditor
{
    private sealed class CustomDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: "XYZ0001",
            title: "Title",
            messageFormat: "Message",
            category: "CustomCategory",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c => c.ReportDiagnostic(Diagnostic.Create(Rule, c.Node.GetLocation())),
                SyntaxKind.ClassDeclaration);
        }
    }

    protected internal override string GetLanguage() => LanguageNames.CSharp;

    protected override ParseOptions GetScriptOptions() => Options.Script;

    internal override Tuple<DiagnosticAnalyzer, IConfigurationFixProvider> CreateDiagnosticProviderAndFixer(Workspace workspace)
    {
        return new Tuple<DiagnosticAnalyzer, IConfigurationFixProvider>(
                    new CustomDiagnosticAnalyzer(), new ConfigureSeverityLevelCodeFixProvider());
    }

    [Trait(Traits.Feature, Traits.Features.CodeActionsConfiguration)]
    public sealed class SilentConfigurationTests : AllAnalyzersSeverityConfigurationTests
    {
        /// <summary>
        /// Code action ranges:
        ///     1. (0 - 4) => Code actions for diagnostic "ID" configuration with severity None, Silent, Suggestion, Warning and Error
        ///     2. (5 - 9) => Code actions for diagnostic "Category" configuration with severity None, Silent, Suggestion, Warning and Error
        ///     3. (10 - 14) => Code actions for all analyzer diagnostics configuration with severity None, Silent, Suggestion, Warning and Error
        /// </summary>
        protected override int CodeActionIndex => 11;

        [ConditionalFact(typeof(IsEnglishLocal))]
        public Task ConfigureEditorconfig_Empty()
            => TestInRegularAndScriptAsync("""
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig"></AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, """
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                         <Document FilePath="z:\\file.cs">
                class Program1 { }
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.cs]

                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = silent
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, CodeActionIndex);

        [Fact]
        public Task ConfigureEditorconfig_RuleExists()
            => TestInRegularAndScriptAsync("""
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.cs]
                dotnet_analyzer_diagnostic.severity = suggestion   # Comment
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, """
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                         <Document FilePath="z:\\file.cs">
                class Program1 { }
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.cs]
                dotnet_analyzer_diagnostic.severity = silent   # Comment
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, CodeActionIndex);

        [Fact]
        public Task ConfigureEditorconfig_RuleIdEntryExists()
            => TestInRegularAndScriptAsync("""
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.cs]
                dotnet_diagnostic.XYZ0001.severity = suggestion   # Comment1
                dotnet_diagnostic.category-CustomCategory.severity = warning   # Comment2
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, """
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                         <Document FilePath="z:\\file.cs">
                class Program1 { }
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.cs]
                dotnet_diagnostic.XYZ0001.severity = suggestion   # Comment1
                dotnet_diagnostic.category-CustomCategory.severity = warning   # Comment2

                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = silent
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, CodeActionIndex);

        [ConditionalFact(typeof(IsEnglishLocal))]
        public Task ConfigureEditorconfig_InvalidHeader()
            => TestInRegularAndScriptAsync("""
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.vb]
                dotnet_analyzer_diagnostic.severity = suggestion
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, """
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                class Program1 { }
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.vb]
                dotnet_analyzer_diagnostic.severity = suggestion

                [*.cs]

                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = silent
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, CodeActionIndex);

        [Fact]
        public async Task ConfigureEditorconfig_MaintainExistingEntry()
        {
            var input = """
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.{vb,cs}]
                dotnet_analyzer_diagnostic.severity = silent
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """;

            await TestInRegularAndScriptAsync(input, input, CodeActionIndex);
        }

        [Fact]
        public Task ConfigureEditorconfig_DiagnosticsSuppressed()
            => TestMissingInRegularAndScriptAsync("""
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.{vb,cs}]
                dotnet_analyzer_diagnostic.severity = none
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """);

        [ConditionalFact(typeof(IsEnglishLocal))]
        public Task ConfigureEditorconfig_InvalidRule()
            => TestInRegularAndScriptAsync("""
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.{vb,cs}]
                dotnet_analyzer_diagnostic.XYZ1111.severity = suggestion
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, """
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*.{vb,cs}]
                dotnet_analyzer_diagnostic.XYZ1111.severity = suggestion

                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = silent
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, CodeActionIndex);

        [ConditionalFact(typeof(IsEnglishLocal))]
        public Task ConfigureEditorconfig_RegexHeaderMatch()
            => TestInRegularAndScriptAsync("""
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\Program/file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*am/fi*e.cs]
                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = warning
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, """
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                         <Document FilePath="z:\\Program/file.cs">
                class Program1 { }
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*am/fi*e.cs]
                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = warning

                [*.cs]

                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = silent
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, CodeActionIndex);

        [ConditionalFact(typeof(IsEnglishLocal))]
        public Task ConfigureEditorconfig_RegexHeaderNonMatch()
            => TestInRegularAndScriptAsync("""
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                        <Document FilePath="z:\\Program/file.cs">
                [|class Program1 { }|]
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*am/fii*e.cs]
                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = warning
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, """
                <Workspace>
                    <Project Language="C#" AssemblyName="Assembly1" CommonReferences="true" FilePath="z:\\Assembly1.csproj">
                         <Document FilePath="z:\\Program/file.cs">
                class Program1 { }
                        </Document>
                        <AnalyzerConfigDocument FilePath="z:\\.editorconfig">[*am/fii*e.cs]
                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = warning

                [*.cs]

                # Default severity for all analyzer diagnostics
                dotnet_analyzer_diagnostic.severity = silent
                </AnalyzerConfigDocument>
                    </Project>
                </Workspace>
                """, CodeActionIndex);
    }
}

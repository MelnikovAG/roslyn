﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Runtime.CompilerServices
Imports Microsoft.CodeAnalysis.Contracts.EditAndContinue
Imports Microsoft.CodeAnalysis.EditAndContinue
Imports Microsoft.CodeAnalysis.EditAndContinue.UnitTests
Imports Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.EditAndContinue

Namespace Microsoft.CodeAnalysis.VisualBasic.EditAndContinue.UnitTests

    Friend Module EditAndContinueValidation
        <Extension>
        Friend Sub VerifyLineEdits(
            editScript As EditScriptDescription,
            lineEdits As SourceLineUpdate(),
            Optional semanticEdits As SemanticEditDescription() = Nothing,
            Optional diagnostics As RudeEditDiagnosticDescription() = Nothing,
            Optional capabilities As EditAndContinueCapabilities? = Nothing)

            Assert.NotEmpty(lineEdits)

            VerifyLineEdits(
                editScript,
                {New SequencePointUpdates(editScript.Match.OldRoot.SyntaxTree.FilePath, lineEdits.ToImmutableArray())},
                semanticEdits,
                diagnostics,
                capabilities)
        End Sub

        <Extension>
        Friend Sub VerifyLineEdits(
            editScript As EditScriptDescription,
            lineEdits As SequencePointUpdates(),
            Optional semanticEdits As SemanticEditDescription() = Nothing,
            Optional diagnostics As RudeEditDiagnosticDescription() = Nothing,
            Optional capabilities As EditAndContinueCapabilities? = Nothing)

            Dim validator = New VisualBasicEditAndContinueTestVerifier()
            validator.VerifyLineEdits(editScript, lineEdits, semanticEdits, diagnostics, capabilities)
        End Sub

        <Extension>
        Friend Sub VerifySemanticDiagnostics(
            editScript As EditScriptDescription,
            ParamArray diagnostics As RudeEditDiagnosticDescription())

            VerifySemanticDiagnostics(editScript, activeStatements:=Nothing, targetFrameworks:=Nothing, capabilities:=Nothing, diagnostics)
        End Sub

        <Extension>
        Friend Sub VerifySemanticDiagnostics(
            editScript As EditScriptDescription,
            activeStatements As ActiveStatementsDescription,
            ParamArray diagnostics As RudeEditDiagnosticDescription())

            VerifySemanticDiagnostics(editScript, activeStatements, targetFrameworks:=Nothing, capabilities:=Nothing, diagnostics)
        End Sub

        <Extension>
        Friend Sub VerifySemanticDiagnostics(
            editScript As EditScriptDescription,
            diagnostics As RudeEditDiagnosticDescription(),
            capabilities As EditAndContinueCapabilities?)

            VerifySemanticDiagnostics(editScript, activeStatements:=Nothing, targetFrameworks:=Nothing, capabilities, diagnostics)
        End Sub

        <Extension>
        Friend Sub VerifySemanticDiagnostics(
            editScript As EditScriptDescription,
            Optional activeStatements As ActiveStatementsDescription = Nothing,
            Optional targetFrameworks As TargetFramework() = Nothing,
            Optional capabilities As EditAndContinueCapabilities? = Nothing,
            Optional diagnostics As RudeEditDiagnosticDescription() = Nothing)

            VerifySemantics(
                {editScript},
                {New DocumentAnalysisResultsDescription(activeStatements:=activeStatements, diagnostics:=If(diagnostics, Array.Empty(Of RudeEditDiagnosticDescription)))},
                targetFrameworks,
                capabilities)
        End Sub

        <Extension>
        Friend Sub VerifySemantics(
            editScript As EditScriptDescription,
            Optional activeStatements As ActiveStatementsDescription = Nothing,
            Optional semanticEdits As SemanticEditDescription() = Nothing,
            Optional diagnostics As RudeEditDiagnosticDescription() = Nothing,
            Optional targetFrameworks As TargetFramework() = Nothing,
            Optional capabilities As EditAndContinueCapabilities? = Nothing)

            VerifySemantics(
                {editScript},
                {New DocumentAnalysisResultsDescription(activeStatements, semanticEdits, lineEdits:=Nothing, diagnostics)},
                targetFrameworks,
                capabilities)
        End Sub

        <Extension>
        Friend Sub VerifySemantics(
            editScripts As EditScriptDescription(),
            expected As DocumentAnalysisResultsDescription(),
            Optional targetFrameworks As TargetFramework() = Nothing,
            Optional capabilities As EditAndContinueCapabilities? = Nothing)

            For Each framework In If(targetFrameworks, {TargetFramework.NetStandard20, TargetFramework.NetCoreApp})
                Dim validator = New VisualBasicEditAndContinueTestVerifier()
                validator.VerifySemantics(editScripts, framework, expected, capabilities)
            Next
        End Sub

        <Extension>
        Friend Sub VerifySemantics(
            editScript As EditScriptDescription,
            semanticEdits As SemanticEditDescription(),
            capabilities As EditAndContinueCapabilities)

            VerifySemantics(editScript, ActiveStatementsDescription.Empty, semanticEdits, capabilities:=capabilities)
        End Sub

        <Extension>
        Friend Sub VerifySemantics(
            editScript As EditScriptDescription,
            ParamArray semanticEdits As SemanticEditDescription())

            VerifySemantics(editScript, ActiveStatementsDescription.Empty, semanticEdits)
        End Sub
    End Module
End Namespace

﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.LanguageService
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.LanguageService
    Friend Class VisualBasicBlockFacts
        Inherits AbstractBlockFacts(Of StatementSyntax)

        Public Shared ReadOnly Instance As New VisualBasicBlockFacts()

        Public Overrides Function IsScopeBlock(node As SyntaxNode) As Boolean
            ' VB has no equivalent of curly braces.
            Return False
        End Function

        Public Overrides Function IsExecutableBlock(node As SyntaxNode) As Boolean
            Return node.IsExecutableBlock()
        End Function

        Public Overrides Function GetImmediateParentExecutableBlockForStatement(statement As StatementSyntax) As SyntaxNode
            Return If(IsExecutableBlock(statement.Parent), statement.Parent, Nothing)
        End Function

        Public Overrides Function GetExecutableBlockStatements(node As SyntaxNode) As IReadOnlyList(Of StatementSyntax)
            Return node.GetExecutableBlockStatements()
        End Function

        Public Overrides Function FindInnermostCommonExecutableBlock(nodes As IEnumerable(Of SyntaxNode)) As SyntaxNode
            Return nodes.FindInnermostCommonExecutableBlock()
        End Function

        Public Overrides Function IsStatementContainer(node As SyntaxNode) As Boolean
            Return IsExecutableBlock(node)
        End Function

        Public Overrides Function GetStatementContainerStatements(node As SyntaxNode) As IReadOnlyList(Of StatementSyntax)
            Return GetExecutableBlockStatements(node)
        End Function
    End Class
End Namespace

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.LanguageService;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.LanguageService;

internal class CSharpBlockFacts : AbstractBlockFacts<StatementSyntax>
{
    public static readonly CSharpBlockFacts Instance = new();

    public override bool IsScopeBlock([NotNullWhen(true)] SyntaxNode? node)
        => node.IsKind(SyntaxKind.Block);

    public override bool IsExecutableBlock([NotNullWhen(true)] SyntaxNode? node)
        => node is (kind: SyntaxKind.Block or SyntaxKind.SwitchSection or SyntaxKind.CompilationUnit);

    public override SyntaxNode? GetImmediateParentExecutableBlockForStatement(StatementSyntax statement)
        => statement.Parent switch
        {
            BlockSyntax block => block,
            SwitchSectionSyntax switchSection => switchSection,
            GlobalStatementSyntax globalStatement => globalStatement.Parent,
            _ => null,
        };

    public override IReadOnlyList<StatementSyntax> GetExecutableBlockStatements(SyntaxNode? node)
    {
        return node switch
        {
            BlockSyntax block => block.Statements,
            SwitchSectionSyntax switchSection => switchSection.Statements,
            CompilationUnitSyntax compilationUnit => compilationUnit.Members.OfType<GlobalStatementSyntax>().SelectAsArray(globalStatement => globalStatement.Statement),
            _ => throw ExceptionUtilities.UnexpectedValue(node),
        };
    }

    public override SyntaxNode? FindInnermostCommonExecutableBlock(IEnumerable<SyntaxNode> nodes)
        => nodes.FindInnermostCommonNode(IsExecutableBlock);

    public override bool IsStatementContainer([NotNullWhen(true)] SyntaxNode? node)
        => IsExecutableBlock(node) || node.IsEmbeddedStatementOwner();

    public override IReadOnlyList<StatementSyntax> GetStatementContainerStatements(SyntaxNode? node)
    {
        if (IsExecutableBlock(node))
            return GetExecutableBlockStatements(node);
        else if (node.GetEmbeddedStatement() is { } embeddedStatement)
            return [embeddedStatement];
        else
            return [];
    }
}

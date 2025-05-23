﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public sealed class IncrementalParsingTests(ITestOutputHelper output) : ParsingTests(output)
    {
        private CSharpParseOptions GetOptions(string[] defines)
        {
            return new CSharpParseOptions(languageVersion: LanguageVersion.CSharp3, preprocessorSymbols: defines);
        }

        private SyntaxTree Parse(string text, params string[] defines)
        {
            var options = this.GetOptions(defines);
            var itext = SourceText.From(text);
            return SyntaxFactory.ParseSyntaxTree(itext, options);
        }

        private SyntaxTree Parse(string text, LanguageVersion languageVersion)
        {
            var options = new CSharpParseOptions(languageVersion: languageVersion);
            var itext = SourceText.From(text);
            return SyntaxFactory.ParseSyntaxTree(itext, options);
        }

        private SyntaxTree Parse6(string text)
            => Parse(text, LanguageVersion.CSharp6);

        private SyntaxTree ParsePreview(string text)
            => Parse(text, LanguageVersion.Preview);

        [Fact]
        public void TestChangeClassNameWithNonMatchingMethod()
        {
            var text = "class goo { void m() { } }";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("goo", "bar");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.IdentifierToken);
        }

        [Fact]
        public void TestExclamationExclamation()
        {
            var text = @"#nullable enable

public class C {
    public void M(string? x  !!) {
    }
}";
            var oldTree = this.ParsePreview(text);
            var newTree = oldTree.WithReplaceFirst("?", "");
            oldTree.GetDiagnostics().Verify(
                // (4,30): error CS1003: Syntax error, ',' expected
                //     public void M(string? x  !!) {
                Diagnostic(ErrorCode.ERR_SyntaxError, "!").WithArguments(",").WithLocation(4, 30));
            newTree.GetDiagnostics().Verify(
                // (4,29): error CS1003: Syntax error, ',' expected
                //     public void M(string x  !!) {
                Diagnostic(ErrorCode.ERR_SyntaxError, "!").WithArguments(",").WithLocation(4, 29));

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.MethodDeclaration,
                            SyntaxKind.ParameterList,
                            SyntaxKind.Parameter,
                            SyntaxKind.PredefinedType,
                            SyntaxKind.StringKeyword,
                            SyntaxKind.IdentifierToken);
        }

        [Fact]
        public void TestChangeClassNameToNotMatchConstructor()
        {
            var text = "class goo { goo() { } }";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("goo", "bar");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.IdentifierToken);
        }

        private static void TestDiffsInOrder(ImmutableArray<SyntaxNodeOrToken> diffs, params SyntaxKind[] expectedKinds)
        {
            if (diffs.Length != expectedKinds.Length)
            {
                Assert.Fail(getMessage());
            }

            for (int i = 0; i < diffs.Length; i++)
            {
                if (!diffs[i].IsKind(expectedKinds[i]))
                {
                    Assert.Fail(getMessage());
                }
            }

            string getMessage()
            {
                var builder = PooledStringBuilder.GetInstance();
                builder.Builder.AppendLine("Actual:");
                foreach (var diff in diffs)
                {
                    builder.Builder.AppendLine($"SyntaxKind.{diff.Kind()},");
                }

                return builder.ToStringAndFree();
            }
        }

        [Fact]
        public void TestChangeClassNameToMatchConstructor()
        {
            var text = "class goo { bar() { } }";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("goo", "bar");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.IdentifierToken);
        }

        [Fact]
        public void TestChangeClassNameToNotMatchDestructor()
        {
            var text = "class goo { ~goo() { } }";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("goo", "bar");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.IdentifierToken);
        }

        [Fact]
        public void TestChangeClassNameToMatchDestructor()
        {
            var text = "class goo { ~bar() { } }";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("goo", "bar");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.IdentifierToken);
        }

        [Fact]
        public void TestChangeFromClassToInterface()
        {
            var interfaceKeyword = SyntaxFactory.ParseToken("interface"); // prime the memoizer

            var text = "class goo { public void m() { } }";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("class", "interface");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.InterfaceDeclaration,
                            SyntaxKind.InterfaceKeyword);
        }

        [Fact]
        public void TestChangeFromClassToStruct()
        {
            var interfaceKeyword = SyntaxFactory.ParseToken("struct"); // prime the memoizer

            var text = "class goo { public void m() { } }";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("class", "struct");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.StructDeclaration,
                            SyntaxKind.StructKeyword);
        }

        [Fact]
        public void TestChangeMethodName()
        {
            var text = "class c { void goo(a x, b y) { } }";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("goo", "bar");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.MethodDeclaration,
                            SyntaxKind.IdentifierToken);
        }

        [Fact]
        public void TestChangeIfCondition()
        {
            var text = @"
#if GOO
class goo { void M() { } }
#endif
";
            var oldTree = this.Parse(text, "GOO", "BAR");
            var newTree = oldTree.WithReplaceFirst("GOO", "BAR");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassKeyword,
                            SyntaxKind.EndOfFileToken);
        }

        [Fact]
        public void TestChangeDefine()
        {
            var text = @"
#define GOO
#if GOO||BAR
class goo { void M() { } }
#endif
";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("GOO", "BAR");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassKeyword,
                            SyntaxKind.EndOfFileToken);
        }

        [Fact]
        public void TestChangeDefineAndIfElse()
        {
            var text = @"
#define GOO
#if GOO
class C { void M() { } }
#else
class C { void N() { } }
#endif
";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithReplaceFirst("GOO", "BAR");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassKeyword,
                            SyntaxKind.MethodDeclaration,
                            SyntaxKind.IdentifierToken,
                            SyntaxKind.Block,
                            SyntaxKind.EndOfFileToken);
        }

        [Fact]
        public void TestAddLineDirective()
        {
            var text = @"
class C { void M() { } }
";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithInsertAt(0, "#line 100\r\n");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassKeyword);
        }

        [Fact]
        public void TestRemoveLineDirective()
        {
            var text = @"
#line 10
class C { void M() { } }
";
            var oldTree = this.Parse(text);
            var newTree = oldTree.WithRemoveFirst("#line 10");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassKeyword);
        }

        [Fact]
        public void TestRemoveEndRegionDirective()
        {
            var text = @"
#if true
class A { void a() { } }
#region
class B { void b() { } }
#endregion
class C { void c() { } }
#endif
";
            var oldTree = this.Parse(text);
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            var oldDirectives = oldTree.GetCompilationUnitRoot().GetDirectives();
            Assert.Equal(4, oldDirectives.Count);
            Assert.Equal(SyntaxKind.IfDirectiveTrivia, oldDirectives[0].Kind());
            Assert.Equal(SyntaxKind.RegionDirectiveTrivia, oldDirectives[1].Kind());
            Assert.Equal(SyntaxKind.EndRegionDirectiveTrivia, oldDirectives[2].Kind());
            Assert.Equal(SyntaxKind.EndIfDirectiveTrivia, oldDirectives[3].Kind());

            var newTree = oldTree.WithRemoveFirst("#endregion");
            var errors = newTree.GetCompilationUnitRoot().Errors();
            Assert.Equal(2, errors.Length);
            Assert.Equal((int)ErrorCode.ERR_EndRegionDirectiveExpected, errors[0].Code);
            Assert.Equal((int)ErrorCode.ERR_EndRegionDirectiveExpected, errors[1].Code);
            var newDirectives = newTree.GetCompilationUnitRoot().GetDirectives();
            Assert.Equal(3, newDirectives.Count);
            Assert.Equal(SyntaxKind.IfDirectiveTrivia, newDirectives[0].Kind());
            Assert.Equal(SyntaxKind.RegionDirectiveTrivia, newDirectives[1].Kind());
            Assert.Equal(SyntaxKind.BadDirectiveTrivia, newDirectives[2].Kind());

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,  // class declaration on edge before change
                            SyntaxKind.MethodDeclaration,
                            SyntaxKind.Block,
                            SyntaxKind.ClassDeclaration,  // class declaration on edge after change
                            SyntaxKind.ClassKeyword,      // edge of change and directives different
                            SyntaxKind.EndOfFileToken);    // directives different (endif becomes bad-directive)
        }

        [Fact]
        public void TestAddEndRegionDirective()
        {
            var text = @"
#if true
class A { void a() { } }
#region
class B { void b() { } }
class C { void c() { } }
#endif
";
            var oldTree = this.Parse(text);
            var errors = oldTree.GetCompilationUnitRoot().Errors();
            Assert.Equal(2, errors.Length);
            Assert.Equal((int)ErrorCode.ERR_EndRegionDirectiveExpected, errors[0].Code);
            Assert.Equal((int)ErrorCode.ERR_EndRegionDirectiveExpected, errors[1].Code);
            var oldDirectives = oldTree.GetCompilationUnitRoot().GetDirectives();
            Assert.Equal(3, oldDirectives.Count);
            Assert.Equal(SyntaxKind.IfDirectiveTrivia, oldDirectives[0].Kind());
            Assert.Equal(SyntaxKind.RegionDirectiveTrivia, oldDirectives[1].Kind());
            Assert.Equal(SyntaxKind.BadDirectiveTrivia, oldDirectives[2].Kind());

            var newTree = oldTree.WithInsertBefore("class C", "#endregion\r\n");
            errors = newTree.GetCompilationUnitRoot().Errors();
            Assert.Equal(0, errors.Length);
            var newDirectives = newTree.GetCompilationUnitRoot().GetDirectives();
            Assert.Equal(4, newDirectives.Count);
            Assert.Equal(SyntaxKind.IfDirectiveTrivia, newDirectives[0].Kind());
            Assert.Equal(SyntaxKind.RegionDirectiveTrivia, newDirectives[1].Kind());
            Assert.Equal(SyntaxKind.EndRegionDirectiveTrivia, newDirectives[2].Kind());
            Assert.Equal(SyntaxKind.EndIfDirectiveTrivia, newDirectives[3].Kind());

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,  // class declaration on edge before change
                            SyntaxKind.MethodDeclaration,
                            SyntaxKind.Block,
                            SyntaxKind.ClassDeclaration,  // class declaration on edge after change
                            SyntaxKind.ClassKeyword,      // edge of change and directives different
                            SyntaxKind.EndOfFileToken);    // directives different (endif becomes bad-directive)
        }

        [Fact]
        public void TestGlobalStatementToStatementChange()
        {
            var text = @";a * b";

            var oldTree = SyntaxFactory.ParseSyntaxTree(text, options: TestOptions.Script);
            var newTree = oldTree.WithInsertAt(0, "{ ");

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.GlobalStatement,
                            SyntaxKind.Block,
                            SyntaxKind.OpenBraceToken,
                            SyntaxKind.LocalDeclarationStatement,
                            SyntaxKind.VariableDeclaration,
                            SyntaxKind.PointerType,
                            SyntaxKind.VariableDeclarator,
                            SyntaxKind.SemicolonToken,       // missing
                            SyntaxKind.CloseBraceToken);      // missing
        }

        [Fact]
        public void TestStatementToGlobalStatementChange()
        {
            var text = @"{; a * b; }";

            var oldTree = SyntaxFactory.ParseSyntaxTree(text, options: TestOptions.Script);
            var newTree = oldTree.WithRemoveAt(0, 1);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.GlobalStatement,
                            SyntaxKind.GlobalStatement,
                            SyntaxKind.ExpressionStatement,
                            SyntaxKind.MultiplyExpression,
                            SyntaxKind.IdentifierName,
                            SyntaxKind.SemicolonToken);
        }

        [Fact]
        public void TestAttributeToCollectionExpression1()
        {
            var source = @"
using System;

class C
{
    void M()
    {
        [A] Method();
    }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);

            Assert.True(tree.GetRoot().DescendantNodesAndSelf().Any(n => n is AttributeSyntax));
            Assert.False(tree.GetRoot().DescendantNodesAndSelf().Any(n => n is CollectionExpressionSyntax));

            var text = tree.GetText();
            var span = new TextSpan(source.IndexOf("]") + 1, length: 1);
            var change = new TextChange(span, ".");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());

            Assert.False(tree.GetRoot().DescendantNodesAndSelf().Any(n => n is AttributeSyntax));
            Assert.True(tree.GetRoot().DescendantNodesAndSelf().Any(n => n is CollectionExpressionSyntax));

            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void TestCollectionExpressionToAttribute1()
        {
            var source = @"
using System;

class C
{
    void M()
    {
        [A].Method();
    }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);

            Assert.False(tree.GetRoot().DescendantNodesAndSelf().Any(n => n is AttributeSyntax));
            Assert.True(tree.GetRoot().DescendantNodesAndSelf().Any(n => n is CollectionExpressionSyntax));

            var text = tree.GetText();
            var span = new TextSpan(source.IndexOf("."), length: 1);
            var change = new TextChange(span, " ");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());

            Assert.True(tree.GetRoot().DescendantNodesAndSelf().Any(n => n is AttributeSyntax));
            Assert.False(tree.GetRoot().DescendantNodesAndSelf().Any(n => n is CollectionExpressionSyntax));

            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void TestLocalFunctionCollectionVsAccessParsing()
        {
            var source = """
                using System;

                class C
                {
                    void M()
                    {
                        var v = a ? b?[() =>
                            {
                                var v = whatever();
                                int LocalFunc()
                                {
                                    var v = a ? [b] : c;
                                }
                                var v = whatever();
                            }] : d;
                    }
                }
                """;
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            Assert.Empty(tree.GetDiagnostics());

            var localFunc1 = tree.GetRoot().DescendantNodesAndSelf().Single(n => n is LocalFunctionStatementSyntax);
            var innerConditionalExpr1 = localFunc1.DescendantNodesAndSelf().Single(n => n is ConditionalExpressionSyntax);

            var text = tree.GetText();

            var prefix = "var v = a ? b?[() =>";
            var suffix = "] : d;";

            var prefixSpan = new TextSpan(source.IndexOf(prefix), length: prefix.Length);
            var suffixSpan = new TextSpan(source.IndexOf(suffix), length: suffix.Length);
            text = text.WithChanges(new TextChange(prefixSpan, ""), new TextChange(suffixSpan, ""));
            tree = tree.WithChangedText(text);
            Assert.Empty(tree.GetDiagnostics());

            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            Assert.Empty(fullTree.GetDiagnostics());

            var localFunc2 = tree.GetRoot().DescendantNodesAndSelf().Single(n => n is LocalFunctionStatementSyntax);
            var innerConditionalExpr2 = localFunc2.DescendantNodesAndSelf().Single(n => n is ConditionalExpressionSyntax);

            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/74456")]
        public void TestCollectionExpressionSpreadVsDeletingTopLevelBrace()
        {
            const string valueSetterLine = "x[1] = 312;";
            var initialSource = $$"""
                public class Program
                {
                    public void M2()
                    {
                        if (true)
                        {
                            {
                                if (true)
                                {
                                    {{valueSetterLine}}
                                }
                            }
                        }
                        if (true)
                        {
                            y = [.. z];
                        }
                    }
                }
                """;
            var initialTree = SyntaxFactory.ParseSyntaxTree(initialSource);

            // Initial code is fully legal and should have no parse errors.
            Assert.Empty(initialTree.GetDiagnostics());

            // Delete '{' (and end of line) before 'values[1] = 312;'
            var initialText = initialTree.GetText();
            var valueSetterLinePosition = initialSource.IndexOf(valueSetterLine);
            var initialLines = initialText.Lines;

            int valueSetterLineIndex = initialLines.IndexOf(valueSetterLinePosition);
            var openBraceLine = initialText.Lines[valueSetterLineIndex - 1];
            Assert.EndsWith("{", openBraceLine.ToString());

            var withOpenBraceDeletedText = initialText.WithChanges(new TextChange(openBraceLine.SpanIncludingLineBreak, ""));
            var withOpenBraceDeletedTree = initialTree.WithChangedText(withOpenBraceDeletedText);

            // Deletion of the open brace causes the method body to close early with the close brace before the `if`
            // statement.  This will lead to a ton of cascading errors for what follows.  In particular, the `[.. values]`
            // will be parsed as a very broken attribute on an incomplete member.
            {
                UsingTree(withOpenBraceDeletedTree,
                    // (13,9): error CS1519: Invalid token 'if' in class, record, struct, or interface member declaration
                    //         if (true)
                    Diagnostic(ErrorCode.ERR_InvalidMemberDecl, "if").WithArguments("if").WithLocation(13, 9),
                    // (13,13): error CS1031: Type expected
                    //         if (true)
                    Diagnostic(ErrorCode.ERR_TypeExpected, "true").WithLocation(13, 13),
                    // (13,13): error CS8124: Tuple must contain at least two elements.
                    //         if (true)
                    Diagnostic(ErrorCode.ERR_TupleTooFewElements, "true").WithLocation(13, 13),
                    // (13,13): error CS1026: ) expected
                    //         if (true)
                    Diagnostic(ErrorCode.ERR_CloseParenExpected, "true").WithLocation(13, 13),
                    // (13,13): error CS1519: Invalid token 'true' in class, record, struct, or interface member declaration
                    //         if (true)
                    Diagnostic(ErrorCode.ERR_InvalidMemberDecl, "true").WithArguments("true").WithLocation(13, 13),
                    // (15,15): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
                    //             y = [.. z];
                    Diagnostic(ErrorCode.ERR_InvalidMemberDecl, "=").WithArguments("=").WithLocation(15, 15),
                    // (15,15): error CS1519: Invalid token '=' in class, record, struct, or interface member declaration
                    //             y = [.. z];
                    Diagnostic(ErrorCode.ERR_InvalidMemberDecl, "=").WithArguments("=").WithLocation(15, 15),
                    // (15,18): error CS1001: Identifier expected
                    //             y = [.. z];
                    Diagnostic(ErrorCode.ERR_IdentifierExpected, ".").WithLocation(15, 18),
                    // (15,19): error CS1001: Identifier expected
                    //             y = [.. z];
                    Diagnostic(ErrorCode.ERR_IdentifierExpected, ".").WithLocation(15, 19),
                    // (15,23): error CS1519: Invalid token ';' in class, record, struct, or interface member declaration
                    //             y = [.. z];
                    Diagnostic(ErrorCode.ERR_InvalidMemberDecl, ";").WithArguments(";").WithLocation(15, 23),
                    // (17,5): error CS1022: Type or namespace definition, or end-of-file expected
                    //     }
                    Diagnostic(ErrorCode.ERR_EOFExpected, "}").WithLocation(17, 5),
                    // (18,1): error CS1022: Type or namespace definition, or end-of-file expected
                    // }
                    Diagnostic(ErrorCode.ERR_EOFExpected, "}").WithLocation(18, 1));

                N(SyntaxKind.CompilationUnit);
                {
                    N(SyntaxKind.ClassDeclaration);
                    {
                        N(SyntaxKind.PublicKeyword);
                        N(SyntaxKind.ClassKeyword);
                        N(SyntaxKind.IdentifierToken, "Program");
                        N(SyntaxKind.OpenBraceToken);
                        N(SyntaxKind.MethodDeclaration);
                        {
                            N(SyntaxKind.PublicKeyword);
                            N(SyntaxKind.PredefinedType);
                            {
                                N(SyntaxKind.VoidKeyword);
                            }
                            N(SyntaxKind.IdentifierToken, "M2");
                            N(SyntaxKind.ParameterList);
                            {
                                N(SyntaxKind.OpenParenToken);
                                N(SyntaxKind.CloseParenToken);
                            }
                            N(SyntaxKind.Block);
                            {
                                N(SyntaxKind.OpenBraceToken);
                                N(SyntaxKind.IfStatement);
                                {
                                    N(SyntaxKind.IfKeyword);
                                    N(SyntaxKind.OpenParenToken);
                                    N(SyntaxKind.TrueLiteralExpression);
                                    {
                                        N(SyntaxKind.TrueKeyword);
                                    }
                                    N(SyntaxKind.CloseParenToken);
                                    N(SyntaxKind.Block);
                                    {
                                        N(SyntaxKind.OpenBraceToken);
                                        N(SyntaxKind.Block);
                                        {
                                            N(SyntaxKind.OpenBraceToken);
                                            N(SyntaxKind.IfStatement);
                                            {
                                                N(SyntaxKind.IfKeyword);
                                                N(SyntaxKind.OpenParenToken);
                                                N(SyntaxKind.TrueLiteralExpression);
                                                {
                                                    N(SyntaxKind.TrueKeyword);
                                                }
                                                N(SyntaxKind.CloseParenToken);
                                                N(SyntaxKind.ExpressionStatement);
                                                {
                                                    N(SyntaxKind.SimpleAssignmentExpression);
                                                    {
                                                        N(SyntaxKind.ElementAccessExpression);
                                                        {
                                                            N(SyntaxKind.IdentifierName);
                                                            {
                                                                N(SyntaxKind.IdentifierToken, "x");
                                                            }
                                                            N(SyntaxKind.BracketedArgumentList);
                                                            {
                                                                N(SyntaxKind.OpenBracketToken);
                                                                N(SyntaxKind.Argument);
                                                                {
                                                                    N(SyntaxKind.NumericLiteralExpression);
                                                                    {
                                                                        N(SyntaxKind.NumericLiteralToken, "1");
                                                                    }
                                                                }
                                                                N(SyntaxKind.CloseBracketToken);
                                                            }
                                                        }
                                                        N(SyntaxKind.EqualsToken);
                                                        N(SyntaxKind.NumericLiteralExpression);
                                                        {
                                                            N(SyntaxKind.NumericLiteralToken, "312");
                                                        }
                                                    }
                                                    N(SyntaxKind.SemicolonToken);
                                                }
                                            }
                                            N(SyntaxKind.CloseBraceToken);
                                        }
                                        N(SyntaxKind.CloseBraceToken);
                                    }
                                }
                                N(SyntaxKind.CloseBraceToken);
                            }
                        }
                        // Here is where we go off the rails.  This corresponds to the `if (true) ...` part after the method
                        N(SyntaxKind.IncompleteMember);
                        {
                            N(SyntaxKind.TupleType);
                            {
                                N(SyntaxKind.OpenParenToken);
                                M(SyntaxKind.TupleElement);
                                {
                                    M(SyntaxKind.IdentifierName);
                                    {
                                        M(SyntaxKind.IdentifierToken);
                                    }
                                }
                                M(SyntaxKind.CommaToken);
                                M(SyntaxKind.TupleElement);
                                {
                                    M(SyntaxKind.IdentifierName);
                                    {
                                        M(SyntaxKind.IdentifierToken);
                                    }
                                }
                                M(SyntaxKind.CloseParenToken);
                            }
                        }
                        // this corresponds to 'y' in 'y = [.. z];'
                        N(SyntaxKind.IncompleteMember);
                        {
                            N(SyntaxKind.IdentifierName);
                            {
                                N(SyntaxKind.IdentifierToken, "y");
                            }
                        }
                        // This corresponds to `[.. z]` which parser thinks is an attribute with an invalid dotted name.
                        N(SyntaxKind.IncompleteMember);
                        {
                            N(SyntaxKind.AttributeList);
                            {
                                N(SyntaxKind.OpenBracketToken);
                                N(SyntaxKind.Attribute);
                                {
                                    N(SyntaxKind.QualifiedName);
                                    {
                                        N(SyntaxKind.QualifiedName);
                                        {
                                            M(SyntaxKind.IdentifierName);
                                            {
                                                M(SyntaxKind.IdentifierToken);
                                            }
                                            N(SyntaxKind.DotToken);
                                            M(SyntaxKind.IdentifierName);
                                            {
                                                M(SyntaxKind.IdentifierToken);
                                            }
                                        }
                                        N(SyntaxKind.DotToken);
                                        N(SyntaxKind.IdentifierName);
                                        {
                                            N(SyntaxKind.IdentifierToken, "z");
                                        }
                                    }
                                }
                                N(SyntaxKind.CloseBracketToken);
                            }
                        }
                        N(SyntaxKind.CloseBraceToken);
                    }
                    N(SyntaxKind.EndOfFileToken);
                }
                EOF();
            }

            // Now delete '}' after 'values[1] = 312;'.  This should result in no diagnostics.
            //
            // Note: because we deleted the end of line after the '{', the line that is now on the line where `values...` was
            // will be the line that was originally after it (the } line).
            var withOpenBraceDeletedLines = withOpenBraceDeletedText.Lines;
            var closeBraceLine = withOpenBraceDeletedLines[valueSetterLineIndex];
            Assert.EndsWith("}", closeBraceLine.ToString());
            var withCloseBraceDeletedText = withOpenBraceDeletedText.WithChanges(new TextChange(closeBraceLine.SpanIncludingLineBreak, ""));
            var withCloseBraceDeletedTree = withOpenBraceDeletedTree.WithChangedText(withCloseBraceDeletedText);

            Assert.Empty(withCloseBraceDeletedTree.GetDiagnostics());

            var fullTree = SyntaxFactory.ParseSyntaxTree(withCloseBraceDeletedText.ToString());
            Assert.Empty(fullTree.GetDiagnostics());

            WalkTreeAndVerify(withCloseBraceDeletedTree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact, CompilerTrait(CompilerFeature.Extensions)]
        public void UpdateFromExtensionToClass()
        {
            var text = @"
class C
{
    extension(object x) { }
}
";
            var oldTree = this.Parse(text, LanguageVersionFacts.CSharpNext);
            var newTree = oldTree.WithReplaceFirst("extension", "class D");
            oldTree.GetDiagnostics().Verify();
            newTree.GetDiagnostics().Verify();

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassKeyword,
                            SyntaxKind.IdentifierToken);

            UsingTree(newTree);

            N(SyntaxKind.CompilationUnit);
            {
                N(SyntaxKind.ClassDeclaration);
                {
                    N(SyntaxKind.ClassKeyword);
                    N(SyntaxKind.IdentifierToken, "C");
                    N(SyntaxKind.OpenBraceToken);
                    N(SyntaxKind.ClassDeclaration);
                    {
                        N(SyntaxKind.ClassKeyword);
                        N(SyntaxKind.IdentifierToken, "D");
                        N(SyntaxKind.ParameterList);
                        {
                            N(SyntaxKind.OpenParenToken);
                            N(SyntaxKind.Parameter);
                            {
                                N(SyntaxKind.PredefinedType);
                                {
                                    N(SyntaxKind.ObjectKeyword);
                                }
                                N(SyntaxKind.IdentifierToken, "x");
                            }
                            N(SyntaxKind.CloseParenToken);
                        }
                        N(SyntaxKind.OpenBraceToken);
                        N(SyntaxKind.CloseBraceToken);
                    }
                    N(SyntaxKind.CloseBraceToken);
                }
                N(SyntaxKind.EndOfFileToken);
            }
            EOF();
        }

        [Fact, CompilerTrait(CompilerFeature.Extensions)]
        public void UpdateFromExtensionToClass_NoParameterIdentifier()
        {
            var text = @"
class C
{
    extension(object) { }
}
";
            var oldTree = this.Parse(text, LanguageVersionFacts.CSharpNext);
            var newTree = oldTree.WithReplaceFirst("extension", "class D");
            oldTree.GetDiagnostics().Verify();
            newTree.GetDiagnostics().Verify(
                // (4,19): error CS1001: Identifier expected
                //     class D(object) { }
                Diagnostic(ErrorCode.ERR_IdentifierExpected, ")").WithLocation(4, 19));

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassKeyword,
                            SyntaxKind.IdentifierToken,
                            SyntaxKind.ParameterList,
                            SyntaxKind.Parameter,
                            SyntaxKind.IdentifierToken);

            UsingTree(newTree,
                // (4,19): error CS1001: Identifier expected
                //     class D(object) { }
                Diagnostic(ErrorCode.ERR_IdentifierExpected, ")").WithLocation(4, 19));

            N(SyntaxKind.CompilationUnit);
            {
                N(SyntaxKind.ClassDeclaration);
                {
                    N(SyntaxKind.ClassKeyword);
                    N(SyntaxKind.IdentifierToken, "C");
                    N(SyntaxKind.OpenBraceToken);
                    N(SyntaxKind.ClassDeclaration);
                    {
                        N(SyntaxKind.ClassKeyword);
                        N(SyntaxKind.IdentifierToken, "D");
                        N(SyntaxKind.ParameterList);
                        {
                            N(SyntaxKind.OpenParenToken);
                            N(SyntaxKind.Parameter);
                            {
                                N(SyntaxKind.PredefinedType);
                                {
                                    N(SyntaxKind.ObjectKeyword);
                                }
                                M(SyntaxKind.IdentifierToken);
                            }
                            N(SyntaxKind.CloseParenToken);
                        }
                        N(SyntaxKind.OpenBraceToken);
                        N(SyntaxKind.CloseBraceToken);
                    }
                    N(SyntaxKind.CloseBraceToken);
                }
                N(SyntaxKind.EndOfFileToken);
            }
            EOF();
        }

        [Fact, CompilerTrait(CompilerFeature.Extensions)]
        public void UpdateFromExtensionToClass_WithName()
        {
            var text = @"
class C
{
    extension E(object x) { }
}
";
            var oldTree = this.Parse(text, LanguageVersionFacts.CSharpNext);
            var newTree = oldTree.WithReplaceFirst("extension", "class");
            oldTree.GetDiagnostics().Verify(
                // (4,15): error CS9281: Extension declarations may not have a name.
                //     extension E(object x) { }
                Diagnostic(ErrorCode.ERR_ExtensionDisallowsName, "E").WithLocation(4, 15));
            newTree.GetDiagnostics().Verify();

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ClassKeyword,
                            SyntaxKind.IdentifierToken);

            UsingTree(newTree);

            N(SyntaxKind.CompilationUnit);
            {
                N(SyntaxKind.ClassDeclaration);
                {
                    N(SyntaxKind.ClassKeyword);
                    N(SyntaxKind.IdentifierToken, "C");
                    N(SyntaxKind.OpenBraceToken);
                    N(SyntaxKind.ClassDeclaration);
                    {
                        N(SyntaxKind.ClassKeyword);
                        N(SyntaxKind.IdentifierToken, "E");
                        N(SyntaxKind.ParameterList);
                        {
                            N(SyntaxKind.OpenParenToken);
                            N(SyntaxKind.Parameter);
                            {
                                N(SyntaxKind.PredefinedType);
                                {
                                    N(SyntaxKind.ObjectKeyword);
                                }
                                N(SyntaxKind.IdentifierToken, "x");
                            }
                            N(SyntaxKind.CloseParenToken);
                        }
                        N(SyntaxKind.OpenBraceToken);
                        N(SyntaxKind.CloseBraceToken);
                    }
                    N(SyntaxKind.CloseBraceToken);
                }
                N(SyntaxKind.EndOfFileToken);
            }
            EOF();
        }

        [Fact, CompilerTrait(CompilerFeature.Extensions)]
        public void UpdateFromClassToExtension()
        {
            var text = @"
class C
{
    class D(object x) { }
}
";
            var oldTree = this.Parse(text, LanguageVersionFacts.CSharpNext);
            var newTree = oldTree.WithReplaceFirst("class D", "extension");
            oldTree.GetDiagnostics().Verify();
            newTree.GetDiagnostics().Verify();

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ExtensionBlockDeclaration,
                            SyntaxKind.ExtensionKeyword);

            UsingTree(newTree);

            N(SyntaxKind.CompilationUnit);
            {
                N(SyntaxKind.ClassDeclaration);
                {
                    N(SyntaxKind.ClassKeyword);
                    N(SyntaxKind.IdentifierToken, "C");
                    N(SyntaxKind.OpenBraceToken);
                    N(SyntaxKind.ExtensionBlockDeclaration);
                    {
                        N(SyntaxKind.ExtensionKeyword);
                        N(SyntaxKind.ParameterList);
                        {
                            N(SyntaxKind.OpenParenToken);
                            N(SyntaxKind.Parameter);
                            {
                                N(SyntaxKind.PredefinedType);
                                {
                                    N(SyntaxKind.ObjectKeyword);
                                }
                                N(SyntaxKind.IdentifierToken, "x");
                            }
                            N(SyntaxKind.CloseParenToken);
                        }
                        N(SyntaxKind.OpenBraceToken);
                        N(SyntaxKind.CloseBraceToken);
                    }
                    N(SyntaxKind.CloseBraceToken);
                }
                N(SyntaxKind.EndOfFileToken);
            }
            EOF();
        }

        [Fact, CompilerTrait(CompilerFeature.Extensions)]
        public void UpdateFromClassToExtension_NoParameterIdentifier()
        {
            var text = @"
class C
{
    class D(object) { }
}
";
            var oldTree = this.Parse(text, LanguageVersionFacts.CSharpNext);
            var newTree = oldTree.WithReplaceFirst("class D", "extension");
            oldTree.GetDiagnostics().Verify(
                // (4,19): error CS1001: Identifier expected
                //     class D(object) { }
                Diagnostic(ErrorCode.ERR_IdentifierExpected, ")").WithLocation(4, 19));
            newTree.GetDiagnostics().Verify();

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ExtensionBlockDeclaration,
                            SyntaxKind.ExtensionKeyword,
                            SyntaxKind.ParameterList,
                            SyntaxKind.Parameter);

            UsingTree(newTree);

            N(SyntaxKind.CompilationUnit);
            {
                N(SyntaxKind.ClassDeclaration);
                {
                    N(SyntaxKind.ClassKeyword);
                    N(SyntaxKind.IdentifierToken, "C");
                    N(SyntaxKind.OpenBraceToken);
                    N(SyntaxKind.ExtensionBlockDeclaration);
                    {
                        N(SyntaxKind.ExtensionKeyword);
                        N(SyntaxKind.ParameterList);
                        {
                            N(SyntaxKind.OpenParenToken);
                            N(SyntaxKind.Parameter);
                            {
                                N(SyntaxKind.PredefinedType);
                                {
                                    N(SyntaxKind.ObjectKeyword);
                                }
                            }
                            N(SyntaxKind.CloseParenToken);
                        }
                        N(SyntaxKind.OpenBraceToken);
                        N(SyntaxKind.CloseBraceToken);
                    }
                    N(SyntaxKind.CloseBraceToken);
                }
                N(SyntaxKind.EndOfFileToken);
            }
            EOF();
        }

        [Fact, CompilerTrait(CompilerFeature.Extensions)]
        public void UpdateFromClassToExtension_WithName()
        {
            var text = @"
class C
{
    struct D(object x) { }
}
";
            var oldTree = this.Parse(text, LanguageVersionFacts.CSharpNext);
            var newTree = oldTree.WithReplaceFirst("struct", "extension");
            oldTree.GetDiagnostics().Verify();
            newTree.GetDiagnostics().Verify(
                // (4,15): error CS9281: Extension declarations may not have a name.
                //     extension D(object x) { }
                Diagnostic(ErrorCode.ERR_ExtensionDisallowsName, "D").WithLocation(4, 15));

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                            SyntaxKind.CompilationUnit,
                            SyntaxKind.ClassDeclaration,
                            SyntaxKind.ExtensionBlockDeclaration,
                            SyntaxKind.ExtensionKeyword);

            UsingTree(newTree,
                // (4,15): error CS9281: Extension declarations may not have a name.
                //     extension D(object x) { }
                Diagnostic(ErrorCode.ERR_ExtensionDisallowsName, "D").WithLocation(4, 15));

            N(SyntaxKind.CompilationUnit);
            {
                N(SyntaxKind.ClassDeclaration);
                {
                    N(SyntaxKind.ClassKeyword);
                    N(SyntaxKind.IdentifierToken, "C");
                    N(SyntaxKind.OpenBraceToken);
                    N(SyntaxKind.ExtensionBlockDeclaration);
                    {
                        N(SyntaxKind.ExtensionKeyword);
                        N(SyntaxKind.ParameterList);
                        {
                            N(SyntaxKind.OpenParenToken);
                            N(SyntaxKind.Parameter);
                            {
                                N(SyntaxKind.PredefinedType);
                                {
                                    N(SyntaxKind.ObjectKeyword);
                                }
                                N(SyntaxKind.IdentifierToken, "x");
                            }
                            N(SyntaxKind.CloseParenToken);
                        }
                        N(SyntaxKind.OpenBraceToken);
                        N(SyntaxKind.CloseBraceToken);
                    }
                    N(SyntaxKind.CloseBraceToken);
                }
                N(SyntaxKind.EndOfFileToken);
            }
            EOF();
        }

        [Fact, CompilerTrait(CompilerFeature.Extensions)]
        public void UpdateExtension_ChangeParameterList()
        {
            var text = """
class C
{
    extension(object, Type z1) { }
}
""";
            var oldTree = this.Parse(text, LanguageVersionFacts.CSharpNext);
            var newTree = oldTree.WithReplaceFirst("z1", "z2");
            oldTree.GetDiagnostics().Verify();
            newTree.GetDiagnostics().Verify();

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            TestDiffsInOrder(diffs,
                SyntaxKind.CompilationUnit,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.ExtensionBlockDeclaration,
                SyntaxKind.ExtensionKeyword,
                SyntaxKind.ParameterList,
                SyntaxKind.Parameter,
                SyntaxKind.IdentifierToken);

            UsingTree(newTree);

            N(SyntaxKind.CompilationUnit);
            {
                N(SyntaxKind.ClassDeclaration);
                {
                    N(SyntaxKind.ClassKeyword);
                    N(SyntaxKind.IdentifierToken, "C");
                    N(SyntaxKind.OpenBraceToken);
                    N(SyntaxKind.ExtensionBlockDeclaration);
                    {
                        N(SyntaxKind.ExtensionKeyword);
                        N(SyntaxKind.ParameterList);
                        {
                            N(SyntaxKind.OpenParenToken);
                            N(SyntaxKind.Parameter);
                            {
                                N(SyntaxKind.PredefinedType);
                                {
                                    N(SyntaxKind.ObjectKeyword);
                                }
                            }
                            N(SyntaxKind.CommaToken);
                            N(SyntaxKind.Parameter);
                            {
                                N(SyntaxKind.IdentifierName);
                                {
                                    N(SyntaxKind.IdentifierToken, "Type");
                                }
                                N(SyntaxKind.IdentifierToken, "z2");
                            }
                            N(SyntaxKind.CloseParenToken);
                        }
                        N(SyntaxKind.OpenBraceToken);
                        N(SyntaxKind.CloseBraceToken);
                    }
                    N(SyntaxKind.CloseBraceToken);
                }
                N(SyntaxKind.EndOfFileToken);
            }
            EOF();
        }

        #region "Regression"

#if false
        [Fact]
        public void DevDiv3599()
        {
            var text =
@"class B {
#if false
#endif
}
";
            var newText =
@"class B
{
    private class E
    {
    }
#if false
#endif
}
";
            var oldTree = this.Parse(text);
            Assert.Equal(text, oldTree.GetCompilationUnitRoot().ToFullString());
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Count);
            var oldDirectives = oldTree.GetCompilationUnitRoot().GetDirectives();
            Assert.Equal(2, oldDirectives.Count);
            Assert.Equal(SyntaxKind.IfDirectiveTrivia, oldDirectives[0].Kind);
            Assert.Equal(SyntaxKind.EndIfDirectiveTrivia, oldDirectives[1].Kind);

            var newTree = oldTree.WithChange(SourceText.From(newText),
                new TextChangeRange(new TextSpan(7, 0), 16),
                new TextChangeRange(new TextSpan(8, 0), 13),
                new TextChangeRange(new TextSpan(9, 0), 7)); //this is the tricky one - it occurs before the trailing trivia of the closing brace
            //this is the line that fails without the fix to DevDiv #3599 - there's extra text because of a blender error
            Assert.Equal(newText, newTree.GetCompilationUnitRoot().ToFullString());
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Count);
            var newDirectives = newTree.GetCompilationUnitRoot().GetDirectives();
            Assert.Equal(2, oldDirectives.Count);
            Assert.Equal(SyntaxKind.IfDirectiveTrivia, oldDirectives[0].Kind);
            Assert.Equal(SyntaxKind.EndIfDirectiveTrivia, oldDirectives[1].Kind);

            var diffs = SyntaxDifferences.GetRebuiltNodes(oldTree, newTree);
            Assert.Equal(8, diffs.Count);
            Assert.Equal(SyntaxKind.CompilationUnit,   // Everything - different because a descendant is different
            Assert.Equal(SyntaxKind.ClassDeclaration,  // class B - different because a descendant is different
            //class keyword is reused
            Assert.Equal(SyntaxKind.IdentifierToken,   // B - different because there a change immediately afterward
            //open brace is reused
            Assert.Equal(SyntaxKind.ClassDeclaration,  // class E - different because it's inserted
            Assert.Equal(SyntaxKind.PrivateKeyword,    // private - different because it's inserted
            Assert.Equal(SyntaxKind.IdentifierToken,   // E - different because it's inserted
            Assert.Equal(SyntaxKind.OpenBraceToken,    // { - different because it's inserted
            Assert.Equal(SyntaxKind.CloseBraceToken,   // } - different because it's inserted
            //close brace is reused
            //eof is reused
        }
#endif

        [Fact]
        public void Bug892212()
        {
            // prove that this incremental change can occur without exception!
            var text = "/";
            var startTree = SyntaxFactory.ParseSyntaxTree(text);
            var newTree = startTree.WithInsertAt(1, "/");
            var fullText = newTree.GetCompilationUnitRoot().ToFullString();
            Assert.Equal("//", fullText);
        }

#if false
        [WorkItem(896260, "Personal")]
        [Fact]//(Skip = "Bug")]
        public void RemovePartialFromClassWithIncorrectSpan()
        {
            var test = @"partial class C{}";
            var resultString = "class C{}";
            var startTree = SyntaxTree.Parse(test);
            var finalString = startTree.GetCompilationUnitRoot().ToString();
            var incrementalChange = new TextChange(startTree.Text, SourceText.From(resultString), new TextChangeRange[] { new TextChangeRange(new TextSpan(0, 7), 0) }); // NOTE: The string length here is a bit too short for the change
            var newTree = startTree.WithChange(incrementalChange);
            var output = newTree.GetCompilationUnitRoot().ToString();
            Assert.Equal(output, resultString);
        }
#endif

#if false // can no longer specify an incorrect range
        [Fact]
        public void Bug896260()
        {
            var test = @"partial class C{}";
            var startTree = SyntaxTree.ParseText(test);
            var finalString = startTree.GetCompilationUnitRoot().ToString();

            Exception e = null;
            try
            {
                // NOTE: The string length here is a bit too short for the change
                var newTree = startTree.WithChange(SourceText.From("class C{}"), new TextChangeRange[] { new TextChangeRange(new TextSpan(0, 7), 0) });
            }
            catch (Exception x)
            {
                e = x;
            }

            Assert.NotNull(e);
        }
#endif

        [Fact]
        public void Bug896262()
        {
            var text = SourceText.From(@"partial class C{}");
            var startTree = SyntaxFactory.ParseSyntaxTree(text);
            var finalString = startTree.GetCompilationUnitRoot().ToFullString();

            var newText = text.WithChanges(new TextChange(new TextSpan(0, 8), ""));
            var newTree = startTree.WithChangedText(newText);
            var finalText = newTree.GetCompilationUnitRoot().ToFullString();
            Assert.Equal(newText.ToString(), finalText);
        }

        [WorkItem(536457, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536457")]
        [Fact]
        public void RemovePartialFromClassWithCorrectSpan()
        {
            var text = SourceText.From(@"partial class C{}");
            var startTree = SyntaxFactory.ParseSyntaxTree(text);
            var finalString = startTree.GetCompilationUnitRoot().ToFullString();

            var newText = text.WithChanges(new TextChange(new TextSpan(0, 8), ""));
            var newTree = startTree.WithChangedText(newText);
            var output = newTree.GetCompilationUnitRoot().ToFullString();
            Assert.Equal(newText.ToString(), output);
        }

        [WorkItem(536519, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536519")]
        [Fact]
        public void AddTopLevelMemberErrorDifference()
        {
            SourceText oldText = SourceText.From(@"
using System;

public d");

            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, 'e', out incrementalTree, out parsedTree);

            // The bug is that the errors are currently different
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536520, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536520")]
        [Fact]
        public void AddIncompleteStatementErrorDifference()
        {
            SourceText oldText = SourceText.From(@"
public class Test
{
    static int Main()
    {
        ");

            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, 'r', out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536523, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536523")]
        [Fact]
        public void DifferentNumberOfErrorsForNonCompletedBlock()
        {
            SourceText oldText = SourceText.From(@"
public class Test
{
    static int Main()
    {
        return 1;
 ");

            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, '}', out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536649, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536649")]
        [Fact]
        public void AddingCharacterOnErrorWithExtern()
        {
            SourceText oldText = SourceText.From(@"
class C
{   
	public extern C();
	static int Main ()
	");
            char newCharacter = '{';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536650, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536650")]
        [Fact]
        public void ErrorWithExtraModifiers()
        {
            SourceText oldText = SourceText.From(@"
class MyClass {
	internal internal const in");
            char newCharacter = 't';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536651, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536651")]
        [Fact]
        public void CommentsCauseDifferentErrorStrings()
        {
            SourceText oldText = SourceText.From(@"
class A
{
   static public int Main ()
   {
      double d = new double(1);   /");
            char newCharacter = '/';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536652, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536652")]
        [Fact]
        public void ErrorModifierOnClass()
        {
            SourceText oldText = SourceText.From(@"
protected class My");
            char newCharacter = 'C';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536653, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536653")]
        [Fact]
        public void ErrorPartialClassWithNoBody()
        {
            SourceText oldText = SourceText.From(@"
public partial clas");
            char newCharacter = 's';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536654, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536654")]
        [Fact]
        public void ErrorConstKeywordInMethodName()
        {
            SourceText oldText = SourceText.From(@"	class A
	{
		protected virtual void Finalize const () { }
	}

	class B");
            char newCharacter = ' ';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536655, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536655")]
        [Fact]
        public void ErrorWithOperatorDeclaration()
        {
            SourceText oldText = SourceText.From(@"public class TestClass
{
    public static TestClass operator ++");
            char newCharacter = '(';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536661, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536661")]
        [Fact]
        public void ErrorWithNestedTypeInNew()
        {
            SourceText oldText = SourceText.From(@"using System;

class Test {
	static public int Main(String[] args) {
		AbstractBase b = new AbstractBase.");
            char newCharacter = 'I';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536662, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536662")]
        [Fact]
        public void ErrorWithInvalidMethodName()
        {
            SourceText oldText = SourceText.From(@"public class MyClass {	
	int -goo(");
            char newCharacter = ')';
            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, newCharacter, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536524, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536524")]
        [Fact]
        public void AddingAFieldInIncompleteClass()
        {
            SourceText oldText = SourceText.From(@"
public class Test
{
    ");

            SyntaxTree incrementalTree, parsedTree;
            CharByCharIncrementalParse(oldText, 'C', out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(903526, "DevDiv/Personal")]
        [Fact]
        public void AddingTryBlockToMethodOneCharAtTime()
        {
            SourceText startingText = SourceText.From(@"
public class Test
{
    void Goo() {} // Point
}");

            SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(startingText);

            // Insert a try/catch block inside the method, one char at a time
            foreach (char c in "try{}catch{}")
            {
                syntaxTree = syntaxTree.WithInsertBefore("} // Point", c.ToString());
            }

            Assert.Equal(0, syntaxTree.GetCompilationUnitRoot().Errors().Length);
        }

        [WorkItem(903526, "DevDiv/Personal")]
        [Fact]
        public void AddingIfBlockToMethodOneCharAtTime()
        {
            SourceText startingText = SourceText.From(@"
public class Test
{
    void Goo() {} // Point
}");

            SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(startingText);

            foreach (char c in "if(true){}else{}")
            {
                syntaxTree = syntaxTree.WithInsertBefore("} // Point", c.ToString());
            }

            Assert.Equal(0, syntaxTree.GetCompilationUnitRoot().Errors().Length);
        }

        [Fact]
        public void AddingWhileBlockToMethodOneCharAtTime()
        {
            SourceText startingText = SourceText.From(@"
public class Test
{
    void Goo() {} // Point
}");

            SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(startingText);

            foreach (char c in "while(true){}")
            {
                syntaxTree = syntaxTree.WithInsertBefore("} // Point", c.ToString());
            }

            Assert.Equal(0, syntaxTree.GetCompilationUnitRoot().Errors().Length);
        }

        [WorkItem(536563, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536563")]
        [Fact]
        public void CommentOutClassKeyword()
        {
            SourceText oldText = SourceText.From(@"class MyClass 
{
	private enum E {zero, one, two, three};
	public const E test = E.two;
	public static int Main() 
	{
		return 1;
	}
}");
            int locationOfChange = 0, widthOfChange = 5;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536565, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536565")]
        [Fact]
        public void CommentOutOpeningCurlyOnPrivateDeclaration()
        {
            SourceText oldText = SourceText.From(@"
private class B{ public class MyClass 
{

	private enum E {zero, one, two, three};
	public const E test = E.two;

	public static int Main() 
	{
		return 1;
	}
}}");
            int locationOfChange = 42, widthOfChange = 1;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536567, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536567")]
        [Fact]
        public void CommentOutBracesOnMethodDeclaration()
        {
            SourceText oldText = SourceText.From(@"
private class B{ private class MyClass 
{

	private enum E {zero, one, two, three};
	public const E test = E.two;

	public int Main() 
	{
		return 1;
	}
}}");
            int locationOfChange = 139, widthOfChange = 2;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536568, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536568")]
        [Fact]
        public void CommentOutEventKeyword()
        {
            SourceText oldText = SourceText.From(@"interface IGoo
{
	event EventHandler E { add { } remove { } }
}

class Test 
{
	public static int Main()
	{
		return 1;
	}
}");
            int locationOfChange = 20, widthOfChange = 6;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536571, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536571")]
        [Fact]
        public void CommentOutEventAccessor()
        {
            SourceText oldText = SourceText.From(@"interface IGoo
{
	event EventHandler E { add { } remove { } }
}

class Test 
{
	public static int Main()
	{
		return 1;
	}
}");
            int locationOfChange = 43, widthOfChange = 3;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536573, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536573")]
        [Fact]
        public void CommentOutDotInUsingAlias()
        {
            SourceText oldText = SourceText.From(@"using System.Runtime.CompilerServices;
[assembly:InternalsVisibleTo(a)]
class A
{
	public int x = 0;
	static int Main()
	{	
		return 0;
	}
}
");
            int locationOfChange = 12, widthOfChange = 1;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536577, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536577")]
        [Fact]
        public void CommentOutThisInIndexer()
        {
            SourceText oldText = SourceText.From(@"class A
{
		int MyInter.this[int i] {
		get {
			return intI + 1;
		}
		set {
			intI = value + 1;
		}
	}

}
");
            int locationOfChange = 26, widthOfChange = 4;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536578, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536578")]
        [Fact]
        public void CommentOutReturnStatementInProperty()
        {
            SourceText oldText = SourceText.From(@"public class MyClass {
	int this[] {
		get {
			return intI;
		}
		set {
			intI = value;
		}
	}
}
");
            int locationOfChange = 51, widthOfChange = 7;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(905311, "DevDiv/Personal")]
        [Fact]
        public void AddSemicolonInForLoop()
        {
            SourceText oldText = SourceText.From(@"public class MyClass {
    void goo()
    {
        for (int i = 0
    }
}
");
            int locationOfInsert = oldText.ToString().IndexOf('0') + 1;
            SyntaxTree oldTree = SyntaxFactory.ParseSyntaxTree(oldText);

            // The bug was that this would simply assert
            SyntaxTree newTree = oldTree.WithInsertAt(locationOfInsert, ";");
        }

        [WorkItem(536635, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536635")]
        [Fact]
        public void AddSemicolonAfterStartOfVerbatimString()
        {
            var oldText = @"class A
{
string s = @
}
";
            var oldTree = SyntaxFactory.ParseSyntaxTree(oldText);
            var newTree = oldTree.WithInsertAt(oldText.IndexOf('@'), ";");
        }

        [WorkItem(536717, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536717")]
        [Fact]
        public void AddReturnWithTriviaAtStart()
        {
            string oldText = @"0;
    }
}";
            string diffText = "return ";

            // Get the Original parse tree
            SyntaxTree origTree = SyntaxFactory.ParseSyntaxTree(oldText);

            // Get the tree after incremental parse after applying the change
            SyntaxTree incrTree = origTree.WithInsertAt(0, diffText);

            string newText = diffText + oldText;

            // Get the full parse tree with the applied change
            SyntaxTree fullTree = SyntaxFactory.ParseSyntaxTree(newText);

            CompareIncToFullParseErrors(incrTree, fullTree);
        }

        [WorkItem(536728, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536728")]
        [Fact]
        public void CommentClassWithGTandGTEOperator()
        {
            // the token in question is now converted to skipped text so this check is no longer applicable
#if false
            SourceText oldText = SourceText.From(@"class Test
{
     static bool Test()
     {
        if (b21 >>= b22)
        {
        }
     }
}
");
            int locationOfChange = 0, widthOfChange = 5;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update "class" to "/*class*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify if the >>= operator in the incremental parse tree is actually 2 separate tokens (> and >=)
            Assert.Equal(SyntaxKind.GreaterThanToken, incrementalTree.GetCompilationUnitRoot().ChildNodesAndTokens()[2].ChildNodesAndTokens()[8].Kind);
            Assert.Equal(SyntaxKind.GreaterThanEqualsToken, incrementalTree.GetCompilationUnitRoot().ChildNodesAndTokens()[2].ChildNodesAndTokens()[9].Kind);

            // The full parse tree should also have the above tree structure for the >>= operator
            Assert.Equal(SyntaxKind.GreaterThanToken, parsedTree.GetCompilationUnitRoot().ChildNodesAndTokens()[2].ChildNodesAndTokens()[8].Kind);
            Assert.Equal(SyntaxKind.GreaterThanEqualsToken, parsedTree.GetCompilationUnitRoot().ChildNodesAndTokens()[2].ChildNodesAndTokens()[9].Kind);

            // Serialize the parse trees and compare the incremental parse tree against the full parse tree
            // Assert.Equal( parsedTree.GetCompilationUnitRoot().ToXml().ToString(), incrementalTree.GetCompilationUnitRoot().ToXml().ToString());
            Assert.True(parsedTree.GetCompilationUnitRoot().IsEquivalentTo(incrementalTree.GetCompilationUnitRoot()));
#endif
        }

        [WorkItem(536730, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536730")]
        [Fact]
        public void CodeWithDollarSign()
        {
            SourceText oldText = SourceText.From(@"class  filesystem{
	po$i$;
}");
            int locationOfChange = 0, widthOfChange = 5;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update "class" to "/*class*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify when you roundtrip the text from the full parse with change should match the text from the incremental parse with change
            // The bug is that the "$" sign was being swallowed on the incremental parse
            Assert.Equal(parsedTree.GetCompilationUnitRoot().ToFullString(), incrementalTree.GetCompilationUnitRoot().ToFullString());
        }

        [WorkItem(536731, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536731")]
        [Fact]
        public void CommentCodeInGOTOStatement()
        {
            SourceText oldText = SourceText.From(@"class CSTR020mod{ public static void CSTR020()  {  ON ERROR GOTO ErrorTrap; } }");
            int locationOfChange = oldText.ToString().IndexOf("ON", StringComparison.Ordinal), widthOfChange = 2;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update "ON" to "/*ON*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536734, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536734")]
        [Fact]
        public void CommentConstInConstDeclError()
        {
            SourceText oldText = SourceText.From(@"class  A
{
    const byte X4var As Byte = 55;
}
");
            int locationOfChange = oldText.ToString().IndexOf("const", StringComparison.Ordinal), widthOfChange = 5;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update "const" to "/*const*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536738, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536738")]
        [Fact]
        public void CommentClassWithDelegateDecl()
        {
            SourceText oldText = SourceText.From(@"public class DynClassDrived
{
     protected delegate void ProtectedDel(dynamic d);
}
");
            int locationOfChange = oldText.ToString().IndexOf("class", StringComparison.Ordinal), widthOfChange = 5;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            var oldTree = SyntaxFactory.ParseSyntaxTree(oldText);

            // This function will update "class" to "/*class*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536738, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536738")]
        [Fact]
        public void CommentCloseBraceInPropertyDecl()
        {
            SourceText oldText = SourceText.From(@"public class MemberClass
{
    public MyStruct[] Property_MyStructArr { get; set; }
    public MyEnum[] Property_MyEnumArr { set; private get; }
}
");
            int locationOfChange = oldText.ToString().IndexOf('}'), widthOfChange = 5;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update the first closing brace in property declaration Property_MyStructArr "}" to "/*}*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [Fact]
        public void CommentCloseBraceInInitOnlyPropertyDecl()
        {
            SourceText oldText = SourceText.From(@"public class MemberClass
{
    public MyStruct[] Property_MyStructArr { get; init; }
    public MyEnum[] Property_MyEnumArr { init; private get; }
}
");
            int locationOfChange = oldText.ToString().IndexOf('}'), widthOfChange = 5;

            // This function will update the first closing brace in property declaration Property_MyStructArr "}" to "/*}*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out SyntaxTree incrementalTree, out SyntaxTree parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536739, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536739")]
        [Fact]
        public void CommentFixedInIllegalArrayDecl()
        {
            SourceText oldText = SourceText.From(@"class Test
{
    unsafe struct A
    {
        public fixed byte Array[dy[""Test""]];
    }
}");
            int locationOfChange = oldText.ToString().IndexOf("fixed", StringComparison.Ordinal), widthOfChange = 5;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update "fixed" to "/*fixed*/" in oldText above
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536788, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536788")]
        [Fact]
        public void CommentGlobalUsedAsAlias()
        {
            SourceText oldText = SourceText.From(
@"using @global=System.Int32;
class Test
{
}
");
            int locationOfChange = oldText.ToString().IndexOf("@global", StringComparison.Ordinal), widthOfChange = 7;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update "@global" to "/*@global*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify if the fully parsed tree and the incrementally parse tree have the same number of children
            Assert.Equal(parsedTree.GetCompilationUnitRoot().ChildNodesAndTokens().Count, incrementalTree.GetCompilationUnitRoot().ChildNodesAndTokens().Count);

            // Verify if the children of the trees are of the same kind
            for (int i = 0; i < parsedTree.GetCompilationUnitRoot().ChildNodesAndTokens().Count; i++)
            {
                Assert.Equal(parsedTree.GetCompilationUnitRoot().ChildNodesAndTokens()[i].Kind(), incrementalTree.GetCompilationUnitRoot().ChildNodesAndTokens()[i].Kind());
            }
        }

        [WorkItem(536789, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536789")]
        [Fact]
        public void CommentUsingStmtGlobalUsedAsAlias()
        {
            SourceText oldText = SourceText.From(
@"using @global=System.Int32;
class Test
{
    static int Main()
    {
        return (@global) 0;
    }
}
");
            string txtToCmnt = @"using @global=System.Int32;";
            int locationOfChange = oldText.ToString().IndexOf(txtToCmnt, StringComparison.Ordinal), widthOfChange = txtToCmnt.Length;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update "using @global=System.Int32;" to "/*using @global=System.Int32;*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536790, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536790")]
        [Fact]
        public void CmntMainInCodeWithGlobalQualifierInUnsafe()
        {
            SourceText oldText = SourceText.From(
@"class Test
{
    unsafe static int Main() 
    {
        global::System.Int32* p = stackalloc global::System.Int32[5];
    }
}
");
            string txtToCmnt = @"Main";
            int locationOfChange = oldText.ToString().IndexOf(txtToCmnt, StringComparison.Ordinal), widthOfChange = txtToCmnt.Length;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will update "Main" to "/*Main*/" in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536842, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536842"), WorkItem(543452, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/543452")]
        [Fact]
        public void DelegateDeclInvalidCastException()
        {
            SourceText oldText = SourceText.From(
@"    public delegate void MyDelegate01(dynamic d, int n);
    [System.CLSCompliant(false)]
");
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the ' ' character to the end of oldText
            // The bug is that when you do the incremental parse with the change an InvalidCastException is thrown at runtime.
            CharByCharIncrementalParse(oldText, ' ', out incrementalTree, out parsedTree);

            // Verify the incrementalTree text and the fully parsed tree text matches
            Assert.Equal(parsedTree.GetText().ToString(), incrementalTree.GetText().ToString());

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536843, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536843")]
        [Fact]
        public void KeyExistsArgumentException()
        {
            SourceText oldText = SourceText.From(
@"    public abstract class AbstractCompiler : ICompiler
    {
        protected virtual IDictionary GetOptions()
        {
            foreach (string parameter in parameters.Split(' '))
            {
                if (true)
                {

                    string[] parts = parameter.Remove(0, 1).Split(':');
                    string key = parts[0].ToLower();

                    if (true)
                    {
                    }
                    if (true)
                    {

                    }
                    else if (false)
                    {

                    }

                }

            }

        }

        protected virtual TargetType GetTargetType(IDictionary options)
        {
        }

    }
");
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the ' ' character to the end of oldText
            // The bug is that when you do the incremental parse with the change an ArgumentException is thrown at runtime.
            CharByCharIncrementalParse(oldText, ' ', out incrementalTree, out parsedTree);

            // Verify the incrementalTree text and the fully parsed tree text matches
            Assert.Equal(parsedTree.GetText().ToString(), incrementalTree.GetText().ToString());

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536849, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536849")]
        [Fact]
        public void QueryExprWithKeywordsAsVariablesAndIncompleteJoin()
        {
            SourceText oldText = SourceText.From(
@"class Test {        
    static void Main()
    { 
        var q = 
	from  string  @params in ( @foreach/9)
	join");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the ' ' character to the end of oldText
            CharByCharIncrementalParse(oldText, ' ', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536865, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536865")]
        [Fact]
        public void IncompleteGenericTypeParamVarDecl()
        {
            SourceText oldText = SourceText.From(
@"public class Test
{
    public static int Main()
    {
        C<B, A");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the '>' character to the end of oldText
            CharByCharIncrementalParse(oldText, '>', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536866, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536866")]
        [Fact]
        public void IncompleteArglistMethodInvocation()
        {
            SourceText oldText = SourceText.From(
@"public class Test
{
    public static void Run()
    {
        testvar.Test(__arglist(10l, 1");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the '2' character to the end of oldText
            CharByCharIncrementalParse(oldText, '2', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536867, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536867")]
        [Fact]
        public void IncompleteErrorExtensionMethodDecl()
        {
            SourceText oldText = SourceText.From(
@"public static class Extensions
{
    public static this Goo(int i, this string str)");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the ' ' character to the end of oldText
            // The bug is that the Incremental Parser throws a NullReferenceException
            CharByCharIncrementalParse(oldText, ' ', out incrementalTree, out parsedTree);

            // Verify the incrementalTree text and the fully parsed tree text matches
            Assert.Equal(parsedTree.GetText().ToString(), incrementalTree.GetText().ToString());

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536868, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536868")]
        [Fact]
        public void IncompleteErrorLambdaExpr()
        {
            SourceText oldText = SourceText.From(
@"public class Program
{
    public static int Main()
    {         
        D[] a2 = new [] {(int x)=");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the '>' character to the end of oldText
            CharByCharIncrementalParse(oldText, '>', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536871, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536871")]
        [Fact]
        public void IncompleteCodeFollowingXmlDocStyleComment()
        {
            SourceText oldText = SourceText.From(
@"class C 
{
    /// =>
    ");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the 's' character to the end of oldText
            CharByCharIncrementalParse(oldText, 's', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536897, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536897")]
        [Fact]
        public void IncompleteNamespaceFollowingExternError()
        {
            SourceText oldText = SourceText.From(
@"using C1 = extern;
namespace N");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the '1' character to the end of oldText
            CharByCharIncrementalParse(oldText, '1', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536898, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536898")]
        [Fact]
        public void IncompleteConditionWithJaggedArrayAccess()
        {
            SourceText oldText = SourceText.From(
@"class A
{
  public static int Main()
  {
    if (arr[2][3l] =");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the '=' character to the end of oldText
            CharByCharIncrementalParse(oldText, '=', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536899, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536899")]
        [Fact]
        public void TrailingCommentFollowingAttributesInsideMethod()
        {
            SourceText oldText = SourceText.From(
@"public class goo 
{
  public static int Goo
  {
    [method:A][goo:A]/");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            var oldTree = SyntaxFactory.ParseSyntaxTree(oldText);

            // This function will add the '/' character to the end of oldText
            CharByCharIncrementalParse(oldText, '/', out incrementalTree, out parsedTree);

            // Verify that the first child node of the root is equivalent between incremental tree and full parse tree
            Assert.Equal(parsedTree.GetCompilationUnitRoot().ChildNodesAndTokens()[0].AsNode().ToFullString(), incrementalTree.GetCompilationUnitRoot().ChildNodesAndTokens()[0].AsNode().ToFullString());
        }

        [WorkItem(536901, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536901")]
        [Fact]
        public void SpecialAttribNameWithDoubleAtToken()
        {
            SourceText oldText = SourceText.From(
@"[@@X");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the ']' character to the end of oldText
            CharByCharIncrementalParse(oldText, ']', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536903, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536903")]
        [Fact]
        public void AssertForAttributeWithGenericType()
        {
            SourceText oldText = SourceText.From(
@"[Goo<i");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the 'n' character to the end of oldText
            // The bug is that an assert is thrown when you perform the incremental parse with the change
            CharByCharIncrementalParse(oldText, 'n', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(539056, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539056")]
        [Fact]
        public void AssertOnTypingColonInGenericTypeConstraint()
        {
            SourceText oldText = SourceText.From(
@"class Meta<T>:imeta<T> where T");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the ':' character to the end of oldText
            // The bug is that an assert is thrown when you perform the incremental parse with the change
            CharByCharIncrementalParse(oldText, ':', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536904, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536904")]
        [Fact]
        public void ArithmeticExprWithLongConstant()
        {
            SourceText oldText = SourceText.From(
@"public class arith0018 
{
  public static void Main()
  {
    long l1 = 1l/0");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the 'l' character to the end of oldText
            CharByCharIncrementalParse(oldText, 'l', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536913, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536913")]
        [Fact]
        public void AddClassKeywordWithAnonymousMethodThrowsIndexOutOfRangeException()
        {
            SourceText oldText = SourceText.From(
@"Production<V, T>
{
    private readonly T epsilon=default(T);

    public Production(T epsilon, Function<SomeType<object>, object> action, V variable, SomeType<object> someType)
    {
        ((VoidDelegate)delegate
        {
            someType.Iterate(delegate(object o)
            {
               System.Console.WriteLine(((BoolDelegate)delegate { return object.Equals(o, this.epsilon); })());
            });
        })();
    }
}");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the "class " text to the start of oldText
            // The bug is that the incremental parser was throwing an IndexOutofRangeException
            TokenByTokenBottomUp(oldText, "class ", out incrementalTree, out parsedTree);

            // Verify the incrementalTree roundtrip text is the same as parsedTree roundtrip text
            Assert.Equal(parsedTree.GetCompilationUnitRoot().ToFullString(), incrementalTree.GetCompilationUnitRoot().ToFullString());

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536914, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536914")]
        [Fact]
        public void AddClassKeywordWithParamsModifierInAnonymousMethod()
        {
            SourceText oldText = SourceText.From(
@"Test
{
    static int Goo()
    {
        Dele f = delegate(params int[] a) { return 1;};
    }
}
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the "class " text to the start of oldText
            TokenByTokenBottomUp(oldText, "class ", out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536916, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536916")]
        [Fact]
        public void AddEqualTokenBeforeLongConst()
        {
            SourceText oldText = SourceText.From(
@"3l;
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the "=" text to the start of oldText
            TokenByTokenBottomUp(oldText, "= ", out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536917, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536917")]
        [Fact]
        public void AddEqualTokenBeforeHugeConst()
        {
            SourceText oldText = SourceText.From(
@"18446744073709551616;
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the "=" text to the start of oldText
            TokenByTokenBottomUp(oldText, "=", out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536616, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536616")]
        [Fact]
        public void AddEndTagToXmlDocComment()
        {
            SourceText oldText = SourceText.From(
@"class c1
{
/// <Summary>
/// <
");
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            TokenByTokenBottomUp(oldText, "/", out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537888, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537888")]
        [Fact]
        public void AddClassKeywordToCodeWithConstructorAndProperty()
        {
            SourceText oldText = SourceText.From(
@"IntVector
{
    public IntVector(int length)
    {
    }

    public int Length
    {
        get
        {
            return 1;
        }
    }
}
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the "class " text to the start of oldText
            TokenByTokenBottomUp(oldText, "class ", out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537890, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537890")]
        [Fact]
        public void AddCurlyBracesToIncompleteCode()
        {
            SourceText oldText = SourceText.From(
@"		int[][] arr;

		if (arr[1][1] == 0)
			return 0;
		else
			return 1;
	}
}
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the "{ " text to the start of oldText
            TokenByTokenBottomUp(oldText, "{ ", out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537891, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537891")]
        [Fact]
        public void AddOpenParenToIncompleteMethodDeclBeforeDestructor()
        {
            SourceText oldText = SourceText.From(
@"string s) {}
~Widget() {}
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the "(" text to the start of oldText
            TokenByTokenBottomUp(oldText, "(", out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(538977, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538977")]
        [Fact]
        public void AddTokenToIncompleteQueryExpr()
        {
            SourceText oldText = SourceText.From(
@"equals abstract select new {  };
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the "i " text to the start of oldText
            TokenByTokenBottomUp(oldText, "i ", out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536986, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536986")]
        [Fact]
        public void IncompleteGenericInterfaceImplementation()
        {
            SourceText oldText = SourceText.From(
@"class GenInt : IGenX<int[]>, IGenY<int> 
{
	string IGenX<int[]>.m");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the '(' character to the end of oldText
            CharByCharIncrementalParse(oldText, '(', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536988, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536988")]
        [Fact]
        public void IncompleteIndexerDecl()
        {
            SourceText oldText = SourceText.From(
@"public class Test 
{
	int this[ params int [] args, i");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the 'n' character to the end of oldText
            CharByCharIncrementalParse(oldText, 'n', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536990, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536990")]
        [Fact]
        public void IncompleteGenericVarDeclWithUnderscore()
        {
            SourceText oldText = SourceText.From(
@"public class Test
{
    public static int Main()
    {
        cT ct = _class.TestT<cT, cU");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the '>' character to the end of oldText
            CharByCharIncrementalParse(oldText, '>', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(536991, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/536991")]
        [Fact]
        public void IncompleteUnsafeArrayInit()
        {
            SourceText oldText = SourceText.From(
@"unsafe class Test 
{
	unsafe void*[] A = {(void*");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the ')' character to the end of oldText
            CharByCharIncrementalParse(oldText, ')', out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537012, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537012")]
        [Fact]
        public void RemoveClassIdentifierTokenWithDelegDecl()
        {
            SourceText oldText = SourceText.From(
@"class Test
{
    static int Goo()
    {

        Dele f = delegate(params int[] a) { return 1;};
    }
}");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "Test";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "Test" token from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537889, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537889")]
        [Fact]
        public void RemoveBracesInExtensionIndexer()
        {
            SourceText oldText = SourceText.From(
@"public static class Extensions
{
    public static int this(this int x)[int index1] { get { return 9; }  }

    public static int Main()
    {
        return 0;
    }
}");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = ")[";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the ")[" token from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537892, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537892")]
        [Fact]
        public void RemoveParensInMethodDeclContainingPartialKeyword()
        {
            SourceText oldText = SourceText.From(
@"static int Main()
{
    partial");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "()";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "()" text from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537020, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537020")]
        [Fact]
        public void IncompleteGlobalQualifierExplInterfaceImpl()
        {
            SourceText oldText = SourceText.From(
@"class Test : N1.I1
{
    int global::N1.");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will add the 'I' character to the end of oldText
            CharByCharIncrementalParse(oldText, 'I', out incrementalTree, out parsedTree);

            // Verify that the fully parsed tree is structurally equivalent to the incrementally parsed tree
            CompareTreeEquivalence(parsedTree.GetCompilationUnitRoot(), incrementalTree.GetCompilationUnitRoot());
        }

        [WorkItem(537033, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537033")]
        [Fact]
        public void RemoveParensInGetEnumeratorWithPropertyAccess()
        {
            SourceText oldText = SourceText.From(
@"public class QueueProducerConsumer
{
    public IEnumerator<T> GetEnumerator()
    {
        while (true)
        {
            if (!value.HasValue)
            {
            }
        }
    }
}
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "()";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "()" text from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the fully parsed tree is structurally equivalent to the incrementally parsed tree
            CompareTreeEquivalence(parsedTree.GetCompilationUnitRoot(), incrementalTree.GetCompilationUnitRoot());
        }

        [WorkItem(537053, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537053")]
        [Fact]
        public void RemoveReturnTypeOnProperty()
        {
            SourceText oldText = SourceText.From(
@"public class Test
{
	public int Prop
	{
		set
		{
			D d = delegate
			{
				Validator(value);
			};
		}
	}
}
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "int";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "int" text from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the fully parsed tree is structurally equivalent to the incrementally parsed tree
            CompareTreeEquivalence(parsedTree.GetCompilationUnitRoot(), incrementalTree.GetCompilationUnitRoot());
        }

        [WorkItem(538975, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538975")]
        [Fact]
        public void RemoveTypeOnArrayInParameterWithMethodDeclError()
        {
            SourceText oldText = SourceText.From(
@"public class A
{
   public void static Main(string[] args)
   {
   }
}
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "string";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "string" text from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537054, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537054")]
        [Fact]
        public void RemoveReturnTypeOnGenericMethodWithTypeParamConstraint()
        {
            SourceText oldText = SourceText.From(
@"class Test
{
    public static int M<U>() where U : IDisposable, new()
    {
    }
}");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "int";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "int" text from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the fully parsed tree is structurally equivalent to the incrementally parsed tree
            CompareTreeEquivalence(parsedTree.GetCompilationUnitRoot(), incrementalTree.GetCompilationUnitRoot());
        }

        [WorkItem(537084, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537084")]
        [Fact]
        public void RemoveNamespaceQualifierFromTypeInIfCondition()
        {
            SourceText oldText = SourceText.From(
@"public class Driver 
{
	public void AddValidations()
	{
		if (typeof(K) is System.ValueType)
		{

		}
	}
}");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "System";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "System" text from oldText
            // The bug is that "Debug.Assert" was thrown by the Incremental Parser with this change
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the fully parsed tree is structurally equivalent to the incrementally parsed tree
            CompareTreeEquivalence(parsedTree.GetCompilationUnitRoot(), incrementalTree.GetCompilationUnitRoot());
        }

        [WorkItem(537092, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537092")]
        [Fact]
        public void RemoveMethodNameWithLambdaExprInMethodBody()
        {
            SourceText oldText = SourceText.From(
@"class C
{
    static int Main()
    {
        M((x, y) => new Pair<int,double>());
    }
}");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "Main";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "Main" text from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);

            // Verify that the fully parsed tree is structurally equivalent to the incrementally parsed tree
            CompareTreeEquivalence(parsedTree.GetCompilationUnitRoot(), incrementalTree.GetCompilationUnitRoot());
        }

        [WorkItem(538978, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538978")]
        [Fact]
        public void RemoveInitializerOnDeclStatementWithErrors()
        {
            SourceText oldText = SourceText.From(
@"x   public static string S = null;
");

            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;
            string textToRemove = "null";
            int locationOfChange = oldText.ToString().IndexOf(textToRemove, StringComparison.Ordinal);
            int widthOfChange = textToRemove.Length;

            // This function will remove the "null" text from oldText
            RemoveText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537116, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537116")]
        [Fact]
        public void CmntEndIfInMethodDecl()
        {
            SourceText oldText = SourceText.From(
@"class Referenced
{

#if PUBLIC
		public
#else
		internal
#endif
			static RecordNotFound Method(){}
}
//<Code>");

            string txtToCmnt = @"internal
#endif
			static RecordNotFound Method(){}";

            int locationOfChange = oldText.ToString().IndexOf(txtToCmnt, StringComparison.Ordinal), widthOfChange = txtToCmnt.Length;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will comment out the txtToCmnt in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537125, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537125")]
        [Fact]
        public void CmntAnonTypeInQueryExpr()
        {
            SourceText oldText = SourceText.From(
@"public class QueryExpressionTest
{
    public static int Main()
    {

        var query7 = from a in b join delegate in c on d equals delegate select new { e, delegate };
        var query13 = from delegate in f join g in h on delegate equals i select delegate;
    }
}");

            string txtToCmnt = @"select new { e, delegate }";

            int locationOfChange = oldText.ToString().IndexOf(txtToCmnt, StringComparison.Ordinal), widthOfChange = txtToCmnt.Length;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will comment out the txtToCmnt in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the errors from the fully parsed tree with the change and the incrementally parsed tree are the same
            CompareIncToFullParseErrors(incrementalTree, parsedTree);
        }

        [WorkItem(537180, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537180")]
        [Fact]
        public void CmntParamsInExtProperty()
        {
            SourceText oldText = SourceText.From(
@"public static class Extensions
{
    public static int Goo2(this int x ) { set { var1 = value; } }
}");

            string txtToCmnt = @"(this int x )";

            int locationOfChange = oldText.ToString().IndexOf(txtToCmnt, StringComparison.Ordinal), widthOfChange = txtToCmnt.Length;
            SyntaxTree incrementalTree;
            SyntaxTree parsedTree;

            // This function will comment out the txtToCmnt in oldText
            CommentOutText(oldText, locationOfChange, widthOfChange, out incrementalTree, out parsedTree);

            // Verify that the fully parsed tree is structurally equivalent to the incrementally parsed tree
            CompareTreeEquivalence(parsedTree.GetCompilationUnitRoot(), incrementalTree.GetCompilationUnitRoot());
        }

        [WorkItem(537533, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537533")]
        [Fact]
        public void MultiCommentInserts()
        {
            var str = @"using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        // abc
        // 123
        // def
        if (true) { } else { }
    }
}";

            var text = SourceText.From(str);
            var tree = SyntaxFactory.ParseSyntaxTree(text);

            var text2 = text.WithChanges(
                new TextChange(new TextSpan(str.IndexOf(" abc", StringComparison.Ordinal), 0), "//"),
                new TextChange(new TextSpan(str.IndexOf(" 123", StringComparison.Ordinal), 0), "//"),
                new TextChange(new TextSpan(str.IndexOf(" def", StringComparison.Ordinal), 0), "//"));

            var parsedTree = SyntaxFactory.ParseSyntaxTree(text2);
            var reparsedTree = tree.WithChangedText(text2);

            CompareTreeEquivalence(parsedTree.GetCompilationUnitRoot(), reparsedTree.GetCompilationUnitRoot());
        }

        [WorkItem(542236, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/542236")]
        [Fact]
        public void InsertOpenBraceBeforeCodes()
        {
            SourceText oldText = SourceText.From(@"
		this.I = i;
	};
}");
            var startTree = SyntaxFactory.ParseSyntaxTree(oldText);

            // first make certain this text round trips
            Assert.Equal(oldText.ToString(), startTree.GetCompilationUnitRoot().ToFullString());
            var newText = oldText.WithChanges(new TextChange(new TextSpan(0, 0), "{"));
            var reparsedTree = startTree.WithChangedText(newText);
            var parsedTree = SyntaxFactory.ParseSyntaxTree(newText);
            CompareIncToFullParseErrors(reparsedTree, parsedTree);
        }

        [WorkItem(6676, "https://github.com/dotnet/roslyn/issues/6676")]
        [Fact]
        public void InsertExpressionStatementWithoutSemicolonBefore()
        {
            SourceText oldText = SourceText.From(@"System.Console.WriteLine(true)
");
            var startTree = SyntaxFactory.ParseSyntaxTree(oldText, options: TestOptions.Script);

            startTree.GetDiagnostics().Verify();

            var newText = oldText.WithChanges(new TextChange(new TextSpan(0, 0), @"System.Console.WriteLine(false)
"));

            AssertEx.AreEqual(@"System.Console.WriteLine(false)
System.Console.WriteLine(true)
",
newText.ToString());

            var reparsedTree = startTree.WithChangedText(newText);
            var parsedTree = SyntaxFactory.ParseSyntaxTree(newText, options: TestOptions.Script);

            parsedTree.GetDiagnostics().Verify(
                // (1,32): error CS1002: ; expected
                // System.Console.WriteLine(false)
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "").WithLocation(1, 32));

            CompareIncToFullParseErrors(reparsedTree, parsedTree);
        }

        [WorkItem(6676, "https://github.com/dotnet/roslyn/issues/6676")]
        [Fact]
        public void InsertExpressionStatementWithoutSemicolonAfter()
        {
            SourceText oldText = SourceText.From(@"System.Console.WriteLine(true)
");
            var startTree = SyntaxFactory.ParseSyntaxTree(oldText, options: TestOptions.Script);

            startTree.GetDiagnostics().Verify();

            var newText = oldText.WithInsertAt(
                oldText.Length,
                @"System.Console.WriteLine(false)
");

            AssertEx.Equal(@"System.Console.WriteLine(true)
System.Console.WriteLine(false)
", newText.ToString());

            var reparsedTree = startTree.WithChangedText(newText);

            var parsedTree = SyntaxFactory.ParseSyntaxTree(newText, options: TestOptions.Script);
            parsedTree.GetDiagnostics().Verify(
                // (1,31): error CS1002: ; expected
                // System.Console.WriteLine(true)
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "").WithLocation(1, 31));

            CompareIncToFullParseErrors(reparsedTree, parsedTree);
        }

        [WorkItem(6676, "https://github.com/dotnet/roslyn/issues/6676")]
        [Fact]
        public void MakeEmbeddedExpressionStatementWithoutSemicolon()
        {
            SourceText oldText = SourceText.From(@"System.Console.WriteLine(true)
");
            var startTree = SyntaxFactory.ParseSyntaxTree(oldText, options: TestOptions.Script);

            startTree.GetDiagnostics().Verify();

            var newText = oldText.WithChanges(new TextChange(new TextSpan(0, 0), @"if (false)
"));

            AssertEx.Equal(@"if (false)
System.Console.WriteLine(true)
", newText.ToString());

            var reparsedTree = startTree.WithChangedText(newText);
            var parsedTree = SyntaxFactory.ParseSyntaxTree(newText, options: TestOptions.Script);

            parsedTree.GetDiagnostics().Verify(
                // (2,31): error CS1002: ; expected
                // System.Console.WriteLine(true)
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "").WithLocation(2, 31));

            CompareIncToFullParseErrors(reparsedTree, parsedTree);
        }

        [WorkItem(531404, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/531404")]
        [Fact]
        public void AppendDisabledText()
        {
            var text =
@"class SmallDictionary
{
    public void Add(int key, int value)
    {
        int hash = key + value;
#if DEBUG";
            var originalTree = this.Parse(text);
            var changedTree = originalTree.WithInsertAt(text.Length, "\r\n        hash++;");
            var parsedTree = this.Parse(changedTree.GetCompilationUnitRoot().ToFullString());

            Assert.Equal(
                parsedTree.GetCompilationUnitRoot().EndOfFileToken.FullSpan,
                changedTree.GetCompilationUnitRoot().EndOfFileToken.FullSpan);
        }

        [Fact, WorkItem(531614, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/531614")]
        public void IncrementalParseStopAtEscapeBackSlash()
        {
            var text1 = @"using System;

class Program
{
    static void Main()
    {
";

            var text2 = @"        Console.WriteLine(""\'\0\a\b\";

            var comp = CSharpTestBase.CreateCompilation(SyntaxFactory.ParseSyntaxTree(String.Empty));

            var oldTree = comp.SyntaxTrees.First();
            var oldIText = oldTree.GetText();
            var span = new TextSpan(oldIText.Length, 0);
            var change = new TextChange(span, text1);

            var newIText = oldIText.WithChanges(change);
            var newTree = oldTree.WithChangedText(newIText);

            var fullTree = SyntaxFactory.ParseSyntaxTree(newIText.ToString(), options: newTree.Options);
            var fullText = fullTree.GetCompilationUnitRoot().ToFullString();
            var incText = newTree.GetCompilationUnitRoot().ToFullString();
            Assert.Equal(fullText.Length, incText.Length);
            Assert.Equal(fullText, incText);
            // 
            oldTree = newTree;
            oldIText = oldTree.GetText();
            span = new TextSpan(oldIText.Length, 0);
            change = new TextChange(span, text2);

            newIText = oldIText.WithChanges(change);
            newTree = oldTree.WithChangedText(newIText);

            fullTree = SyntaxFactory.ParseSyntaxTree(newIText.ToString(), options: newTree.Options);
            fullText = fullTree.GetCompilationUnitRoot().ToFullString();
            incText = newTree.GetCompilationUnitRoot().ToFullString();
            Assert.Equal(fullText.Length, incText.Length);
            Assert.Equal(fullText, incText);
        }

        [Fact, WorkItem(552741, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/552741")]
        public void IncrementalParseTopDownCommentOutLines()
        {
            var text = @"// <Title> Query Expression syntax </Title>
// <Description>
// from, join, on, equals, into, let, orderby, ascending, descending, group, by
// @-with contextual keywords parseable as a type or identifier in a query expression
// Various combinations
// </Description>
// <RelatedBugs></RelatedBugs>

//<Expects status=success></Expects>

// <Code> 

using System;
using System.Linq;

public class from { }
public class join { }
public class on { }
public class equals { }
public class into { }
public class let { }
public class orderby : let { }
public class ascending : orderby, descending { }
public interface descending { }
public class group { }
public class by { }

public class QueryExpressionTest
{
    public static int Main()
    {
        var array02a = new[] { new join(), new join(), new join() } as object[];
        var array02b = new[] { new join(), new join(), new join() } as object[];
        var query02 = from i in array02a join j in array02b on (@from)i equals (@from)j select new { i, j };

        var array03a = new[] { new on(), new on(), new on() } as object[];
        var array03b = new[] { new on(), new on(), new on() } as object[];
        var query03 = from @on i in array03a join j in array03b on i equals (@on)j select new { i, j };

        var array04a = new[] { new equals(), new equals(), new equals() } as object[];

        return 0;
    }
}
";

            var currTree = SyntaxFactory.ParseSyntaxTree(text);
            var currIText = currTree.GetText();

            var items = text.Split('\n');
            int currLen = 0;
            foreach (var item in items)
            {
                var span = new TextSpan(currLen, 0);
                var change = new TextChange(span, "// ");
                currLen += item.Length + 3;

                currIText = currIText.WithChanges(change);
                currTree = currTree.WithChangedText(currIText);

                var fullTree = SyntaxFactory.ParseSyntaxTree(currIText.ToString());

                int incCount = currTree.GetCompilationUnitRoot().ChildNodesAndTokens().Count;
                int fullCount = fullTree.GetCompilationUnitRoot().ChildNodesAndTokens().Count;

                WalkTreeAndVerify(currTree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
            }
        }

        [Fact, WorkItem(552741, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/552741")]
        public void IncrementalParseStatementAfterQuery()
        {
            var text = @"
using System.Linq;
 
class equals
{
    static void Main(string[] args)
    {
        equals[] a;
        var q = from x in args select x;
        a = new[] { new equals() };
    }
}
";

            var currTree = SyntaxFactory.ParseSyntaxTree(text);
            var currIText = currTree.GetText();

            // Insert "// " before the "x" in "select x"; the next statement becomes part of the query.
            var span = new TextSpan(text.LastIndexOf('x'), 0);
            var change = new TextChange(span, "// ");

            currIText = currIText.WithChanges(change);
            currTree = currTree.WithChangedText(currIText);

            var fullTree = SyntaxFactory.ParseSyntaxTree(currIText.ToString());

            WalkTreeAndVerify(currTree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact, WorkItem(529260, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/529260")]
        public void DoNotReuseAnnotatedNodes()
        {
            var text = @"
class C { }
class D { }
";

            Func<SyntaxTree, GreenNode> extractGreenClassC = tree =>
                tree.GetCompilationUnitRoot().Members.First().Green;

            // Check reuse after a trivial change in an unannotated tree.
            {
                var oldTree = SyntaxFactory.ParseSyntaxTree(text);
                var newTree = oldTree.WithInsertAt(text.Length, " ");

                // Class declaration is reused.
                Assert.Same(extractGreenClassC(oldTree), extractGreenClassC(newTree));
            }

            // Check reuse after a trivial change in an annotated tree.
            {
                var tempTree = SyntaxFactory.ParseSyntaxTree(text);
                var tempRoot = tempTree.GetRoot();
                var tempToken = tempRoot.DescendantTokens().First(t => t.Kind() == SyntaxKind.IdentifierToken);
                var oldRoot = tempRoot.ReplaceToken(tempToken, tempToken.WithAdditionalAnnotations(new SyntaxAnnotation()));
                Assert.True(oldRoot.ContainsAnnotations, "Should contain annotations.");
                Assert.Equal(text, oldRoot.ToFullString());

                var oldTree = SyntaxFactory.SyntaxTree(oldRoot, options: tempTree.Options, path: tempTree.FilePath);
                var newTree = oldTree.WithInsertAt(text.Length, " ");

                var oldClassC = extractGreenClassC(oldTree);
                var newClassC = extractGreenClassC(newTree);

                Assert.True(oldClassC.ContainsAnnotations, "Should contain annotations");
                Assert.False(newClassC.ContainsAnnotations, "Annotations should have been removed.");

                // Class declaration is not reused...
                Assert.NotSame(oldClassC, newClassC);
                // ...even though the text is the same.
                Assert.Equal(oldClassC.ToFullString(), newClassC.ToFullString());

                var oldToken = ((Syntax.InternalSyntax.ClassDeclarationSyntax)oldClassC).Identifier;
                var newToken = ((Syntax.InternalSyntax.ClassDeclarationSyntax)newClassC).Identifier;

                Assert.True(oldToken.ContainsAnnotations, "Should contain annotations");
                Assert.False(newToken.ContainsAnnotations, "Annotations should have been removed.");

                // Token is not reused...
                Assert.NotSame(oldToken, newToken);
                // ...even though the text is the same.
                Assert.Equal(oldToken.ToFullString(), newToken.ToFullString());
            }
        }

        [Fact]
        [WorkItem(658496, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/658496")]
        public void DontReuseLambdaParameterAsMethodParameter()
        {
            var items = new string[]
            {
                "a b.c*/ d => {e(f =>",
                "/*",
            };

            var oldText = SourceText.From(items[0]);
            var oldTree = SyntaxFactory.ParseSyntaxTree(oldText); // f is a simple lambda parameter

            var change = new TextChange(new TextSpan(0, 0), items[1]); // Prepend
            var newText = oldText.WithChanges(change); // f is a method decl parameter

            var incrTree = oldTree.WithChangedText(newText);
            var fullTree = SyntaxFactory.ParseSyntaxTree(newText);

            Assert.Equal(
                fullTree.GetDiagnostics().Select(d => d.ToString()),
                incrTree.GetDiagnostics().Select(d => d.ToString()));

            WalkTreeAndVerify(incrTree.GetRoot(), fullTree.GetRoot());
        }

        [Fact]
        public void TestRescanInterpolatedString()
        {
            var interfaceKeyword = SyntaxFactory.ParseToken("interface"); // prime the memoizer

            var text = @"class goo { public void m() { string s = $""{1} world"" ; } }";
            var oldTree = this.Parse6(text);
            var newTree = oldTree.WithReplaceFirst(@"world"" ", @"world""  ");
            Assert.Equal(0, oldTree.GetCompilationUnitRoot().Errors().Length);
            Assert.Equal(0, newTree.GetCompilationUnitRoot().Errors().Length);
        }

        [Fact]
        public void Goo()
        {
            var oldText = SourceText.From(@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {

    }

    protected abstract int Stuff { get; }
}

class G: Program
{
    protected override int Stuff
    {
        get
        {
            throw new NotImplementedException();
        }
    }
}");
            var newText = SourceText.From(@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {

    }

    protected abstract int Stuff { get; }
}

class G: Program
{
    protected override int Stuff =>
    {
        get
        {
            throw new NotImplementedException();
        }
    }
}
");
            var oldTree = SyntaxFactory.ParseSyntaxTree(oldText);
            var newTree = oldTree.WithChangedText(newText);
            WalkTreeAndVerify(newTree.GetCompilationUnitRoot(), SyntaxFactory.ParseSyntaxTree(newText).GetCompilationUnitRoot());
        }

        [WorkItem(23272, "https://github.com/dotnet/roslyn/issues/23272")]
        [Fact]
        public void AddAccessibilityToNullableArray()
        {
            var source =
@"class A { }
class B
{
    A[]? F;
}";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var span = new TextSpan(source.IndexOf(" A[]?"), 0);
            var change = new TextChange(span, "p");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        [WorkItem(37663, "https://github.com/dotnet/roslyn/issues/37663")]
        public void AssemblyAttributeBeforeNamespace()
        {
            var src = @"
using System;
using System.Linq;

[assembly:]
namespace N
{ }";
            var tree = SyntaxFactory.ParseSyntaxTree(src);
            var text = tree.GetText();
            var span = new TextSpan(src.IndexOf(":"), 1);
            var change = new TextChange(span, "");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [WorkItem(37665, "https://github.com/dotnet/roslyn/issues/37665")]
        [Fact]
        public void AddBracketInUsingDirective()
        {
            var source =
@"using System;
namespace NS
{
    class A { }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var span = new TextSpan(source.IndexOf(";"), 0);
            var change = new TextChange(span, "[");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [WorkItem(37665, "https://github.com/dotnet/roslyn/issues/37665")]
        [Fact]
        public void AddAttributeAfterUsingDirective()
        {
            var source =
@"using System;
namespace NS
{
    class A { }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var span = new TextSpan(source.IndexOf(";") + 1, 0);
            var change = new TextChange(span, "[Obsolete]");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [WorkItem(37665, "https://github.com/dotnet/roslyn/issues/37665")]
        [Fact]
        public void AddTrailingModifierInUsingDirective()
        {
            var source =
@"using System;
namespace NS
{
    class A { }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var span = new TextSpan(source.IndexOf(";") + 1, 0);
            var change = new TextChange(span, "public");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [WorkItem(37665, "https://github.com/dotnet/roslyn/issues/37665")]
        [Fact]
        public void AddTrailingModifierInUsingDirective_2()
        {
            var source =
@"using System;publi
namespace NS
{
    class A { }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = "publi";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, "c");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void Statement_EditAttributeList_01()
        {
            var source = @"
class C
{
    void M()
    {
        [Attr]
        void local1() { };
    }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = "Attr";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, "1, Attr2");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void Statement_EditAttributeList_02()
        {
            var source = @"
class C
{
    void M()
    {
        [Attr1]
        Method1();
    }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = @"Attr1";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, ", Attr2");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void Statement_AddAttributeList()
        {
            var source = @"
class C
{
    void M()
    {
        [Attr1]
        void local1() { };
    }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = @"[Attr1]";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, " [Attr2]");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void EditStatementWithAttributes_01()
        {
            var source = @"
class C
{
    void M()
    {
        [Attr1]
        void local1() { Method(); };
    }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = @"Method(";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, "Arg");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void EditStatementWithAttributes_02()
        {
            var source = @"
class C
{
    void M()
    {
        [Attr1]
        Method1();
    }
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = @"Method";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 1);
            var change = new TextChange(span, "2");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Theory]
        [InlineData("[Attr] () => { }")]
        [InlineData("[Attr] x => x")]
        [InlineData("([Attr] x) => x")]
        [InlineData("([Attr] int x) => x")]
        public void Lambda_EditAttributeList(string lambdaExpression)
        {
            var source =
$@"class Program
{{
    static void Main()
    {{
        F({lambdaExpression});
    }}
}}";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = "Attr";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, "1, Attr2");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Theory]
        [InlineData("() => { }", "() => { }")]
        [InlineData("x => x", "x => x")]
        [InlineData("(x) => x", "x) => x")]
        [InlineData("(int x) => x", "int x) => x")]
        public void Lambda_AddFirstAttributeList(string lambdaExpression, string substring)
        {
            var source =
$@"class Program
{{
    static void Main()
    {{
        F({lambdaExpression});
    }}
}}";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var span = new TextSpan(source.IndexOf(substring), 0);
            var change = new TextChange(span, "[Attr]");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Theory]
        [InlineData("[Attr1] () => { }")]
        [InlineData("[Attr1] x => x")]
        [InlineData("([Attr1] x) => x")]
        [InlineData("([Attr1] int x) => x")]
        public void Lambda_AddSecondAttributeList(string lambdaExpression)
        {
            var source =
$@"class Program
{{
    static void Main()
    {{
        F({lambdaExpression});
    }}
}}";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = @"[Attr1]";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, " [Attr2]");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Theory]
        [InlineData("[Attr] () => { }")]
        [InlineData("[Attr] x => x")]
        [InlineData("([Attr] x) => x")]
        [InlineData("([Attr] int x) => x")]
        public void Lambda_RemoveAttributeList(string lambdaExpression)
        {
            var source =
$@"class Program
{{
    static void Main()
    {{
        F({lambdaExpression});
    }}
}}";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = "[Attr] ";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, "");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void EditGlobalStatementWithAttributes_01()
        {
            var source = @"
[Attr]
x.y();
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = @"x.y";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, ".z");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void EditGlobalStatementWithAttributes_02()
        {
            var source = @"
[Attr]
if (b) { }
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = @"if (b) { }";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, " if (c) { }");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void EditGlobalStatementWithAttributes_03()
        {
            var source = @"
[Attr]
if (b) { }
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = @"if (b) { }";
            var span = new TextSpan(source.IndexOf(substring) + substring.Length, 0);
            var change = new TextChange(span, " else (c) { }");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void EditNestedStatementWithAttributes_01()
        {
            var source = "{ [Goo]Goo(); [Goo]Goo(); [Goo]Goo(); }";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var span = new TextSpan(start: 0, length: 1); // delete first character
            var change = new TextChange(span, "");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void EditNestedStatementWithAttributes_02()
        {
            var source = "{ [Goo]Goo(); [Goo]Goo(); [Goo]Goo(); }";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var span = new TextSpan(start: 0, length: 0);
            var change = new TextChange(span, "{ ");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        public void EditNestedStatementWithAttributes_03()
        {
            var source = "class C { void M() { Goo[Goo] [Goo]if(Goo) { } } }";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var substring = "Goo[Goo]";
            var span = new TextSpan(start: source.IndexOf(substring), length: 3); // Goo[Goo] -> [Goo]
            var change = new TextChange(span, "");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact]
        [WorkItem(62126, "https://github.com/dotnet/roslyn/issues/62126")]
        public void StartAttributeOnABlock()
        {
            var source = @"
using System;

switch (getVirtualKey())
{
	case VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right:
	{

	}
}

// A local function to simulate get operation.
static VirtualKey getVirtualKey() => VirtualKey.Up;


enum VirtualKey
{
	Up,
	Down,
	Left,
	Right
}
";
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();
            var span = new TextSpan(start: source.IndexOf(":") + 1, length: 0);
            var change = new TextChange(span, "[");
            text = text.WithChanges(change);
            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/76439")]
        public void InKeywordInsideAForBlock()
        {
            var source = """
                void Main()
                {
                    for (int i = 0; i < n; i++)
                    {
                    }
                }
                """;
            var tree = SyntaxFactory.ParseSyntaxTree(source);
            var text = tree.GetText();

            // Update all the 'i's in the for-loop to be 'in' instead.
            var position1 = source.IndexOf("i =") + 1;
            var position2 = source.IndexOf("i <") + 1;
            var position3 = source.IndexOf("i++") + 1;
            text = text.WithChanges(
                new TextChange(new TextSpan(position1, 0), "n"),
                new TextChange(new TextSpan(position2, 0), "n"),
                new TextChange(new TextSpan(position3, 0), "n"));

            Assert.Equal("""
                void Main()
                {
                    for (int in = 0; in < n; in++)
                    {
                    }
                }
                """, text.ToString());

            tree = tree.WithChangedText(text);
            var fullTree = SyntaxFactory.ParseSyntaxTree(text.ToString());
            WalkTreeAndVerify(tree.GetCompilationUnitRoot(), fullTree.GetCompilationUnitRoot());
        }

        #endregion

        #region Helper functions
        private void WalkTreeAndVerify(SyntaxNodeOrToken incNode, SyntaxNodeOrToken fullNode)
        {
            var incChildren = incNode.ChildNodesAndTokens();
            var fullChildren = fullNode.ChildNodesAndTokens();
            Assert.Equal(incChildren.Count, fullChildren.Count);

            for (int i = 0; i < incChildren.Count; i++)
            {
                var incChild = incChildren[i];
                var fullChild = fullChildren[i];

                WalkTreeAndVerify(incChild, fullChild);
            }
        }

        private static void CommentOutText(SourceText oldText, int locationOfChange, int widthOfChange, out SyntaxTree incrementalTree, out SyntaxTree parsedTree)
        {
            var newText = oldText.WithChanges(
                new TextChange[] {
                    new TextChange(new TextSpan(locationOfChange, 0), "/*"),
                    new TextChange(new TextSpan(locationOfChange + widthOfChange, 0), "*/")
                });
            var tree = SyntaxFactory.ParseSyntaxTree(oldText);
            incrementalTree = tree.WithChangedText(newText);
            parsedTree = SyntaxFactory.ParseSyntaxTree(newText);
        }

        private static void RemoveText(SourceText oldText, int locationOfChange, int widthOfChange, out SyntaxTree incrementalTree, out SyntaxTree parsedTree)
        {
            var newText = oldText.WithChanges(new TextChange(new TextSpan(locationOfChange, widthOfChange), ""));
            var tree = SyntaxFactory.ParseSyntaxTree(oldText);
            incrementalTree = tree.WithChangedText(newText);
            parsedTree = SyntaxFactory.ParseSyntaxTree(newText);
        }

        private void CompareIncToFullParseErrors(SyntaxTree incrementalTree, SyntaxTree parsedTree)
        {
            var pd = parsedTree.GetDiagnostics();
            var id = incrementalTree.GetDiagnostics();
            Assert.Equal(pd.Count(), id.Count());
            for (int i = 0; i < id.Count(); i++)
            {
                Assert.Equal(pd.ElementAt(i).Inspect(), id.ElementAt(i).Inspect());
            }

            ParentChecker.CheckParents(parsedTree.GetCompilationUnitRoot(), parsedTree);
            ParentChecker.CheckParents(incrementalTree.GetCompilationUnitRoot(), incrementalTree);
        }

        private static void CharByCharIncrementalParse(SourceText oldText, char newChar, out SyntaxTree incrementalTree, out SyntaxTree parsedTree)
        {
            var startTree = SyntaxFactory.ParseSyntaxTree(oldText);

            // first make certain this text round trips
            Assert.Equal(oldText.ToString(), startTree.GetCompilationUnitRoot().ToFullString());
            var newText = oldText.WithChanges(new TextChange(new TextSpan(oldText.Length, 0), newChar.ToString()));
            incrementalTree = startTree.WithChangedText(newText);
            parsedTree = SyntaxFactory.ParseSyntaxTree(newText);
        }

        private static void TokenByTokenBottomUp(SourceText oldText, string token, out SyntaxTree incrementalTree, out SyntaxTree parsedTree)
        {
            var startTree = SyntaxFactory.ParseSyntaxTree(oldText);
            SourceText newText = SourceText.From(token + oldText.ToString());
            incrementalTree = startTree.WithInsertAt(0, token);
            parsedTree = SyntaxFactory.ParseSyntaxTree(newText);
        }

        private static void CompareTreeEquivalence(SyntaxNodeOrToken parsedTreeNode, SyntaxNodeOrToken incrementalTreeNode)
        {
            Assert.Equal(parsedTreeNode.Kind(), incrementalTreeNode.Kind());

            Assert.Equal(parsedTreeNode.ChildNodesAndTokens().Count, incrementalTreeNode.ChildNodesAndTokens().Count);

            for (int i = 0; i < parsedTreeNode.ChildNodesAndTokens().Count; i++)
            {
                CompareTreeEquivalence(parsedTreeNode.ChildNodesAndTokens()[i], incrementalTreeNode.ChildNodesAndTokens()[i]);
            }
        }

        #endregion
    }
}

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.UnitTests;
using Microsoft.CodeAnalysis.EditAndContinue;
using Microsoft.CodeAnalysis.EditAndContinue.UnitTests;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.EditAndContinue.UnitTests;

[UseExportProvider]
public sealed class StatementEditingTests : EditingTestBase
{
    private readonly string s_asyncIteratorStateMachineAttributeSource = """

        namespace System.Runtime.CompilerServices
        {
            [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
            public sealed class AsyncIteratorStateMachineAttribute : StateMachineAttribute
            {
                public AsyncIteratorStateMachineAttribute(Type stateMachineType)
                    : base(stateMachineType)
                {
                }
            }
        }

        """;

    #region Strings

    [Fact]
    public void StringLiteral_update()
    {
        var src1 = """

            var x = "Hello1";

            """;
        var src2 = """

            var x = "Hello2";

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [x = \"Hello1\"]@8 -> [x = \"Hello2\"]@8");
    }

    [Fact]
    public void InterpolatedStringText_update()
    {
        var src1 = """

            var x = $"Hello1";

            """;
        var src2 = """

            var x = $"Hello2";

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [x = $\"Hello1\"]@8 -> [x = $\"Hello2\"]@8");
    }

    [Fact]
    public void Interpolation_update()
    {
        var src1 = """

            var x = $"Hello{123}";

            """;
        var src2 = """

            var x = $"Hello{124}";

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [x = $\"Hello{123}\"]@8 -> [x = $\"Hello{124}\"]@8");
    }

    [Fact]
    public void InterpolationFormatClause_update()
    {
        var src1 = """

            var x = $"Hello{123:N1}";

            """;
        var src2 = """

            var x = $"Hello{123:N2}";

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [x = $\"Hello{123:N1}\"]@8 -> [x = $\"Hello{123:N2}\"]@8");
    }

    #endregion

    #region Variable Declaration

    [Fact]
    public void VariableDeclaration_Insert()
    {
        var src1 = "if (x == 1) { x++; }";
        var src2 = "var x = 1; if (x == 1) { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [var x = 1;]@2",
            "Insert [var x = 1]@2",
            "Insert [x = 1]@6");
    }

    [Fact]
    public void VariableDeclaration_Update()
    {
        var src1 = "int x = F(1), y = G(2);";
        var src2 = "int x = F(3), y = G(4);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [x = F(1)]@6 -> [x = F(3)]@6",
            "Update [y = G(2)]@16 -> [y = G(4)]@16");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_Update()
    {
        var src1 = """

            var (x1, (x2, x3)) = (1, (2, true));
            var (a1, a2) = (1, () => { return 7; });

            """;
        var src2 = """

            var (x1, (x2, x4)) = (1, (2, true));
            var (a1, a3) = (1, () => { return 8; });

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [x3]@18 -> [x4]@18",
            "Update [a2]@51 -> [a3]@51");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_Insert()
    {
        var src1 = @"var (z1, z2) = (1, 2);";
        var src2 = @"var (z1, z2, z3) = (1, 2, 5);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [var (z1, z2) = (1, 2);]@2 -> [var (z1, z2, z3) = (1, 2, 5);]@2",
            "Insert [z3]@15");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_Delete()
    {
        var src1 = @"var (y1, y2, y3) = (1, 2, 7);";
        var src2 = @"var (y1, y2) = (1, 4);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [var (y1, y2, y3) = (1, 2, 7);]@2 -> [var (y1, y2) = (1, 4);]@2",
            "Delete [y3]@15");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_Insert_Mixed1()
    {
        var src1 = @"int a; (var z1, a) = (1, 2);";
        var src2 = @"int a; (var z1, a, var z3) = (1, 2, 5);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(var z1, a) = (1, 2);]@9 -> [(var z1, a, var z3) = (1, 2, 5);]@9",
            "Insert [z3]@25");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_Insert_Mixed2()
    {
        var src1 = @"int a; (var z1, var z2) = (1, 2);";
        var src2 = @"int a; (var z1, var z2, a) = (1, 2, 5);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(var z1, var z2) = (1, 2);]@9 -> [(var z1, var z2, a) = (1, 2, 5);]@9");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_Delete_Mixed1()
    {
        var src1 = @"int a; (var y1, var y2, a) = (1, 2, 7);";
        var src2 = @"int a; (var y1, var y2) = (1, 4);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(var y1, var y2, a) = (1, 2, 7);]@9 -> [(var y1, var y2) = (1, 4);]@9");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_Delete_Mixed2()
    {
        var src1 = @"int a; (var y1, a, var y3) = (1, 2, 7);";
        var src2 = @"int a; (var y1, a) = (1, 4);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(var y1, a, var y3) = (1, 2, 7);]@9 -> [(var y1, a) = (1, 4);]@9",
            "Delete [y3]@25");
    }

    [Fact]
    public void VariableDeclaraions_Reorder()
    {
        var src1 = @"var (a, b) = (1, 2); var (c, d) = (3, 4);";
        var src2 = @"var (c, d) = (3, 4); var (a, b) = (1, 2);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [var (c, d) = (3, 4);]@23 -> @2");
    }

    [Fact]
    public void VariableDeclaraions_Reorder_Mixed()
    {
        var src1 = @"int a; (a, int b) = (1, 2); (int c, int d) = (3, 4);";
        var src2 = @"int a; (int c, int d) = (3, 4); (a, int b) = (1, 2);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [(int c, int d) = (3, 4);]@30 -> @9");
    }

    [Fact]
    public void VariableNames_Reorder()
    {
        var src1 = @"var (a, b) = (1, 2);";
        var src2 = @"var (b, a) = (2, 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [var (a, b) = (1, 2);]@2 -> [var (b, a) = (2, 1);]@2",
            "Reorder [b]@10 -> @7");
    }

    [Fact]
    public void VariableNames_Reorder_Mixed()
    {
        var src1 = @"int a; (a, int b) = (1, 2);";
        var src2 = @"int a; (int b, a) = (2, 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(a, int b) = (1, 2);]@9 -> [(int b, a) = (2, 1);]@9");
    }

    [Fact]
    public void VariableNamesAndDeclaraions_Reorder()
    {
        var src1 = @"var (a, b) = (1, 2); var (c, d) = (3, 4);";
        var src2 = @"var (d, c) = (3, 4); var (a, b) = (1, 2);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [var (c, d) = (3, 4);]@23 -> @2",
            "Reorder [d]@31 -> @7");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_Reorder()
    {
        var src1 = @"var (a, (b, c)) = (1, (2, 3));";
        var src2 = @"var ((b, c), a) = ((2, 3), 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [var (a, (b, c)) = (1, (2, 3));]@2 -> [var ((b, c), a) = ((2, 3), 1);]@2",
            "Reorder [a]@7 -> @15");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_DoubleReorder()
    {
        var src1 = @"var (a, (b, c)) = (1, (2, 3));";
        var src2 = @"var ((c, b), a) = ((2, 3), 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [var (a, (b, c)) = (1, (2, 3));]@2 -> [var ((c, b), a) = ((2, 3), 1);]@2",
            "Reorder [b]@11 -> @11",
            "Reorder [c]@14 -> @8");
    }

    [Fact]
    public void ParenthesizedVariableDeclaration_ComplexReorder()
    {
        var src1 = @"var (a, (b, c)) = (1, (2, 3)); var (x, (y, z)) = (4, (5, 6));";
        var src2 = @"var (x, (y, z)) = (4, (5, 6)); var ((c, b), a) = (1, (2, 3)); ";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [var (x, (y, z)) = (4, (5, 6));]@33 -> @2",
            "Update [var (a, (b, c)) = (1, (2, 3));]@2 -> [var ((c, b), a) = (1, (2, 3));]@33",
            "Reorder [b]@11 -> @42",
            "Reorder [c]@14 -> @39");
    }

    #endregion

    #region Switch Statement

    [Fact]
    public void Switch1()
    {
        var src1 = "switch (a) { case 1: f(); break; } switch (b) { case 2: g(); break; }";
        var src2 = "switch (b) { case 2: f(); break; } switch (a) { case 1: g(); break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [switch (b) { case 2: g(); break; }]@37 -> @2",
            "Update [case 1: f(); break;]@15 -> [case 2: f(); break;]@15",
            "Move [case 1: f(); break;]@15 -> @15",
            "Update [case 2: g(); break;]@50 -> [case 1: g(); break;]@50",
            "Move [case 2: g(); break;]@50 -> @50");
    }

    [Fact]
    public void Switch_Case_Reorder()
    {
        var src1 = "switch (expr) { case 1: f(); break;   case 2: case 3: case 4: g(); break; }";
        var src2 = "switch (expr) { case 2: case 3: case 4: g(); break;   case 1: f(); break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [case 2: case 3: case 4: g(); break;]@40 -> @18");
    }

    [Fact]
    public void Switch_Case_Update()
    {
        var src1 = "switch (expr) { case 1: f(); break; }";
        var src2 = "switch (expr) { case 2: f(); break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [case 1: f(); break;]@18 -> [case 2: f(); break;]@18");
    }

    [Fact]
    public void CasePatternLabel_UpdateDelete()
    {
        var src1 = """

            switch(shape)
            {
                case Point p: return 0;
                case Circle c: return 1;
            }

            """;

        var src2 = """

            switch(shape)
            {
                case Circle circle: return 1;
            }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [case Circle c: return 1;]@55 -> [case Circle circle: return 1;]@26",
            "Update [c]@67 -> [circle]@38",
            "Delete [case Point p: return 0;]@26",
            "Delete [case Point p:]@26",
            "Delete [p]@37",
            "Delete [return 0;]@40");
    }

    #endregion

    #region Switch Expression

    [Fact]
    public void MethodUpdate_UpdateSwitchExpression1()
    {
        var src1 = """

            class C
            {
                static int F(int a) => a switch { 0 => 0, _ => 1 };
            }
            """;
        var src2 = """

            class C
            {
                static int F(int a) => a switch { 0 => 0, _ => 2 };
            }
            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits("Update [static int F(int a) => a switch { 0 => 0, _ => 1 };]@18 -> [static int F(int a) => a switch { 0 => 0, _ => 2 };]@18");

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void MethodUpdate_UpdateSwitchExpression2()
    {
        var src1 = """

            class C
            {
                static int F(int a) => a switch { 0 => 0, _ => 1 };
            }
            """;
        var src2 = """

            class C
            {
                static int F(int a) => a switch { 1 => 0, _ => 2 };
            }
            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits("Update [static int F(int a) => a switch { 0 => 0, _ => 1 };]@18 -> [static int F(int a) => a switch { 1 => 0, _ => 2 };]@18");

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void MethodUpdate_UpdateSwitchExpression3()
    {
        var src1 = """

            class C
            {
                static int F(int a) => a switch { 0 => 0, _ => 1 };
            }
            """;
        var src2 = """

            class C
            {
                static int F(int a) => a switch { 0 => 0, 1 => 1, _ => 2 };
            }
            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits("Update [static int F(int a) => a switch { 0 => 0, _ => 1 };]@18 -> [static int F(int a) => a switch { 0 => 0, 1 => 1, _ => 2 };]@18");

        edits.VerifySemanticDiagnostics();
    }

    #endregion

    #region Try Catch Finally

    [Fact]
    public void TryInsert1()
    {
        var src1 = "x++;";
        var src2 = "try { x++; } catch { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [try { x++; } catch { }]@2",
            "Insert [{ x++; }]@6",
            "Insert [catch { }]@15",
            "Move [x++;]@2 -> @8",
            "Insert [{ }]@21");
    }

    [Fact]
    public void TryInsert2()
    {
        var src1 = "{ x++; }";
        var src2 = "try { x++; } catch { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [try { x++; } catch { }]@2",
            "Move [{ x++; }]@2 -> @6",
            "Insert [catch { }]@15",
            "Insert [{ }]@21");
    }

    [Fact]
    public void TryDelete1()
    {
        var src1 = "try { x++; } catch { }";
        var src2 = "x++;";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [x++;]@8 -> @2",
            "Delete [try { x++; } catch { }]@2",
            "Delete [{ x++; }]@6",
            "Delete [catch { }]@15",
            "Delete [{ }]@21");
    }

    [Fact]
    public void TryDelete2()
    {
        var src1 = "try { x++; } catch { }";
        var src2 = "{ x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ x++; }]@6 -> @2",
            "Delete [try { x++; } catch { }]@2",
            "Delete [catch { }]@15",
            "Delete [{ }]@21");
    }

    [Fact]
    public void TryReorder()
    {
        var src1 = "try { x++; } catch { /*1*/ } try { y++; } catch { /*2*/ }";
        var src2 = "try { y++; } catch { /*2*/ } try { x++; } catch { /*1*/ } ";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [try { y++; } catch { /*2*/ }]@31 -> @2");
    }

    [Fact]
    public void Finally_DeleteHeader()
    {
        var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } finally { /*3*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ /*3*/ }]@47 -> @39",
            "Delete [finally { /*3*/ }]@39");
    }

    [Fact]
    public void Finally_InsertHeader()
    {
        var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } finally { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [finally { /*3*/ }]@39",
            "Move [{ /*3*/ }]@39 -> @47");
    }

    [Fact]
    public void CatchUpdate()
    {
        var src1 = "try { } catch (Exception e) { }";
        var src2 = "try { } catch (IOException e) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(Exception e)]@16 -> [(IOException e)]@16");
    }

    [Fact]
    public void CatchInsert()
    {
        var src1 = "try { /*1*/ } catch (Exception e) { /*2*/ } ";
        var src2 = "try { /*1*/ } catch (IOException e) { /*3*/ } catch (Exception e) { /*2*/ } ";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [catch (IOException e) { /*3*/ }]@16",
            "Insert [(IOException e)]@22",
            "Insert [{ /*3*/ }]@38");
    }

    [Fact]
    public void CatchBodyUpdate()
    {
        var src1 = "try { } catch (E e) { x++; }";
        var src2 = "try { } catch (E e) { y++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [x++;]@24 -> [y++;]@24");
    }

    [Fact]
    public void CatchDelete()
    {
        var src1 = "try { } catch (IOException e) { } catch (Exception e) { } ";
        var src2 = "try { } catch (IOException e) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [catch (Exception e) { }]@36",
            "Delete [(Exception e)]@42",
            "Delete [{ }]@56");
    }

    [Fact]
    public void CatchReorder1()
    {
        var src1 = "try { } catch (IOException e) { } catch (Exception e) { } ";
        var src2 = "try { } catch (Exception e) { } catch (IOException e) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [catch (Exception e) { }]@36 -> @10");
    }

    [Fact]
    public void CatchReorder2()
    {
        var src1 = "try { } catch (IOException e) { } catch (Exception e) { } catch { }";
        var src2 = "try { } catch (A e) { } catch (Exception e) { } catch (IOException e) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [catch (Exception e) { }]@36 -> @26",
            "Reorder [catch { }]@60 -> @10",
            "Insert [(A e)]@16");
    }

    [Fact]
    public void CatchFilterReorder2()
    {
        var src1 = "try { } catch (Exception e) when (e != null) { } catch (Exception e) { } catch { }";
        var src2 = "try { } catch when (s == 1) { } catch (Exception e) { } catch (Exception e) when (e != null) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [catch (Exception e) { }]@51 -> @34",
            "Reorder [catch { }]@75 -> @10",
            "Insert [when (s == 1)]@16");
    }

    [Fact]
    public void CatchInsertDelete()
    {
        var src1 = """

            try { x++; } catch (E e) { /*1*/ } catch (Exception e) { /*2*/ } 
            try { Console.WriteLine(); } finally { /*3*/ }
            """;

        var src2 = """

            try { x++; } catch (Exception e) { /*2*/ }  
            try { Console.WriteLine(); } catch (E e) { /*1*/ } finally { /*3*/ }
            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [catch (E e) { /*1*/ }]@79",
            "Insert [(E e)]@85",
            "Move [{ /*1*/ }]@29 -> @91",
            "Delete [catch (E e) { /*1*/ }]@17",
            "Delete [(E e)]@23");
    }

    [Fact]
    public void Catch_DeleteHeader1()
    {
        var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } catch (E2 e) { /*3*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ /*3*/ }]@52 -> @39",
            "Delete [catch (E2 e) { /*3*/ }]@39",
            "Delete [(E2 e)]@45");
    }

    [Fact]
    public void Catch_InsertHeader1()
    {
        var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } catch (E2 e) { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [catch (E2 e) { /*3*/ }]@39",
            "Insert [(E2 e)]@45",
            "Move [{ /*3*/ }]@39 -> @52");
    }

    [Fact]
    public void Catch_DeleteHeader2()
    {
        var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } catch { /*3*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ /*3*/ }]@45 -> @39",
            "Delete [catch { /*3*/ }]@39");
    }

    [Fact]
    public void Catch_InsertHeader2()
    {
        var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } catch { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [catch { /*3*/ }]@39",
            "Move [{ /*3*/ }]@39 -> @45");
    }

    [Fact]
    public void Catch_InsertFilter1()
    {
        var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [when (e == null)]@29");
    }

    [Fact]
    public void Catch_InsertFilter2()
    {
        var src1 = "try { /*1*/ } catch when (e == null) { /*2*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [(E1 e)]@22");
    }

    [Fact]
    public void Catch_InsertFilter3()
    {
        var src1 = "try { /*1*/ } catch { /*2*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [(E1 e)]@22",
            "Insert [when (e == null)]@29");
    }

    [Fact]
    public void Catch_DeleteDeclaration1()
    {
        var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ }";
        var src2 = "try { /*1*/ } catch { /*2*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [(E1 e)]@22");
    }

    [Fact]
    public void Catch_DeleteFilter1()
    {
        var src1 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";
        var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [when (e == null)]@29");
    }

    [Fact]
    public void Catch_DeleteFilter2()
    {
        var src1 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";
        var src2 = "try { /*1*/ } catch when (e == null) { /*2*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [(E1 e)]@22");
    }

    [Fact]
    public void Catch_DeleteFilter3()
    {
        var src1 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";
        var src2 = "try { /*1*/ } catch { /*2*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [(E1 e)]@22",
            "Delete [when (e == null)]@29");
    }

    [Fact]
    public void TryCatchFinally_DeleteHeader()
    {
        var src1 = "try { /*1*/ } catch { /*2*/ } finally { /*3*/ }";
        var src2 = "{ /*1*/ } { /*2*/ } { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ /*1*/ }]@6 -> @2",
            "Move [{ /*2*/ }]@22 -> @12",
            "Move [{ /*3*/ }]@40 -> @22",
            "Delete [try { /*1*/ } catch { /*2*/ } finally { /*3*/ }]@2",
            "Delete [catch { /*2*/ }]@16",
            "Delete [finally { /*3*/ }]@32");
    }

    [Fact]
    public void TryCatchFinally_InsertHeader()
    {
        var src1 = "{ /*1*/ } { /*2*/ } { /*3*/ }";
        var src2 = "try { /*1*/ } catch { /*2*/ } finally { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [try { /*1*/ } catch { /*2*/ } finally { /*3*/ }]@2",
            "Move [{ /*1*/ }]@2 -> @6",
            "Insert [catch { /*2*/ }]@16",
            "Insert [finally { /*3*/ }]@32",
            "Move [{ /*2*/ }]@12 -> @22",
            "Move [{ /*3*/ }]@22 -> @40");
    }

    [Fact]
    public void TryFilterFinally_InsertHeader()
    {
        var src1 = "{ /*1*/ } if (a == 1) { /*2*/ } { /*3*/ }";
        var src2 = "try { /*1*/ } catch when (a == 1) { /*2*/ } finally { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [try { /*1*/ } catch when (a == 1) { /*2*/ } finally { /*3*/ }]@2",
            "Move [{ /*1*/ }]@2 -> @6",
            "Insert [catch when (a == 1) { /*2*/ }]@16",
            "Insert [finally { /*3*/ }]@46",
            "Insert [when (a == 1)]@22",
            "Move [{ /*2*/ }]@24 -> @36",
            "Move [{ /*3*/ }]@34 -> @54",
            "Delete [if (a == 1) { /*2*/ }]@12");
    }

    #endregion

    #region Blocks

    [Fact]
    public void Block_Insert()
    {
        var src1 = "";
        var src2 = "{ x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [{ x++; }]@2",
            "Insert [x++;]@4");
    }

    [Fact]
    public void Block_Delete()
    {
        var src1 = "{ x++; }";
        var src2 = "";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [{ x++; }]@2",
            "Delete [x++;]@4");
    }

    [Fact]
    public void Block_Reorder()
    {
        var src1 = "{ x++; } { y++; }";
        var src2 = "{ y++; } { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [{ y++; }]@11 -> @2");
    }

    [Fact]
    public void Block_AddLine()
    {
        var src1 = "{ x++; }";
        var src2 = """
            { //
                                        x++; }
            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits();
    }

    #endregion

    #region Checked/Unchecked

    [Fact]
    public void Checked_Insert()
    {
        var src1 = "";
        var src2 = "checked { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [checked { x++; }]@2",
            "Insert [{ x++; }]@10",
            "Insert [x++;]@12");
    }

    [Fact]
    public void Checked_Delete()
    {
        var src1 = "checked { x++; }";
        var src2 = "";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [checked { x++; }]@2",
            "Delete [{ x++; }]@10",
            "Delete [x++;]@12");
    }

    [Fact]
    public void Checked_Update()
    {
        var src1 = "checked { x++; }";
        var src2 = "unchecked { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [checked { x++; }]@2 -> [unchecked { x++; }]@2");
    }

    [Fact]
    public void Checked_DeleteHeader()
    {
        var src1 = "checked { x++; }";
        var src2 = "{ x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ x++; }]@10 -> @2",
            "Delete [checked { x++; }]@2");
    }

    [Fact]
    public void Checked_InsertHeader()
    {
        var src1 = "{ x++; }";
        var src2 = "checked { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [checked { x++; }]@2",
            "Move [{ x++; }]@2 -> @10");
    }

    [Fact]
    public void Unchecked_InsertHeader()
    {
        var src1 = "{ x++; }";
        var src2 = "unchecked { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [unchecked { x++; }]@2",
            "Move [{ x++; }]@2 -> @12");
    }

    #endregion

    #region Unsafe

    [Fact]
    public void Unsafe_Insert()
    {
        var src1 = "";
        var src2 = "unsafe { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [unsafe { x++; }]@2",
            "Insert [{ x++; }]@9",
            "Insert [x++;]@11");
    }

    [Fact]
    public void Unsafe_Delete()
    {
        var src1 = "unsafe { x++; }";
        var src2 = "";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [unsafe { x++; }]@2",
            "Delete [{ x++; }]@9",
            "Delete [x++;]@11");
    }

    [Fact]
    public void Unsafe_DeleteHeader()
    {
        var src1 = "unsafe { x++; }";
        var src2 = "{ x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ x++; }]@9 -> @2",
            "Delete [unsafe { x++; }]@2");
    }

    [Fact]
    public void Unsafe_InsertHeader()
    {
        var src1 = "{ x++; }";
        var src2 = "unsafe { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [unsafe { x++; }]@2",
            "Move [{ x++; }]@2 -> @9");
    }

    #endregion

    #region Using Statement

    [Fact]
    public void Using1()
    {
        var src1 = @"using (a) { using (b) { Goo(); } }";
        var src2 = @"using (a) { using (c) { using (b) { Goo(); } } }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [using (c) { using (b) { Goo(); } }]@14",
            "Insert [{ using (b) { Goo(); } }]@24",
            "Move [using (b) { Goo(); }]@14 -> @26");
    }

    [Fact]
    public void Using_DeleteHeader()
    {
        var src1 = @"using (a) { Goo(); }";
        var src2 = @"{ Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(); }]@12 -> @2",
            "Delete [using (a) { Goo(); }]@2");
    }

    [Fact]
    public void Using_InsertHeader()
    {
        var src1 = @"{ Goo(); }";
        var src2 = @"using (a) { Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [using (a) { Goo(); }]@2",
            "Move [{ Goo(); }]@2 -> @12");
    }

    #endregion

    #region Lock Statement

    [Fact]
    public void Lock1()
    {
        var src1 = @"lock (a) { lock (b) { Goo(); } }";
        var src2 = @"lock (a) { lock (c) { lock (b) { Goo(); } } }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [lock (c) { lock (b) { Goo(); } }]@13",
            "Insert [{ lock (b) { Goo(); } }]@22",
            "Move [lock (b) { Goo(); }]@13 -> @24");
    }

    [Fact]
    public void Lock_DeleteHeader()
    {
        var src1 = @"lock (a) { Goo(); }";
        var src2 = @"{ Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(); }]@11 -> @2",
            "Delete [lock (a) { Goo(); }]@2");
    }

    [Fact]
    public void Lock_InsertHeader()
    {
        var src1 = @"{ Goo(); }";
        var src2 = @"lock (a) { Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [lock (a) { Goo(); }]@2",
            "Move [{ Goo(); }]@2 -> @11");
    }

    #endregion

    #region ForEach Statement

    [Fact]
    public void ForEach1()
    {
        var src1 = @"foreach (var a in e) { foreach (var b in f) { Goo(); } }";
        var src2 = @"foreach (var a in e) { foreach (var c in g) { foreach (var b in f) { Goo(); } } }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [foreach (var c in g) { foreach (var b in f) { Goo(); } }]@25",
            "Insert [{ foreach (var b in f) { Goo(); } }]@46",
            "Move [foreach (var b in f) { Goo(); }]@25 -> @48");

        var actual = ToMatchingPairs(edits.Match);

        var expected = new MatchingPairs
        {
            { "foreach (var a in e) { foreach (var b in f) { Goo(); } }", "foreach (var a in e) { foreach (var c in g) { foreach (var b in f) { Goo(); } } }" },
            { "{ foreach (var b in f) { Goo(); } }", "{ foreach (var c in g) { foreach (var b in f) { Goo(); } } }" },
            { "foreach (var b in f) { Goo(); }", "foreach (var b in f) { Goo(); }" },
            { "{ Goo(); }", "{ Goo(); }" },
            { "Goo();", "Goo();" }
        };

        expected.AssertEqual(actual);
    }

    [Fact]
    public void ForEach_Swap1()
    {
        var src1 = @"foreach (var a in e) { foreach (var b in f) { Goo(); } }";
        var src2 = @"foreach (var b in f) { foreach (var a in e) { Goo(); } }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [foreach (var b in f) { Goo(); }]@25 -> @2",
            "Move [foreach (var a in e) { foreach (var b in f) { Goo(); } }]@2 -> @25",
            "Move [Goo();]@48 -> @48");

        var actual = ToMatchingPairs(edits.Match);

        var expected = new MatchingPairs
        {
            { "foreach (var a in e) { foreach (var b in f) { Goo(); } }", "foreach (var a in e) { Goo(); }" },
            { "{ foreach (var b in f) { Goo(); } }", "{ Goo(); }" },
            { "foreach (var b in f) { Goo(); }", "foreach (var b in f) { foreach (var a in e) { Goo(); } }" },
            { "{ Goo(); }", "{ foreach (var a in e) { Goo(); } }" },
            { "Goo();", "Goo();" }
        };

        expected.AssertEqual(actual);
    }

    [Fact]
    public void Foreach_DeleteHeader()
    {
        var src1 = @"foreach (var a in b) { Goo(); }";
        var src2 = @"{ Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(); }]@23 -> @2",
            "Delete [foreach (var a in b) { Goo(); }]@2");
    }

    [Fact]
    public void Foreach_InsertHeader()
    {
        var src1 = @"{ Goo(); }";
        var src2 = @"foreach (var a in b) { Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [foreach (var a in b) { Goo(); }]@2",
            "Move [{ Goo(); }]@2 -> @23");
    }

    [Fact]
    public void ForeachVariable_Update1()
    {
        var src1 = """

            foreach (var (a1, a2) in e) { }
            foreach ((var b1, var b2) in e) { }
            foreach (var a in e1) { }

            """;

        var src2 = """

            foreach (var (a1, a3) in e) { }
            foreach ((var b3, int b2) in e) { }
            foreach (_ in e1) { }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [foreach ((var b1, var b2) in e) { }]@37 -> [foreach ((var b3, int b2) in e) { }]@37",
            "Update [foreach (var a in e1) { }]@74 -> [foreach (_ in e1) { }]@74",
            "Update [a2]@22 -> [a3]@22",
            "Update [b1]@51 -> [b3]@51");
    }

    [Fact]
    public void ForeachVariable_Update2()
    {
        var src1 = """

            foreach (_ in e2) { }
            foreach (_ in e3) {  A(); }

            """;

        var src2 = """

            foreach (var b in e2) { }
            foreach (_ in e4) { A(); }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [foreach (_ in e2) { }]@4 -> [foreach (var b in e2) { }]@4",
            "Update [foreach (_ in e3) {  A(); }]@27 -> [foreach (_ in e4) { A(); }]@31");
    }

    [Fact]
    public void ForeachVariable_Insert()
    {
        var src1 = """

            foreach (var (a3, a4) in e) { }
            foreach ((var b4, var b5) in e) { }

            """;

        var src2 = """

            foreach (var (a3, a5, a4) in e) { }
            foreach ((var b6, var b4, var b5) in e) { }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [foreach (var (a3, a4) in e) { }]@4 -> [foreach (var (a3, a5, a4) in e) { }]@4",
            "Update [foreach ((var b4, var b5) in e) { }]@37 -> [foreach ((var b6, var b4, var b5) in e) { }]@41",
            "Insert [a5]@22",
            "Insert [b6]@55");
    }

    [Fact]
    public void ForeachVariable_Delete()
    {
        var src1 = """

            foreach (var (a11, a12, a13) in e) { F(); }
            foreach ((var b7, var b8, var b9) in e) { G(); }

            """;

        var src2 = """

            foreach (var (a12, a13) in e1) { F(); }
            foreach ((var b7, var b9) in e) { G(); }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [foreach (var (a11, a12, a13) in e) { F(); }]@4 -> [foreach (var (a12, a13) in e1) { F(); }]@4",
            "Update [foreach ((var b7, var b8, var b9) in e) { G(); }]@49 -> [foreach ((var b7, var b9) in e) { G(); }]@45",
            "Delete [a11]@18",
            "Delete [b8]@71");
    }

    [Fact]
    public void ForeachVariable_Reorder()
    {
        var src1 = """

            foreach (var (a, b) in e1) { }
            foreach ((var x, var y) in e2) { }

            """;

        var src2 = """

            foreach ((var x, var y) in e2) { }
            foreach (var (a, b) in e1) { }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [foreach ((var x, var y) in e2) { }]@36 -> @4");
    }

    [Fact]
    public void ForeachVariableEmbedded_Reorder()
    {
        var src1 = """

            foreach (var (a, b) in e1) { 
                foreach ((var x, var y) in e2) { }
            }

            """;

        var src2 = """

            foreach ((var x, var y) in e2) { }
            foreach (var (a, b) in e1) { }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [foreach ((var x, var y) in e2) { }]@39 -> @4");
    }

    #endregion

    #region For Statement

    [Fact]
    public void For1()
    {
        var src1 = @"for (int a = 0; a < 10; a++) { for (int a = 0; a < 20; a++) { Goo(); } }";
        var src2 = @"for (int a = 0; a < 10; a++) { for (int b = 0; b < 10; b++) { for (int a = 0; a < 20; a++) { Goo(); } } }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [for (int b = 0; b < 10; b++) { for (int a = 0; a < 20; a++) { Goo(); } }]@33",
            "Insert [int b = 0]@38",
            "Insert [b < 10]@49",
            "Insert [b++]@57",
            "Insert [{ for (int a = 0; a < 20; a++) { Goo(); } }]@62",
            "Insert [b = 0]@42",
            "Move [for (int a = 0; a < 20; a++) { Goo(); }]@33 -> @64");
    }

    [Fact]
    public void For_DeleteHeader()
    {
        var src1 = @"for (int i = 10, j = 0; i > j; i--, j++) { Goo(); }";
        var src2 = @"{ Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(); }]@43 -> @2",
            "Delete [for (int i = 10, j = 0; i > j; i--, j++) { Goo(); }]@2",
            "Delete [int i = 10, j = 0]@7",
            "Delete [i = 10]@11",
            "Delete [j = 0]@19",
            "Delete [i > j]@26",
            "Delete [i--]@33",
            "Delete [j++]@38");
    }

    [Fact]
    public void For_InsertHeader()
    {
        var src1 = @"{ Goo(); }";
        var src2 = @"for (int i = 10, j = 0; i > j; i--, j++) { Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [for (int i = 10, j = 0; i > j; i--, j++) { Goo(); }]@2",
            "Insert [int i = 10, j = 0]@7",
            "Insert [i > j]@26",
            "Insert [i--]@33",
            "Insert [j++]@38",
            "Move [{ Goo(); }]@2 -> @43",
            "Insert [i = 10]@11",
            "Insert [j = 0]@19");
    }

    [Fact]
    public void For_DeclaratorsToInitializers()
    {
        var src1 = @"for (var i = 10; i < 10; i++) { }";
        var src2 = @"for (i = 10; i < 10; i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [i = 10]@7",
            "Delete [var i = 10]@7",
            "Delete [i = 10]@11");
    }

    [Fact]
    public void For_InitializersToDeclarators()
    {
        var src1 = @"for (i = 10, j = 0; i < 10; i++) { }";
        var src2 = @"for (var i = 10, j = 0; i < 10; i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [var i = 10, j = 0]@7",
            "Insert [i = 10]@11",
            "Insert [j = 0]@19",
            "Delete [i = 10]@7",
            "Delete [j = 0]@15");
    }

    [Fact]
    public void For_Declarations_Reorder()
    {
        var src1 = @"for (var i = 10, j = 0; i > j; i++, j++) { }";
        var src2 = @"for (var j = 0, i = 10; i > j; i++, j++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Reorder [j = 0]@19 -> @11");
    }

    [Fact]
    public void For_Declarations_Insert()
    {
        var src1 = @"for (var i = 0, j = 1; i > j; i++, j++) { }";
        var src2 = @"for (var i = 0, j = 1, k = 2; i > j; i++, j++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [var i = 0, j = 1]@7 -> [var i = 0, j = 1, k = 2]@7",
            "Insert [k = 2]@25");
    }

    [Fact]
    public void For_Declarations_Delete()
    {
        var src1 = @"for (var i = 0, j = 1, k = 2; i > j; i++, j++) { }";
        var src2 = @"for (var i = 0, j = 1; i > j; i++, j++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [var i = 0, j = 1, k = 2]@7 -> [var i = 0, j = 1]@7",
            "Delete [k = 2]@25");
    }

    [Fact]
    public void For_Initializers_Reorder()
    {
        var src1 = @"for (i = 10, j = 0; i > j; i++, j++) { }";
        var src2 = @"for (j = 0, i = 10; i > j; i++, j++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Reorder [j = 0]@15 -> @7");
    }

    [Fact]
    public void For_Initializers_Insert()
    {
        var src1 = @"for (i = 10; i < 10; i++) { }";
        var src2 = @"for (i = 10, j = 0; i < 10; i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Insert [j = 0]@15");
    }

    [Fact]
    public void For_Initializers_Delete()
    {
        var src1 = @"for (i = 10, j = 0; i < 10; i++) { }";
        var src2 = @"for (i = 10; i < 10; i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Delete [j = 0]@15");
    }

    [Fact]
    public void For_Initializers_Update()
    {
        var src1 = @"for (i = 1; i < 10; i++) { }";
        var src2 = @"for (i = 2; i < 10; i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [i = 1]@7 -> [i = 2]@7");
    }

    [Fact]
    public void For_Initializers_Update_Lambda()
    {
        var src1 = @"for (int i = 10, j = F(() => 1); i > j; i++) { }";
        var src2 = @"for (int i = 10, j = F(() => 2); i > j; i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [() => 1]@25 -> [() => 2]@25");
    }

    [Fact]
    public void For_Condition_Update()
    {
        var src1 = @"for (int i = 0; i < 10; i++) { }";
        var src2 = @"for (int i = 0; i < 20; i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [i < 10]@18 -> [i < 20]@18");
    }

    [Fact]
    public void For_Condition_Lambda()
    {
        var src1 = @"for (int i = 0; F(() => 1); i++) { }";
        var src2 = @"for (int i = 0; F(() => 2); i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [() => 1]@20 -> [() => 2]@20");
    }

    [Fact]
    public void For_Incrementors_Reorder()
    {
        var src1 = @"for (int i = 10, j = 0; i > j; i--, j++) { }";
        var src2 = @"for (int i = 10, j = 0; i > j; j++, i--) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Reorder [j++]@38 -> @33");
    }

    [Fact]
    public void For_Incrementors_Insert()
    {
        var src1 = @"for (int i = 10, j = 0; i > j; i--) { }";
        var src2 = @"for (int i = 10, j = 0; i > j; j++, i--) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Insert [j++]@33");
    }

    [Fact]
    public void For_Incrementors_Delete()
    {
        var src1 = @"for (int i = 10, j = 0; i > j; j++, i--) { }";
        var src2 = @"for (int i = 10, j = 0; i > j; j++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Delete [i--]@38");
    }

    [Fact]
    public void For_Incrementors_Update()
    {
        var src1 = @"for (int i = 10, j = 0; i > j; j++) { }";
        var src2 = @"for (int i = 10, j = 0; i > j; i++) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [j++]@33 -> [i++]@33");
    }

    [Fact]
    public void For_Incrementors_Update_Lambda()
    {
        var src1 = @"for (int i = 10, j = 0; i > j; F(() => 1)) { }";
        var src2 = @"for (int i = 10, j = 0; i > j; F(() => 2)) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [() => 1]@35 -> [() => 2]@35");
    }

    #endregion

    #region While Statement

    [Fact]
    public void While1()
    {
        var src1 = @"while (a) { while (b) { Goo(); } }";
        var src2 = @"while (a) { while (c) { while (b) { Goo(); } } }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [while (c) { while (b) { Goo(); } }]@14",
            "Insert [{ while (b) { Goo(); } }]@24",
            "Move [while (b) { Goo(); }]@14 -> @26");
    }

    [Fact]
    public void While_DeleteHeader()
    {
        var src1 = @"while (a) { Goo(); }";
        var src2 = @"{ Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(); }]@12 -> @2",
            "Delete [while (a) { Goo(); }]@2");
    }

    [Fact]
    public void While_InsertHeader()
    {
        var src1 = @"{ Goo(); }";
        var src2 = @"while (a) { Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [while (a) { Goo(); }]@2",
            "Move [{ Goo(); }]@2 -> @12");
    }

    #endregion

    #region Do Statement

    [Fact]
    public void Do1()
    {
        var src1 = @"do { do { Goo(); } while (b); } while (a);";
        var src2 = @"do { do { do { Goo(); } while(b); } while(c); } while(a);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [do { do { Goo(); } while(b); } while(c);]@7",
            "Insert [{ do { Goo(); } while(b); }]@10",
            "Move [do { Goo(); } while (b);]@7 -> @12");
    }

    [Fact]
    public void Do_DeleteHeader()
    {
        var src1 = @"do { Goo(); } while (a);";
        var src2 = @"{ Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(); }]@5 -> @2",
            "Delete [do { Goo(); } while (a);]@2");
    }

    [Fact]
    public void Do_InsertHeader()
    {
        var src1 = @"{ Goo(); }";
        var src2 = @"do { Goo(); } while (a);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [do { Goo(); } while (a);]@2",
            "Move [{ Goo(); }]@2 -> @5");
    }

    #endregion

    #region If Statement

    [Fact]
    public void IfStatement_TestExpression_Update()
    {
        var src1 = "var x = 1; if (x == 1) { x++; }";
        var src2 = "var x = 1; if (x == 2) { x++; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [if (x == 1) { x++; }]@13 -> [if (x == 2) { x++; }]@13");
    }

    [Fact]
    public void ElseClause_Insert()
    {
        var src1 = "if (x == 1) x++; ";
        var src2 = "if (x == 1) x++; else y++;";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [else y++;]@19",
            "Insert [y++;]@24");
    }

    [Fact]
    public void ElseClause_InsertMove()
    {
        var src1 = "if (x == 1) x++; else y++;";
        var src2 = "if (x == 1) x++; else if (x == 2) y++;";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [if (x == 2) y++;]@24",
            "Move [y++;]@24 -> @36");
    }

    [Fact]
    public void If1()
    {
        var src1 = @"if (a) if (b) Goo();";
        var src2 = @"if (a) if (c) if (b) Goo();";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [if (c) if (b) Goo();]@9",
            "Move [if (b) Goo();]@9 -> @16");
    }

    [Fact]
    public void If_DeleteHeader()
    {
        var src1 = @"if (a) { Goo(); }";
        var src2 = @"{ Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(); }]@9 -> @2",
            "Delete [if (a) { Goo(); }]@2");
    }

    [Fact]
    public void If_InsertHeader()
    {
        var src1 = @"{ Goo(); }";
        var src2 = @"if (a) { Goo(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [if (a) { Goo(); }]@2",
            "Move [{ Goo(); }]@2 -> @9");
    }

    [Fact]
    public void Else_DeleteHeader()
    {
        var src1 = @"if (a) { Goo(/*1*/); } else { Goo(/*2*/); }";
        var src2 = @"if (a) { Goo(/*1*/); } { Goo(/*2*/); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(/*2*/); }]@30 -> @25",
            "Delete [else { Goo(/*2*/); }]@25");
    }

    [Fact]
    public void Else_InsertHeader()
    {
        var src1 = @"if (a) { Goo(/*1*/); } { Goo(/*2*/); }";
        var src2 = @"if (a) { Goo(/*1*/); } else { Goo(/*2*/); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [else { Goo(/*2*/); }]@25",
            "Move [{ Goo(/*2*/); }]@25 -> @30");
    }

    [Fact]
    public void ElseIf_DeleteHeader()
    {
        var src1 = @"if (a) { Goo(/*1*/); } else if (b) { Goo(/*2*/); }";
        var src2 = @"if (a) { Goo(/*1*/); } { Goo(/*2*/); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ Goo(/*2*/); }]@37 -> @25",
            "Delete [else if (b) { Goo(/*2*/); }]@25",
            "Delete [if (b) { Goo(/*2*/); }]@30");
    }

    [Fact]
    public void ElseIf_InsertHeader()
    {
        var src1 = @"if (a) { Goo(/*1*/); } { Goo(/*2*/); }";
        var src2 = @"if (a) { Goo(/*1*/); } else if (b) { Goo(/*2*/); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [else if (b) { Goo(/*2*/); }]@25",
            "Insert [if (b) { Goo(/*2*/); }]@30",
            "Move [{ Goo(/*2*/); }]@25 -> @37");
    }

    [Fact]
    public void IfElseElseIf_InsertHeader()
    {
        var src1 = @"{ /*1*/ } { /*2*/ } { /*3*/ }";
        var src2 = @"if (a) { /*1*/ } else if (b) { /*2*/ } else { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [if (a) { /*1*/ } else if (b) { /*2*/ } else { /*3*/ }]@2",
            "Move [{ /*1*/ }]@2 -> @9",
            "Insert [else if (b) { /*2*/ } else { /*3*/ }]@19",
            "Insert [if (b) { /*2*/ } else { /*3*/ }]@24",
            "Move [{ /*2*/ }]@12 -> @31",
            "Insert [else { /*3*/ }]@41",
            "Move [{ /*3*/ }]@22 -> @46");
    }

    [Fact]
    public void IfElseElseIf_DeleteHeader()
    {
        var src1 = @"if (a) { /*1*/ } else if (b) { /*2*/ } else { /*3*/ }";
        var src2 = @"{ /*1*/ } { /*2*/ } { /*3*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Move [{ /*1*/ }]@9 -> @2",
            "Move [{ /*2*/ }]@31 -> @12",
            "Move [{ /*3*/ }]@46 -> @22",
            "Delete [if (a) { /*1*/ } else if (b) { /*2*/ } else { /*3*/ }]@2",
            "Delete [else if (b) { /*2*/ } else { /*3*/ }]@19",
            "Delete [if (b) { /*2*/ } else { /*3*/ }]@24",
            "Delete [else { /*3*/ }]@41");
    }

    #endregion

    #region Switch Statement

    [Fact]
    public void SwitchStatement_Update_Expression()
    {
        var src1 = "var x = 1; switch (x + 1) { case 1: break; }";
        var src2 = "var x = 1; switch (x + 2) { case 1: break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [switch (x + 1) { case 1: break; }]@13 -> [switch (x + 2) { case 1: break; }]@13");
    }

    [Fact]
    public void SwitchStatement_Update_SectionLabel()
    {
        var src1 = "var x = 1; switch (x) { case 1: break; }";
        var src2 = "var x = 1; switch (x) { case 2: break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [case 1: break;]@26 -> [case 2: break;]@26");
    }

    [Fact]
    public void SwitchStatement_Update_AddSectionLabel()
    {
        var src1 = "var x = 1; switch (x) { case 1: break; }";
        var src2 = "var x = 1; switch (x) { case 1: case 2: break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [case 1: break;]@26 -> [case 1: case 2: break;]@26");
    }

    [Fact]
    public void SwitchStatement_Update_DeleteSectionLabel()
    {
        var src1 = "var x = 1; switch (x) { case 1: case 2: break; }";
        var src2 = "var x = 1; switch (x) { case 1: break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [case 1: case 2: break;]@26 -> [case 1: break;]@26");
    }

    [Fact]
    public void SwitchStatement_Update_BlockInSection()
    {
        var src1 = "var x = 1; switch (x) { case 1: { x++; break; } }";
        var src2 = "var x = 1; switch (x) { case 1: { x--; break; } }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [x++;]@36 -> [x--;]@36");
    }

    [Fact]
    public void SwitchStatement_Update_BlockInDefaultSection()
    {
        var src1 = "var x = 1; switch (x) { default: { x++; break; } }";
        var src2 = "var x = 1; switch (x) { default: { x--; break; } }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [x++;]@37 -> [x--;]@37");
    }

    [Fact]
    public void SwitchStatement_Insert_Section()
    {
        var src1 = "var x = 1; switch (x) { case 1: break; }";
        var src2 = "var x = 1; switch (x) { case 1: break; case 2: break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [case 2: break;]@41",
            "Insert [break;]@49");
    }

    [Fact]
    public void SwitchStatement_Delete_Section()
    {
        var src1 = "var x = 1; switch (x) { case 1: break; case 2: break; }";
        var src2 = "var x = 1; switch (x) { case 1: break; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Delete [case 2: break;]@41",
            "Delete [break;]@49");
    }

    #endregion

    #region Lambdas

    [Fact]
    public void Lambdas_AddAttribute()
    {
        var src1 = "Func<int, int> x = (a) => a;";
        var src2 = "Func<int, int> x = [A][return:A]([A]a) => a;";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(a) => a]@21 -> [[A][return:A]([A]a) => a]@21",
            "Update [a]@22 -> [[A]a]@35");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "([A]a)", GetResource("lambda")),
                Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "([A]a)", GetResource("lambda")),
                Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "([A]a)", GetResource("parameter"))
            ],
            capabilities: EditAndContinueCapabilities.Baseline);

        GetTopEdits(edits).VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.ChangeCustomAttributes);
    }

    [Fact]
    public void Lambdas_InVariableDeclarator()
    {
        var src1 = "Action x = a => a, y = b => b;";
        var src2 = "Action x = (a) => a, y = b => b + 1;";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [a => a]@13 -> [(a) => a]@13",
            "Update [b => b]@25 -> [b => b + 1]@27",
            "Insert [(a)]@13",
            "Insert [a]@14",
            "Delete [a]@13");
    }

    [Fact]
    public void Lambdas_InExpressionStatement()
    {
        var src1 = "F(a => a, b => b);";
        var src2 = "F(b => b, a => a+1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [b => b]@12 -> @4",
            "Update [a => a]@4 -> [a => a+1]@12");
    }

    [Fact]
    public void Lambdas_ReorderArguments()
    {
        var src1 = "F(G(a => {}), G(b => {}));";
        var src2 = "F(G(b => {}), G(a => {}));";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [b => {}]@18 -> @6");
    }

    [Fact]
    public void Lambdas_InWhile()
    {
        var src1 = "while (F(a => a)) { /*1*/ }";
        var src2 = "do { /*1*/ } while (F(a => a));";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [do { /*1*/ } while (F(a => a));]@2",
            "Move [{ /*1*/ }]@20 -> @5",
            "Move [a => a]@11 -> @24",
            "Delete [while (F(a => a)) { /*1*/ }]@2");
    }

    [Fact]
    public void Lambdas_InLambda_ChangeInLambdaSignature()
    {
        var src1 = "F(() => { G(x => y); });";
        var src2 = "F(q => { G(() => y); });";

        var edits = GetMethodEdits(src1, src2);

        // changes were made to the outer lambda signature:
        edits.VerifyEdits(
            "Update [() => { G(x => y); }]@4 -> [q => { G(() => y); }]@4",
            "Insert [q]@4",
            "Delete [()]@4");
    }

    [Fact]
    public void Lambdas_InLambda_ChangeOnlyInLambdaBody()
    {
        var src1 = "F(() => { G(x => y); });";
        var src2 = "F(() => { G(() => y); });";

        var edits = GetMethodEdits(src1, src2);

        // no changes to the method were made, only within the outer lambda body:
        edits.VerifyEdits();
    }

    [Fact]
    public void Lambdas_Insert_First_Static()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    var f = new Func<int, int>(a => a);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddStaticFieldToExistingType | EditAndContinueCapabilities.AddMethodToExistingType);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "a", GetResource("lambda"))],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void Lambdas_Insert_First_Static_InGenericContext_Method()
    {
        var src1 = """

            using System;
            class C
            {
                void F<T>()
                {
                }
            }
            """;
        var src2 = """

            using System;
            class C
            {
                void F<T>()
                {
                    var f = new Func<int, int>(a => a);
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.NewTypeDefinition |
                EditAndContinueCapabilities.AddMethodToExistingType |
                EditAndContinueCapabilities.AddStaticFieldToExistingType |
                EditAndContinueCapabilities.GenericAddMethodToExistingType |
                EditAndContinueCapabilities.GenericAddFieldToExistingType |
                EditAndContinueCapabilities.GenericUpdateMethod);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "a", GetResource("lambda"))],
            capabilities: EditAndContinueCapabilities.GenericUpdateMethod);
    }

    [Fact]
    public void Lambdas_Insert_First_Static_InGenericContext_Type()
    {
        var src1 = """

            using System;
            class C<T>
            {
                void F()
                {
                }
            }
            """;
        var src2 = """

            using System;
            class C<T>
            {
                void F()
                {
                    var f = new Func<int, int>(a => a);
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.NewTypeDefinition |
                EditAndContinueCapabilities.AddMethodToExistingType |
                EditAndContinueCapabilities.AddStaticFieldToExistingType |
                EditAndContinueCapabilities.GenericAddMethodToExistingType |
                EditAndContinueCapabilities.GenericAddFieldToExistingType |
                EditAndContinueCapabilities.GenericUpdateMethod);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "a", GetResource("lambda"))],
            capabilities: EditAndContinueCapabilities.GenericUpdateMethod);
    }

    [Fact]
    public void Lambdas_Insert_First_Static_InGenericContext_LocalFunction()
    {
        var src1 = """

            using System;
            class C
            {
                void F()
                {
                    void L<T>()
                    {
                    }
                }
            }
            """;
        var src2 = """

            using System;
            class C
            {
                void F()
                {
                    void L<T>()
                    {
                        var f = new Func<int, int>(a => a);
                    }
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.NewTypeDefinition |
                EditAndContinueCapabilities.AddMethodToExistingType |
                EditAndContinueCapabilities.GenericAddMethodToExistingType |
                EditAndContinueCapabilities.GenericUpdateMethod);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "a", GetResource("lambda"))],
            capabilities: EditAndContinueCapabilities.GenericUpdateMethod);
    }

    [Fact]
    public void Lambdas_Insert_Static_Nested()
    {
        var src1 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;

                void F()
                {
                    G(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;
               
                void F()
                {
                    G(a => G(b => b) + a);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.AddStaticFieldToExistingType);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "b", GetResource("lambda"))],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void Lambdas_Insert_ThisOnly_Top1()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 0;
                int G(Func<int, int> f) => 0;

                void F()
                {

                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 0;
                int G(Func<int, int> f) => 0;
               
                void F()
                {
                    G(a => x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1291")]
    public void Lambdas_Insert_ThisOnly_Top2()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    int y = 1;
                    {
                        int x = 2;
                        var f1 = new Func<int, int>(a => y);
                    }
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    int y = 1;
                    {
                        int x = 2;
                        var f2 = from a in new[] { 1 } select a + y;
                        var f3 = from a in new[] { 1 } where x > 0 select a;
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_ThisOnly_Nested1()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 0;
                int G(Func<int, int> f) => 0;

                void F()
                {
                    G(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 0;
                int G(Func<int, int> f) => 0;
               
                void F()
                {
                    G(a => G(b => x));
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_ThisOnly_Nested2()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 0;

                void F()
                {
                    var f1 = new Func<int, int>(a => 
                    {
                        var f2 = new Func<int, int>(b => 
                        {
                            return b;
                        });

                        return a;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 0;
               
                void F()
                {
                    var f1 = new Func<int, int>(a => 
                    {
                        var f2 = new Func<int, int>(b => 
                        {
                            return b;
                        });

                        var f3 = new Func<int, int>(c => 
                        {
                            return c + x;
                        });

                        return a;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_PrimaryParameterOnly_Top()
    {
        var src1 = """

            using System;

            class C(int x)
            {
                int G(Func<int, int> f) => 0;

                void F()
                {
                    
                }
            }

            """;
        var src2 = """

            using System;

            class C(int x)
            {
                int G(Func<int, int> f) => 0;
               
                void F()
                {
                    G(a => x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_PrimaryParameterOnly_Nested()
    {
        var src1 = """

            using System;

            class C(int x)
            {
                void F()
                {
                    var f1 = new Func<int, int>(a => 
                    {
                        var f2 = new Func<int, int>(b => 
                        {
                            return b;
                        });

                        return a;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C(int x)
            {
                void F()
                {
                    var f1 = new Func<int, int>(a => 
                    {
                        var f2 = new Func<int, int>(b => 
                        {
                            return b;
                        });

                        var f3 = new Func<int, int>(c => 
                        {
                            return c + x;
                        });

                        return a;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_ThisOnly_Second()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 0;

                void F()
                {
                    var f1 = new Func<int, int>(a => x);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 0;
               
                void F()
                {
                    var f1 = new Func<int, int>(a => x);
                    var f2 = new Func<int, int>(b => x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "b", GetResource("lambda"))],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void Lambdas_Insert_ThisAndPrimaryParameter()
    {
        var src1 = """

            using System;

            class C(int y)
            {
                int x = 0;

                void F()
                {
                    var f1 = new Func<int, int>(a => x);
                }
            }

            """;
        var src2 = """

            using System;

            class C(int y)
            {
                int x = 0;
               
                void F()
                {
                    var f1 = new Func<int, int>(a => x);
                    var f2 = new Func<int, int>(b => y);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "b", GetResource("lambda"))],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void Lambdas_Insert_Closure_Second()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int x = 1;
                    var f1 = new Func<int, int>(a => x);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int x = 1;
                    var f1 = new Func<int, int>(a => x);
                    var f2 = new Func<int, int>(b => x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "b", GetResource("lambda"))],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void Lambdas_InsertAndDelete_Scopes1()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                int x = 0, y = 0;                      // Group #0

                void F()
                {
                    int x0 = 0, y0 = 0;                // Group #1 
                                                     
                    { int x1 = 0, y1 = 0;              // Group #2 
                                                       
                        { int x2 = 0, y2 = 0;          // Group #1 
                                                        
                            { int x3 = 0, y3 = 0;      // Group #2 
                                                       
                                G(a => x3 + x1);       
                                G(b => x0 + y0 + x2);
                                G(c => x);
                            }
                        }
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                int x = 0, y = 0;                       // Group #0

                void F()
                {
                    int x0 = 0, y0 = 0;                 // Group #1
                                                       
                    { int x1 = 0, y1 = 0;               // Group #2 
                                                       
                        { int x2 = 0, y2 = 0;           // Group #1
                                                       
                            { int x3 = 0, y3 = 0;       // Group #2 
                                                        
                                G(a => x3 + x1);        
                                G(b => x0 + y0 + x2);
                                G(c => x);

                                G(d => x);              // OK
                                G(e => x0 + y0);        // OK
                                G(f => x1 + y0);        // runtime rude edit - connecting Group #1 and Group #2
                                G(g => x3 + x1);        // runtime rude edit - multi-scope (conservative)
                                G(h => x + y0);         // runtime rude edit - connecting Group #0 and Group #1
                                G(i => x + x3);         // runtime rude edit - connecting Group #0 and Group #2
                            }
                        }
                    }
                }
            }

            """;
        var insert = GetTopEdits(src1, src2);

        insert.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);

        var delete = GetTopEdits(src2, src1);

        delete.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_ForEach1()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                void F()                       
                {                              
                    foreach (int x0 in new[] { 1 })  // Group #0             
                    {                                // Group #1
                        int x1 = 0;                  
                                                     
                        G(a => x0);   
                        G(a => x1);
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                void F()                       
                {                              
                    foreach (int x0 in new[] { 1 })  // Group #0             
                    {                                // Group #1
                        int x1 = 0;                  
                                                     
                        G(a => x0);   
                        G(a => x1);

                        G(a => x0 + x1);             // runtime rude edit: connecting previously disconnected closures
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_ForEach2()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f1, Func<int, int> f2, Func<int, int> f3) {}

                void F()                       
                {               
                    int x0 = 0;                              // Group #0  
                    foreach (int x1 in new[] { 1 })          // Group #1                   
                        G(a => x0, a => x1, null);                     
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f1, Func<int, int> f2, Func<int, int> f3) {}

                void F()                       
                {               
                    int x0 = 0;                              // Group #0  
                    foreach (int x1 in new[] { 1 })          // Group #1            
                        G(a => x0, a => x1, a => x0 + x1);   // runtime rude edit: connecting previously disconnected closures            
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_For1()
    {
        var src1 = """

            using System;

            class C
            {
                bool G(Func<int, int> f) => true;

                void F()                       
                {                              
                    for (int x0 = 0, x1 = 0; G(a => x0) && G(a => x1);)
                    {
                        int x2 = 0;
                        G(a => x2); 
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                bool G(Func<int, int> f) => true;

                void F()                       
                {                              
                    for (int x0 = 0, x1 = 0; G(a => x0) && G(a => x1);)
                    {
                        int x2 = 0;
                        G(a => x2); 

                        G(a => x0 + x1);  // ok
                        G(a => x0 + x2);  // runtime rude edit: connecting previously disconnected closures
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_Switch1()
    {
        var src1 = """

            using System;

            class C
            {
                bool G(Func<int> f) => true;

                int a = 1;

                void F()                       
                {        
                    int x2 = 1;
                    G(() => x2);
                                  
                    switch (a)
                    {
                        case 1:
                            int x0 = 1;
                            G(() => x0);
                            break;

                        case 2:
                            int x1 = 1;
                            G(() => x1);
                            break;
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                bool G(Func<int> f) => true;

                int a = 1;

                void F()                       
                {                
                    int x2 = 1;
                    G(() => x2);
             
                    switch (a)
                    {
                        case 1:
                            int x0 = 1;
                            G(() => x0);
                            goto case 2;

                        case 2:
                            int x1 = 1;
                            G(() => x1);
                            goto default;

                        default:
                            x0 = 1;
                            x1 = 2;
                            G(() => x0 + x1);       // ok
                            G(() => x0 + x2);       // runtime rude edit
                            break;
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_Using1()
    {
        var src1 = """

            using System;

            class C
            {
                static bool G<T>(Func<T> f) => true;
                static int H(object a, object b) => 1;

                static IDisposable D() => null;
                
                static void F()                       
                {                              
                    using (IDisposable x0 = D(), y0 = D())
                    {
                        int x1 = 1;
                    
                        G(() => x0);
                        G(() => y0);
                        G(() => x1);
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static bool G<T>(Func<T> f) => true;
                static int H(object a, object b) => 1;

                static IDisposable D() => null;
                
                static void F()                       
                {                              
                    using (IDisposable x0 = D(), y0 = D())
                    {
                        int x1 = 1;
                    
                        G(() => x0);
                        G(() => y0);
                        G(() => x1);

                        G(() => H(x0, y0)); // ok
                        G(() => H(x0, x1)); // runtime rude edit
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_Catch1()
    {
        var src1 = """

            using System;

            class C
            {
                static bool G<T>(Func<T> f) => true;
                static int H(object a, object b) => 1;
                
                static void F()                       
                {                              
                    try
                    {
                    }
                    catch (Exception x0)
                    {
                        int x1 = 1;
                        G(() => x0);
                        G(() => x1);
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static bool G<T>(Func<T> f) => true;
                static int H(object a, object b) => 1;
                
                static void F()                       
                {                              
                    try
                    {
                    }
                    catch (Exception x0)
                    {
                        int x1 = 1;
                        G(() => x0);
                        G(() => x1);

                        G(() => x0); //ok
                        G(() => H(x0, x1)); // runtime rude edit
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1504")]
    public void Lambdas_Insert_CatchFilter1()
    {
        var src1 = """

            using System;

            class C
            {
                static bool G<T>(Func<T> f) => true;
                
                static void F()                       
                {                              
                    Exception x1 = null;
                
                    try
                    {
                        G(() => x1);
                    }
                    catch (Exception x0) when (G(() => x0))
                    {
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static bool G<T>(Func<T> f) => true;
                
                static void F()                       
                {                 
                    Exception x1 = null;
                         
                    try
                    {
                        G(() => x1);
                    }
                    catch (Exception x0) when (G(() => x0) && 
                                               G(() => x0) &&    // ok
                                               G(() => x0 != x1)) // runtime rude edit
                    {
                        G(() => x0); // ok
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Insert_Static_Second()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    var f = new Func<int, int>(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    var f = new Func<int, int>(a => a);
                    var g = new Func<int, int>(b => b);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.AddStaticFieldToExistingType);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "b", CSharpFeaturesResources.lambda)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void Lambdas_Update_ParameterRefness_NoBodyChange()
    {
        var src1 = @"F((ref int a) => a = 1);";
        var src2 = @"F((out int a) => a = 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [ref int a]@5 -> [out int a]@5");
    }

    [Fact]
    public void Lambdas_Update_Signature1()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<long, long> f) {}

                void F()
                {
                    G1(<N:0>a => a</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<long, long> f) {}

                void F()
                {
                    G2(<N:0>a => a</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_Signature2()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int, int> f) {}

                void F()
                {
                    G1(<N:0>a => a</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int, int> f) {}

                void F()
                {
                    G2(<N:0>(a, b) => a + b</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_Signature3()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, long> f) {}

                void F()
                {
                    G1(<N:0>a => a</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, long> f) {}

                void F()
                {
                    G2(<N:0>a => a</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_Signature_Nullable()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<string, string> f) {}
                void G2(Func<string?, string?> f) {}

                void F()
                {
                    G1(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<string, string> f) {}
                void G2(Func<string?, string?> f) {}

                void F()
                {
                    G2(a => a);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_Signature_SyntaxOnly1()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int> f) {}

                void F()
                {
                    G1(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int> f) {}

                void F()
                {
                    G2((a) => a);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_Signature_ReturnType1()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Action<int> f) {}

                void F()
                {
                    G1(<N:0>a => { return 1; }</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Action<int> f) {}

                void F()
                {
                    G2(<N:0>a => { }</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_Signature_ReturnType2()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Action<int> f) {}

                void F()
                {
                    var x = <N:0>int (int a) => a</N:0>;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Action<int> f) {}

                void F()
                {
                    var x = <N:0>long (int a) => a</N:0>;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_Signature_ReturnType_Anonymous()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    var x = <N:0>(int* a, int b) => a</N:0>;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    var x = <N:0>(int* a, int b) => b</N:0>;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_Signature_BodySyntaxOnly()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int> f) {}

                void F()
                {
                    G1(a => { return 1; });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int> f) {}

                void F()
                {
                    G2(a => 2);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_Signature_ParameterName1()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int> f) {}

                void F()
                {
                    G1(a => 1);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int> f) {}

                void F()
                {
                    G2(b => 2);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_Signature_ParameterRefness1()
    {
        var src1 = """

            using System;

            delegate int D1(ref int a);
            delegate int D2(int a);

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G1(<N:0>(ref int a) => 1</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            delegate int D1(ref int a);
            delegate int D2(int a);

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G2(<N:0>(int a) => 2</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_Signature_ParameterRefness2()
    {
        var src1 = """

            using System;

            delegate int D1(ref int a);
            delegate int D2(out int a);

            class C
            {
                void G(D1 f) {}
                void G(D2 f) {}

                void F()
                {
                    G((ref int a) => a = 1);
                }
            }

            """;
        var src2 = """

            using System;

            delegate int D1(ref int a);
            delegate int D2(out int a);

            class C
            {
                void G(D1 f) {}
                void G(D2 f) {}

                void F()
                {
                    G((out int a) => a = 1);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    // Add corresponding test to VB
    [Fact(Skip = "TODO")]
    public void Lambdas_Update_Signature_CustomModifiers1()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G1(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G2(a => a);
                }
            }

            """;
        MetadataReference delegateDefs;
        using (var tempAssembly = IlasmUtilities.CreateTempAssembly("""

            .class public auto ansi sealed D1
                   extends [mscorlib]System.MulticastDelegate
            {
              .method public specialname rtspecialname instance void .ctor(object 'object', native int 'method') runtime managed
              {
              }

              .method public newslot virtual instance int32 [] modopt([mscorlib]System.Int64) Invoke(
                  int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) a, 
                  int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) b) runtime managed
              {
              }
            }

            .class public auto ansi sealed D2
                   extends [mscorlib]System.MulticastDelegate
            {
              .method public specialname rtspecialname instance void .ctor(object 'object', native int 'method') runtime managed
              {
              }

              .method public newslot virtual instance int32 [] modopt([mscorlib]System.Boolean) Invoke(
                  int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) a, 
                  int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) b) runtime managed
              {
              }
            }

            .class public auto ansi sealed D3
                   extends [mscorlib]System.MulticastDelegate
            {
              .method public specialname rtspecialname instance void .ctor(object 'object', native int 'method') runtime managed
              {
              }

              .method public newslot virtual instance int32 [] modopt([mscorlib]System.Boolean) Invoke(
                  int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) a, 
                  int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) b) runtime managed
              {
              }
            }
            """))
        {
            delegateDefs = MetadataReference.CreateFromImage(File.ReadAllBytes(tempAssembly.Path));
        }

        var edits = GetTopEdits(src1, src2);

        // TODO
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_Signature_MatchingErrorType()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<Unknown, Unknown> f) {}

                void F()
                {
                    G(a => 1);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<Unknown, Unknown> f) {}

                void F()
                {
                    G(a => 2);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            ActiveStatementsDescription.Empty,
            semanticEdits:
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").GetMembers("F").Single(), preserveLocalVariables: true)
            ]);
    }

    [Fact]
    public void Lambdas_Update_Signature_NonMatchingErrorType()
    {
        var src1 = """

            using System;

            class C
            {
                void G1(Func<Unknown1, Unknown1> f) {}
                void G2(Func<Unknown2, Unknown2> f) {}

                void F()
                {
                    G1(<N:0>a => 1</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<Unknown1, Unknown1> f) {}
                void G2(Func<Unknown2, Unknown2> f) {}

                void F()
                {
                    G2(<N:0>a => 2</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_DelegateType1()
    {
        var src1 = """

            using System;

            delegate int D1(int a);
            delegate int D2(int a);

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G1(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            delegate int D1(int a);
            delegate int D2(int a);

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G2(a => a);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_SourceType1()
    {
        var src1 = """

            using System;

            delegate C D1(C a);
            delegate C D2(C a);

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G1(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            delegate C D1(C a);
            delegate C D2(C a);

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G2(a => a);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_SourceType2()
    {
        var src1 = """

            using System;

            delegate C D1(C a);
            delegate B D2(B a);

            class B { }

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G1(<N:0>a => a</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            delegate C D1(C a);
            delegate B D2(B a);

            class B { }

            class C
            {
                void G1(D1 f) {}
                void G2(D2 f) {}

                void F()
                {
                    G2(<N:0>a => a</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_SourceTypeAndMetadataType1()
    {
        var src1 = """

            namespace System
            {
                delegate string D1(string a);
                delegate String D2(String a);

                class String { }

                class C
                {
                    void G1(D1 f) {}
                    void G2(D2 f) {}

                    void F()
                    {
                        G1(<N:0>a => a</N:0>);
                    }
                }
            }

            """;
        var src2 = """

            namespace System
            {
                delegate string D1(string a);
                delegate String D2(String a);

                class String { }

                class C
                {
                    void G1(D1 f) {}
                    void G2(D2 f) {}

                    void F()
                    {
                        G2(<N:0>a => a</N:0>);
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("System.C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), arguments: [GetResource("lambda")])
            ]));
    }

    [Fact]
    public void Lambdas_Update_Generic1()
    {
        var src1 = """

            delegate T D1<S, T>(S a, T b);
            delegate T D2<S, T>(T a, S b);

            class C
            {
                void G1(D1<int, int> f) {}
                void G2(D2<int, int> f) {}

                void F()
                {
                    G1((a, b) => a + b);
                }
            }

            """;
        var src2 = """

            delegate T D1<S, T>(S a, T b);
            delegate T D2<S, T>(T a, S b);

            class C
            {
                void G1(D1<int, int> f) {}
                void G2(D2<int, int> f) {}

                void F()
                {
                    G2((a, b) => a + b);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_Generic2()
    {
        var src1 = """

            delegate int D1<S, T>(S a, T b);
            delegate int D2<S, T>(T a, S b);

            class C
            {
                void G1(D1<int, int> f) {}
                void G2(D2<int, string> f) {}

                void F()
                {
                    G1(<N:0>(a, b) => 1</N:0>);
                }
            }

            """;
        var src2 = """

            delegate int D1<S, T>(S a, T b);
            delegate int D2<S, T>(T a, S b);

            class C
            {
                void G1(D1<int, int> f) {}
                void G2(D2<int, string> f) {}

                void F()
                {
                    G2(<N:0>(a, b) => 1</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap,
                    rudeEdits: [RuntimeRudeEdit(marker: 0, RudeEditKind.ChangingLambdaParameters, syntaxMap.NodePosition(0), [GetResource("lambda")])]),
            ]);
    }

    [Fact]
    public void Lambdas_Update_CapturedParameters1()
    {
        var src1 = """

            using System;

            class C
            {
                void F(int x1)
                {
                    var f1 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f2 = new Func<int, int>(a3 => x1 + a2);
                        return a1;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F(int x1)
                {
                    var f1 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f2 = new Func<int, int>(a3 => x1 + a2 + 1);
                        return a1;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/2223")]
    public void Lambdas_Update_CapturedParameters2()
    {
        var src1 = """

            using System;

            class C
            {
                void F(int x1)
                {
                    var f1 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f2 = new Func<int, int>(a3 => x1 + a2);
                        return a1;
                    });

                    var f3 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f4 = new Func<int, int>(a3 => x1 + a2);
                        return a1;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F(int x1)
                {
                    var f1 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f2 = new Func<int, int>(a3 => x1 + a2 + 1);
                        return a1;
                    });

                    var f3 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f4 = new Func<int, int>(a3 => x1 + a2 + 1);
                        return a1;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_This()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    var f = new Func<int, int>(<N:0>a => a + x</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 1;
               
                void F()
                {
                    var f = new Func<int, int>(<N:0>a => a</N:0>);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = GetSyntaxMap(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_Closure1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => 
                    {
                        var f2 = new Func<int, int>(a2 => y + a2);
                        return a1;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => 
                    {
                        var f2 = new Func<int, int>(a2 => a2);
                        return a1 + y;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        // y is no longer captured in f2
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/51297")]
    public void Lambdas_Update_CeaseCapture_IndexerParameter_WithExpressionBody()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] => new Func<int, int>(a3 => a1 + a2);
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] => new Func<int, int>(a3 => a2);
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.this[]")),
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true),
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/51297")]
    public void Lambdas_Update_CeaseCapture_IndexerParameter_WithExpressionBody_LambdaBlock()
    {
        var src1 = """

            using System;

            class C
            {
                int this[int a] => new Func<int>(() => { return a + 1; })();
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int this[int a] => new Func<int>(() => { return 2; })();   // not capturing a anymore
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [int this[int a] => new Func<int>(() => { return a + 1; })();]@35 -> [int this[int a] => new Func<int>(() => { return 2; })();]@35",
            "Update [=> new Func<int>(() => { return a + 1; })()]@51 -> [=> new Func<int>(() => { return 2; })()]@51");

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.this[]")),
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true),
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/51297")]
    public void Lambdas_Update_CeaseCapture_IndexerParameter_WithExpressionBody_Delegate()
    {
        var src1 = """

            using System;

            class C
            {
                int this[int a] => new Func<int>(delegate { return a + 1; })();
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int this[int a] => new Func<int>(delegate { return 2; })();   // not capturing a anymore
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [int this[int a] => new Func<int>(delegate { return a + 1; })();]@35 -> [int this[int a] => new Func<int>(delegate { return 2; })();]@35",
            "Update [=> new Func<int>(delegate { return a + 1; })()]@51 -> [=> new Func<int>(delegate { return 2; })()]@51");

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.this[]")),
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true),
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_IndexerParameter_WithExpressionBody_Getter()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get => new(a3 => a1 + a2); }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { return new Func<int, int>(a3 => a2); } }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/51297")]
    public void Lambdas_Update_CeaseCapture_IndexerParameter_WithExpressionBody_Partial()
        => EditAndContinueValidation.VerifySemantics(
            [GetTopEdits("""

                partial class C
                {
                }
                """, """

                partial class C
                {
                    int this[int a] => new System.Func<int>(() => 2); // no capture
                }
                """), GetTopEdits("""

                         partial class C
                         {
                             int this[int a] => new System.Func<int>(() => a + 1);
                         }
                         """, """

                         partial class C
                         {
                         }
                         """)],
            [
                DocumentResults(
                    semanticEdits: [
                        SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.this[]")),
                        SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true),
                    ]),
                DocumentResults(),
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);

    [Fact]
    public void Lambdas_Update_CeaseCapture_IndexerParameter_ParameterDelete()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { return new Func<int, int>(a3 => a1 + a2); } }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a2] { get { return new Func<int, int>(a3 => a2); } }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.this[]"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.this[]")),
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.get_Item"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.get_Item"))
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_MethodParameter()
    {
        var src1 = """

            using System;

            class C
            {
                void F(int a1, int a2)
                {
                    var f2 = new Func<int, int>(a3 => a1 + a2);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F(int a1, int a2)
                {
                    var f2 = new Func<int, int>(a3 => a1);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/51297")]
    public void Lambdas_Update_CeaseCapture_MethodParameter_WithExpressionBody()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1 + a2);
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1);
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_MethodParameter_ParameterDelete()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1 + a2);
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> F(int a1) => new Func<int, int>(a3 => a1);
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.F"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.F"))
            ],
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_MethodParameter_ParameterTypeChange()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1 + a2);
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> F(byte a1) => new Func<int, int>(a3 => a1);
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.F"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.F"))
            ],
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_MethodParameter_LocalToParameter()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> F(int a1) { int a2 = 1; return new Func<int, int>(a3 => a1 + a2); }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) { return new Func<int, int>(a3 => a1 + a2); }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.F"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.F"))
            ],
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_MethodParameter_ParameterToLocal()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) { return new Func<int, int>(a3 => a1 + a2); }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> F(int a1) { int a2 = 1; return new Func<int, int>(a3 => a1 + a2); }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.F"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.F"))
            ],
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_LambdaParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    var f1 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f2 = new Func<int, int>(a3 => 
                        {
                            var f3 = new Func<int, int>(a4 => a1 + a2 + a3);
                            return 1;
                        });
                        return a1;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    var f1 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f2 = new Func<int, int>(a3 => 
                        {
                            var f3 = new Func<int, int>(a4 => a2);
                            return 1;
                        });
                        return a1;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")]
    public void Lambdas_Update_CeaseCapture_SetterValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                int D
                {
                    get { return 0; }
                    set { new Action(() => { Console.Write(value); }).Invoke(); }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int D
                {
                    get { return 0; }
                    set { }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.set_D")));
    }

    [Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")]
    public void Lambdas_Update_CeaseCapture_IndexerSetterValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                int this[int a1, int a2]
                {
                    get { return 0; }
                    set { new Action(() => { Console.Write(value); }).Invoke(); }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int this[int a1, int a2]
                {
                    get { return 0; }
                    set { }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.set_Item")));
    }

    [Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")]
    public void Lambdas_Update_CeaseCapture_EventAdderValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                event Action D
                {
                    add { new Action(() => { Console.Write(value); }).Invoke(); }
                    remove { }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                event Action D
                {
                    add {  }
                    remove { }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.add_D")));
    }

    [Fact, WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")]
    public void Lambdas_Update_CeaseCapture_EventRemoverValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                event Action D
                {
                    add { }
                    remove { new Action(() => { Console.Write(value); }).Invoke(); }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                event Action D
                {
                    add { }
                    remove { }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.remove_D")));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/68731")]
    public void Lambdas_Update_CeaseCapture_ConstructorInitializer_This()
    {
        var src1 = "class C { C(int x) : this(() => x) {} C(Func<int> f) {} }";
        var src2 = "class C { C(int x) : this(() => 1) {} C(Func<int> f) {} }";

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").InstanceConstructors.Single(m => m.Parameters is [{ Name: "x" }]), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/68731")]
    public void Lambdas_Update_CeaseCapture_ConstructorInitializer_Base()
    {
        var src1 = "class C : B { C(int x) : base(() => x) {} } class B { public B(Func<int> f) {} }";
        var src2 = "class C : B { C(int x) : base(() => 1) {} } class B { public B(Func<int> f) {} }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C..ctor"), preserveLocalVariables: true));
    }

    [Theory, CombinatorialData]
    public void Lambdas_Update_CeaseCapture_PrimaryParameter_InPrimaryConstructor_First(
        [CombinatorialValues("class", "struct", "record", "record struct")] string keyword)
    {
        var src1 = keyword + " C(int x, int y) { System.Func<int> z = () => x; }";
        var src2 = keyword + " C(int x, int y) { System.Func<int> z = () => 1; }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").InstanceConstructors.Single(m => m.Parameters is [_, _]), preserveLocalVariables: true));
    }

    [Theory, CombinatorialData]
    public void Lambdas_Update_CeaseCapture_PrimaryParameter_InPrimaryConstructor_Second(
        [CombinatorialValues("class", "struct", "record", "record struct")] string keyword)
    {
        var src1 = keyword + " C(int x, int y) { System.Func<int> z = () => x + y; }";
        var src2 = keyword + " C(int x, int y) { System.Func<int> z = () => x; }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").InstanceConstructors.Single(m => m.Parameters is [_, _]), preserveLocalVariables: true));
    }

    [Theory, CombinatorialData]
    [WorkItem("https://github.com/dotnet/roslyn/issues/68731")]
    public void Lambdas_Update_CeaseCapture_PrimaryParameter_InPrimaryConstructor_BaseInitializer(
        [CombinatorialValues("class", "record")] string keyword)
    {
        var src1 = keyword + " C(int x, int y) : B(() => x);";
        var src2 = keyword + " C(int x, int y) : B(() => 1);";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").InstanceConstructors.Single(m => m.Parameters is [_, _]), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_PrimaryParameter_Method_First()
    {
        var src1 = "class C(int x, int y) { System.Func<int> M() => () => x; }";
        var src2 = "class C(int x, int y) { System.Func<int> M() => () => 1; }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_PrimaryParameter_Method_Second()
    {
        var src1 = "class C(int x, int y) { System.Func<int> M() => () => x + y; }";
        var src2 = "class C(int x, int y) { System.Func<int> M() => () => x; }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_PrimaryParameter_Method_ThisToPrimaryCapture()
    {
        var src1 = "class C(int x, int y) { System.Func<int> M() => () => x; }";
        var src2 = "class C(int x, int y) { System.Func<int> M() => () => this.M()(); }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_CeaseCapture_PrimaryParameter_Method_PrimaryToThisCapture()
    {
        var src1 = "class C(int x, int y) { System.Func<int> M() => () => this.M()(); }";
        var src2 = "class C(int x, int y) { System.Func<int> M() => () => x; }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_DeleteCapture1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => 
                    {
                        var f2 = new Func<int, int>(a2 => y + a2);
                        return y;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                { // error
                    var f1 = new Func<int, int>(a1 => 
                    {
                        var f2 = new Func<int, int>(a2 => a2);
                        return a1;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        // y is no longer captured in f2
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Capturing_IndexerGetterParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] => new Func<int, int>(a3 => a2);
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] => new Func<int, int>(a3 => a1 + a2);
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.this[]")),
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true)
            ]);
    }

    [Fact]
    public void Lambdas_Update_Capturing_IndexerGetterParameter2()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { return new Func<int, int>(a3 => a2); } }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { return new Func<int, int>(a3 => a1 + a2); } }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Capturing_IndexerGetterParameter_ParameterInsert()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1] => new(a3 => a1);
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] => new(a3 => a1 + a2);
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.this[]"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.get_Item"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.this[]")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.get_Item")),
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_Capturing_IndexerSetterParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { return null; } set { var f = new Func<int, int>(a3 => a2); } }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { return null; } set { var f = new Func<int, int>(a3 => a1 + a2); } }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.set_Item"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Capturing_IndexerSetterValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                int this[int a1, int a2]
                {
                    get { return 0; }
                    set {  }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int this[int a1, int a2]
                {
                    get { return 0; }
                    set { new Action(() => { Console.Write(value); }).Invoke(); }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.set_Item"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_Capturing_EventAdderValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                event Action D
                {
                    add {  }
                    remove { }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                event Action D
                {
                    add {  }
                    remove { new Action(() => { Console.Write(value); }).Invoke(); }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.remove_D"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_Capturing_EventRemoverValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                event Action D
                {
                    add { }
                    remove {  }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                event Action D
                {
                    add { }
                    remove { new Action(() => { Console.Write(value); }).Invoke(); }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.remove_D"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_Capturing_MethodParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                void F(int a1, int a2)
                {
                    var f2 = new Func<int, int>(a3 => a1);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F(int a1, int a2)
                {
                    var f2 = new Func<int, int>(a3 => a1 + a2);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Capturing_MethodParameter2()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1);
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1 + a2);
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Capturing_MethodParameter_ParameterInsert()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> F(int a1) => new Func<int, int>(a3 => a1);
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1 + a2);
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.F"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.F"))
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_Capturing_MethodParameter_ParameterInsert_Partial()
    {
        var src1 = """

            using System;

            partial class C
            {
                public partial Func<int, int> F(int a1);
            }

            partial class C
            {
                public partial Func<int, int> F(int a1) => new Func<int, int>(a3 => a1);
            }

            """;
        var src2 = """

            using System;

            partial class C
            {
                public partial Func<int, int> F(int a1, int a2);
            }

            partial class C
            {
                public partial Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1 + a2);
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember<IMethodSymbol>("C.F").PartialImplementationPart, deletedSymbolContainerProvider: c => c.GetMember("C"), partialType: "C"),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember<IMethodSymbol>("C.F").PartialImplementationPart, partialType: "C")
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_Capturing_LambdaParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    var f1 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f2 = new Func<int, int>(a3 => a2);
                        return a1;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    var f1 = new Func<int, int, int>((a1, a2) => 
                    {
                        var f2 = new Func<int, int>(a3 => a1 + a2);
                        return a1;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/68731")]
    public void Lambdas_Update_Capturing_ConstructorInitializer_This()
    {
        var src1 = "class C { C(int x) : this(() => 1) {} C(Func<int> f) {} }";
        var src2 = "class C { C(int x) : this(() => x) {} C(Func<int> f) {} }";

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").InstanceConstructors.Single(m => m.Parameters is [{ Name: "x" }]), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/68731")]
    public void Lambdas_Update_Capturing_ConstructorInitializer_Base()
    {
        var src1 = "class C : B { C(int x) : base(() => 1) {} } class B { public B(Func<int> f) {} }";
        var src2 = "class C : B { C(int x) : base(() => x) {} } class B { public B(Func<int> f) {} }";

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C..ctor"), preserveLocalVariables: true));
    }

    [Theory, CombinatorialData]
    public void Lambdas_Update_Capturing_PrimaryParameter_InPrimaryConstructor_First(
        [CombinatorialValues("class", "struct", "record", "record struct")] string keyword)
    {
        var src1 = keyword + " C(int x, int y) { System.Func<int> z = () => 1; }";
        var src2 = keyword + " C(int x, int y) { System.Func<int> z = () => x; }";

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").InstanceConstructors.Single(m => m.Parameters is [_, _]), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Theory, CombinatorialData]
    public void Lambdas_Update_Capturing_PrimaryParameter_InPrimaryConstructor_Second(
        [CombinatorialValues("class", "struct", "record", "record struct")] string keyword)
    {
        var src1 = keyword + " C(int x, int y) { System.Func<int> z = () => x; }";
        var src2 = keyword + " C(int x, int y) { System.Func<int> z = () => x + y; }";

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").InstanceConstructors.Single(m => m.Parameters is [_, _]), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Theory]
    [WorkItem("https://github.com/dotnet/roslyn/issues/68731")]
    [CombinatorialData]
    public void Lambdas_Update_Capturing_PrimaryParameter_InPrimaryConstructor_BaseInitializer(
        [CombinatorialValues("class", "record")] string keyword)
    {
        var src1 = keyword + " C(int x, int y) : B(() => 1);";
        var src2 = keyword + " C(int x, int y) : B(() => x);";

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember<INamedTypeSymbol>("C").InstanceConstructors.Single(m => m.Parameters is [_, _]), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_Update_Capturing_PrimaryParameter_Method_First()
    {
        var src1 = "class C(int x, int y) { System.Func<int> M() => () => 1; }";
        var src2 = "class C(int x, int y) { System.Func<int> M() => () => x; }";

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Capturing_PrimaryParameter_Method_Second()
    {
        var src1 = "class C(int x, int y) { System.Func<int> M() => () => x; }";
        var src2 = "class C(int x, int y) { System.Func<int> M() => () => x + y; }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Capturing_PrimaryParameter_Method_ThisToPrimaryCapture()
    {
        var src1 = "class C(int x, int y) { System.Func<int> M() => () => this.M()(); }";
        var src2 = "class C(int x, int y) { System.Func<int> M() => () => x; }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Capturing_PrimaryParameter_Method_PrimaryToThisCapture()
    {
        var src1 = "class C(int x, int y) { System.Func<int> M() => () => x; }";
        var src2 = "class C(int x, int y) { System.Func<int> M() => () => this.M()(); }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69152")]
    public void Lambdas_Update_PrimaryParameterOutsideOfLambda()
    {
        var src1 = """

            using System;

            class C(int x)
            {
                void F()
                {
                    var f = new Func<int, int>(a => 1);
                }
            }

            """;
        var src2 = """

            using System;

            class C(int x)
            {
                void F()
                {
                    var f = new Func<int, int>(a => 2);
                    var y = x;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_StaticToThisOnly1()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    var f = new Func<int, int>(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 1;
               
                void F()
                {
                    var f = new Func<int, int>(a => a + x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_StaticToThisOnly_Partial()
    {
        var src1 = """

            using System;

            partial class C
            {
                int x = 1;
                partial void F(); // def
            }

            partial class C
            {
                partial void F()  // impl
                {
                    var f = new Func<int, int>(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            partial class C
            {
                int x = 1;
                partial void F(); // def
            }

            partial class C
            {
                partial void F()  // impl
                {
                    var f = new Func<int, int>(a => a + x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember<IMethodSymbol>("C.F").PartialImplementationPart, preserveLocalVariables: true, partialType: "C"));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69152")]
    public void Lambdas_Update_StaticToPrimaryParameterOnly_Partial()
    {
        var src1 = """

            using System;

            partial class C(int x)
            {
                partial void F(); // def
            }

            partial class C
            {
                partial void F()  // impl
                {
                    var f = new Func<int, int>(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            partial class C(int x)
            {
                partial void F(); // def
            }

            partial class C
            {
                partial void F()  // impl
                {
                    var f = new Func<int, int>(a => a + x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember<IMethodSymbol>("C.F").PartialImplementationPart, preserveLocalVariables: true, partialType: "C"));
    }

    [Fact]
    public void Lambdas_Update_StaticToThisOnly3()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    var f1 = new Func<int, int>(a1 => a1);
                    var f2 = new Func<int, int>(a2 => a2 + x);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 1;
               
                void F()
                {
                    var f1 = new Func<int, int>(a1 => a1 + x);
                    var f2 = new Func<int, int>(a2 => a2 + x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69152")]
    public void Lambdas_Update_StaticToPrimaryParameterOnly3()
    {
        var src1 = """

            using System;

            class C(int x)
            {
                void F()
                {
                    var f1 = new Func<int, int>(a1 => a1);
                    var f2 = new Func<int, int>(a2 => a2 + x);
                }
            }

            """;
        var src2 = """

            using System;

            class C(int x)
            {
                void F()
                {
                    var f1 = new Func<int, int>(a1 => a1 + x);
                    var f2 = new Func<int, int>(a2 => a2 + x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_StaticToPrimaryParameterOnly()
    {
        var src1 = """

            using System;

            class C(int x)
            {
                void F()
                {
                    var f = new Func<int, int>(a => a);
                }
            }

            """;
        var src2 = """

            using System;

            class C(int x)
            {
                void F()
                {
                    var f = new Func<int, int>(a => a + x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_StaticToClosure1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int x = 1;
                    var f1 = new Func<int, int>(a1 => a1);
                    var f2 = new Func<int, int>(a2 => a2 + x);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int x = 1;
                    var f1 = new Func<int, int>(a1 => 
                    { 
                        return a1 + 
                            x+ // 1 
                            x; // 2
                    });

                    var f2 = new Func<int, int>(a2 => a2 + x);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_ThisOnlyToClosure1()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => a1 + x);
                    var f2 = new Func<int, int>(a2 => a2 + x + y);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => a1 + x + y);
                    var f2 = new Func<int, int>(a2 => a2 + x + y);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Nested1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => 
                    {
                        var f2 = new Func<int, int>(a2 => a2 + y);
                        return a1;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => 
                    {
                        var f2 = new Func<int, int>(a2 => a2 + y);
                        return a1 + y;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_Update_Nested2()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => 
                    {
                        var f2 = new Func<int, int>(a2 => a2);
                        return a1;
                    });
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    var f1 = new Func<int, int>(a1 => 
                    {
                        var f2 = new Func<int, int>(a2 => a1 + a2);
                        return a1;
                    });
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Accessing_Closure1()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                void F()
                {
                    int x0 = 0, y0 = 0;                
                                                     
                    G(a => x0);
                    G(a => y0);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                void F()
                {
                    int x0 = 0, y0 = 0;                
                                                     
                    G(a => x0);
                    G(a => y0 + x0);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Accessing_Closure2()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                int x = 0;                     // Group #0
                                               
                void F()                       
                {                              
                    { int x0 = 0, y0 = 0;      // Group #0             
                        { int x1 = 0, y1 = 0;  // Group #1               
                                                     
                            G(a => x + x0);   
                            G(a => x0);
                            G(a => y0);
                            G(a => x1);
                        }
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}
                int x = 0;                     // Group #0

                void F()
                {
                    { int x0 = 0, y0 = 0;      // Group #0          
                        { int x1 = 0, y1 = 0;  // Group #1              
                                                     
                            G(a => x);         // error: disconnecting previously connected closures
                            G(a => x0);
                            G(a => y0);
                            G(a => x1);
                        }
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Accessing_Closure3()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                int x = 0;                     // Group #0
                                               
                void F()                       
                {                              
                    { int x0 = 0, y0 = 0;      // Group #0             
                        { int x1 = 0, y1 = 0;  // Group #1               
                                                     
                            G(a => x);   
                            G(a => x0);
                            G(a => y0);
                            G(a => x1);
                            G(a => y1);
                        }
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}
                int x = 0;                     // Group #0

                void F()
                {
                    { int x0 = 0, y0 = 0;      // Group #0          
                        { int x1 = 0, y1 = 0;  // Group #1              
                                                     
                            G(a => x);         
                            G(a => x0);
                            G(a => y0);
                            G(a => x1);
                            G(a => y1 + x0);   // error: connecting previously disconnected closures
                        }
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Accessing_Closure4()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                int x = 0;                     // Group #0
                                               
                void F()                       
                {                              
                    { int x0 = 0, y0 = 0;      // Group #0             
                        { int x1 = 0, y1 = 0;  // Group #1               
                                                     
                            G(a => x + x0);   
                            G(a => x0);
                            G(a => y0);
                            G(a => x1);
                            G(a => y1);
                        }
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}
                int x = 0;                     // Group #0

                void F()
                {
                    { int x0 = 0, y0 = 0;      // Group #0          
                        { int x1 = 0, y1 = 0;  // Group #1              
                                                     
                            G(a => x);         // error: disconnecting previously connected closures
                            G(a => x0);
                            G(a => y0);
                            G(a => x1);
                            G(a => y1 + x0);   // error: connecting previously disconnected closures
                        }
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Update_Accessing_Closure_NestedLambdas()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, Func<int, int>> f) {}

                void F()                       
                {                              
                    { int x0 = 0;      // Group #0             
                        { int x1 = 0;  // Group #1               
                                                     
                            G(a => b => x0);
                            G(a => b => x1);
                        }
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, Func<int, int>> f) {}

                void F()
                {
                    { int x0 = 0;      // Group #0          
                        { int x1 = 0;  // Group #1              
                                                     
                            G(a => b => x0);
                            G(a => b => x1);

                            G(a => b => x0);      // ok
                            G(a => b => x1);      // ok
                            G(a => b => x0 + x1); // runtime rude edit
                        }
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void Lambdas_CapturedLocal_Rename()
    {
        var src1 = """

            using System;

            class C
            {
                static void F()
                <N:0>{
                    int x = 1;
                    Func<int> f = () => x;
                }</N:0>
            }
            """;
        var src2 = """

            using System;

            class C
            {
                static void F()
                <N:0>{
                    int <S:0>X</S:0> = 1;
                    Func<int> f = () => X;
                }</N:0>
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), ["x", "X"])
            ]));
    }

    [Fact]
    public void Lambdas_CapturedLocal_ChangeType()
    {
        var src1 = """

            using System;

            class C
            {
                static void F()
                <N:0>{
                    int <S:0>x</S:0> = 1;
                    Func<int> f = <N:1>() => x</N:1>;
                }</N:0>
            }
            """;
        var src2 = """

            using System;

            class C
            {
                static void F()
                <N:0>{
                    byte <S:0>x</S:0> = 1;
                    Func<int> f = <N:1>() => x</N:1>;
                }</N:0>
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(
                SemanticEditKind.Update,
                c => c.GetMember("C.F"),
                syntaxMap,
                rudeEdits: [RuntimeRudeEdit(marker: 0, RudeEditKind.ChangingCapturedVariableType, syntaxMap.Position(0), ["x", "int"])]));
    }

    [Fact]
    public void Lambdas_CapturedParameter_Rename_BlockBody()
    {
        var src1 = """

            using System;

            class C
            {
                static void F(int x)
                <N:0>{
                    Func<int> f = <N:1>() => x</N:1>;
                }</N:0>
            }
            """;
        var src2 = """

            using System;

            class C
            {
                static void F(int <S:0>X</S:0>)
                <N:0>{
                    Func<int> f = <N:1>() => X</N:1>;
                }</N:0>
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap,
                    rudeEdits: [RuntimeRudeEdit(marker: 0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), ["x", "X"])]),
            ],
            capabilities: EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact]
    public void Lambdas_CapturedParameter_Rename_ExpressionBody()
    {
        var src1 = """

            using System;

            class C
            {
                static void G(Func<int> f) {}
                static void F(int x) <N:0>=> G(<N:1>() => x</N:1>)</N:0>;
            }
            """;
        var src2 = """

            using System;

            class C
            {
                static void G(Func<int> f) {}
                static void F(int <S:0>X</S:0>) <N:0>=> G(<N:1>() => X</N:1>)</N:0>;
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap,
                    rudeEdits: [RuntimeRudeEdit(marker: 0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), ["x", "X"])]),
            ],
            capabilities: EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact]
    public void Lambdas_CapturedParameter_Rename_Lambda_BlockBody()
    {
        var src1 = """

            using System;

            class C
            {
                static void F()    
                <N:0>{
                    Func<int> f1 = <N:1>x =>
                    <N:2>{
                        Func<int> f2 = <N:3>() => x</N:3>;
                    }</N:1,2>;
                }</N:0>
            }
            """;
        var src2 = """

            using System;

            class C
            {
                static void F()    
                <N:0>{
                    Func<int> f1 = <N:1>X =>
                    <N:2>{
                        Func<int> f2 = <N:3>() => X</N:3>;
                    }</N:1,2>;
                }</N:0>
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap,
                    rudeEdits: [RuntimeRudeEdit(marker: 2, RudeEditKind.RenamingCapturedVariable, syntaxMap.NodePosition(1), ["x", "X"])]),
            ],
            capabilities: EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact]
    public void Lambdas_CapturedParameter_Rename_Lambda_ExpressionBody()
    {
        var src1 = """

            using System;

            class C
            {
                static int G(Func<int> f) => 1;

                static void F()    
                {
                    Func<int, int> <N:0>f1 = <N:1>x => <N:2>G(<N:3>() => x</N:3>)</N:0,1,2>;
                }
            }
            """;
        var src2 = """

            using System;

            class C
            {
                static int G(Func<int> f) => 1;

                static void F()    
                {
                    Func<int, int> <N:0>f1 = <N:1>X => <N:2>G(<N:3>() => X</N:3>)</N:0,1,2>;
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap,
                    rudeEdits: [RuntimeRudeEdit(marker: 2, RudeEditKind.RenamingCapturedVariable, syntaxMap.NodePosition(1), ["x", "X"])]),
            ],
            capabilities: EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact]
    public void Lambdas_CapturedParameter_Rename_ConstructorDeclaration()
    {
        var src1 = """

            using System;

            class B(Func<int> f);

            class C
            {
                <N:0>C(int x, int y) : base(() => x)
                {
                    Func<int> g = () => y;
                }</N:0>
            }
            """;
        var src2 = """

            using System;

            class B(Func<int> f);

            class C
            {
                <N:0>C(int <S:0>X</S:0>, int Y) : base(() => X)
                {
                    Func<int> g = () => Y;
                }</N:0>
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C..ctor"), syntaxMap,
                    // only the first rude edit is reported for each node:
                    rudeEdits: [RuntimeRudeEdit(marker: 0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), ["x", "X"])]),
            ],
            capabilities: EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact]
    public void Lambdas_CapturedParameter_Rename_PrimaryConstructorDeclaration()
    {
        var src1 = """

            using System;

            class B(Func<int> f);

            <N:0>class C(int x) : B(() => x);</N:0>

            """;
        var src2 = """

            using System;

            class B(Func<int> f);

            <N:0>class C(int <S:0>X</S:0>) : B(() => X);</N:0>

            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C..ctor"), syntaxMap,
                    rudeEdits: [RuntimeRudeEdit(marker: 0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), ["x", "X"])]),
            ],
            capabilities: EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/68708")]
    public void Lambdas_CapturedParameter_ChangeType()
    {
        var src1 = """

            using System;

            class C
            {
                static void F(int x)
                {
                    Func<int> f = () => x;
                }
            }
            """;
        var src2 = """

            using System;

            class C
            {
                static void F(byte x)
                {
                    Func<int> f = () => x;
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.F"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.F"))
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void Lambdas_CapturedParameter_ChangeType_Indexer1()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int> this[int a] { get => () => a; }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int> this[byte a] => () => a;
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.this[]"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.this[]")),
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.get_Item"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.get_Item"))
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void Lambdas_CapturedParameter_ChangeType_Indexer_NoBodyChange()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int> this[int a ] => () => a;
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int> this[byte a] => () => a;
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.get_Item"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.get_Item")),
                SemanticEdit(SemanticEditKind.Delete, c => c.GetMember("C.this[]"), deletedSymbolContainerProvider: c => c.GetMember("C")),
                SemanticEdit(SemanticEditKind.Insert, c => c.GetMember("C.this[]")),
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void Lambdas_ReorderCapturedParameters()
    {
        var src1 = """

            using System;
            using System.Diagnostics;

            class Program
            {
                static void Main(int x, int y)
                {
                    Func<int> f = () => x + y;
                }
            }
            """;
        var src2 = """

            using System;
            using System.Diagnostics;

            class Program
            {
                static void Main(int y, int x)
                {
                    Func<int> f = () => x + y;
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            ActiveStatementsDescription.Empty,
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("Program.Main"), preserveLocalVariables: true)
            ],
            capabilities: EditAndContinueTestVerifier.Net6RuntimeCapabilities);
    }

    [Fact]
    public void Lambdas_Parameter_To_Discard1()
    {
        var src1 = "var x = new System.Func<int, int, int>((a, b) => 1);";
        var src2 = "var x = new System.Func<int, int, int>((a, _) => 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [b]@45 -> [_]@45");

        GetTopEdits(edits).VerifySemantics(SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Parameter_To_Discard2()
    {
        var src1 = "var x = new System.Func<int, int, int>((int a, int b) => 1);";
        var src2 = "var x = new System.Func<int, int, int>((_, _) => 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [int a]@42 -> [_]@42",
            "Update [int b]@49 -> [_]@45");

        GetTopEdits(edits).VerifySemantics(SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_Parameter_To_Discard3()
    {
        var src1 = "var x = new System.Func<int, int, int>((a, b) => 1);";
        var src2 = "var x = new System.Func<int, int, int>((_, _) => 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [a]@42 -> [_]@42",
            "Update [b]@45 -> [_]@45");

        GetTopEdits(edits).VerifySemantics(SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Lambdas_StackAlloc_Update()
    {
        var src1 = """

            using System;
            class C
            {
                Delegate F() => () => { Span<int> s = stackalloc int[10]; };
            }
            """;
        var src2 = """

            using System;
            class C
            {
                Delegate F() => () => { Span<int> s = stackalloc int[20]; };
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.StackAllocUpdate, "stackalloc int[20]", GetResource("lambda")));
    }

    [Fact]
    public void Lambdas_StackAlloc_Insert()
    {
        var src1 = """

            using System;
            class C
            {
                Delegate F() => () => { };
            }
            """;
        var src2 = """

            using System;
            class C
            {
                Delegate F() => () => { Span<int> s = stackalloc int[10]; };
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.StackAllocUpdate, "stackalloc int[10]", GetResource("lambda")));
    }

    [Fact]
    public void Lambdas_StackAlloc_Delete()
    {
        var src1 = """

            using System;
            class C
            {
                Delegate F() => () => { Span<int> s = stackalloc int[10]; };
            }
            """;
        var src2 = """

            using System;
            class C
            {
                Delegate F() => () => { };
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.StackAllocUpdate, "()", GetResource("lambda")));
    }

    [Fact]
    public void Lambdas_StackAlloc_UpdateAround()
    {
        var src1 = "unsafe class C { void M() { F(1, () => { int* a = stackalloc int[10]; }); } }";
        var src2 = "unsafe class C { void M() { F(2, () => { int* a = stackalloc int[10]; }); } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Lambdas_AsyncModifier_Add()
    {
        var src1 = """

            using System;
            using System.Threading.Tasks;

            class Test
            {
                public void F()
                {
                    var f = new Func<Task<int>>(() => Task.FromResult(1));
                }
            }
            """;
        var src2 = """

            using System;
            using System.Threading.Tasks;

            class Test
            {
                public void F()
                {
                    var f = new Func<Task<int>>(async () => 1);
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.MakeMethodAsyncNotSupportedByRuntime, "()")],
            capabilities: EditAndContinueCapabilities.Baseline);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddExplicitInterfaceImplementation);
    }

    [Fact]
    public void Lambdas_BodyUpdate_RestartRequired()
    {
        var src1 = RestartRequiredOnMetadataUpdateAttributeSrc + """
            public class C
            {
                public int F([RestartRequiredOnMetadataUpdateAttribute] System.Func<int> f)
                    => f();

                public void G()
                {
                    F(() => 1);
                }
            }
            """;

        var src2 = RestartRequiredOnMetadataUpdateAttributeSrc + """
            public class C
            {
                public int F([RestartRequiredOnMetadataUpdateAttribute] System.Func<int> f)
                    => f();

                public void G()
                {
                    F(() => 2);
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.UpdateMightNotHaveAnyEffect, "()", GetResource("lambda")));
    }

    [Fact]
    public void Lambdas_BodyUpdate_RestartRequired_ContainingMethod()
    {
        var src1 = RestartRequiredOnMetadataUpdateAttributeSrc + """
            public class C
            {
                public int F(System.Func<int> f)
                    => f();

                public void G()
                {
                    F(() => 1);
                }
            }
            """;

        var src2 = RestartRequiredOnMetadataUpdateAttributeSrc + """
            public class C
            {
                public int F(System.Func<int> f)
                    => f();

                [RestartRequiredOnMetadataUpdateAttribute]
                public void G()
                {
                    F(() => 2);
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        // UpdateMightNotHaveAnyEffect not reported since the change is in the lambda body:
        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.ChangeCustomAttributes);
    }

    #endregion

    #region Local Functions

    [Fact]
    public void LocalFunctions_InExpressionStatement()
    {
        var src1 = "F(a => a, b => b);";
        var src2 = "int x(int a) => a + 1; F(b => b, x);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [a => a]@4 -> [int x(int a) => a + 1;]@2",
            "Move [a => a]@4 -> @2",
            "Update [F(a => a, b => b);]@2 -> [F(b => b, x);]@25",
            "Insert [(int a)]@7",
            "Insert [int a]@8",
            "Delete [a]@4");
    }

    [Fact]
    public void LocalFunctions_ReorderAndUpdate()
    {
        var src1 = "int x(int a) => a; int y(int b) => b;";
        var src2 = "int y(int b) => b; int x(int a) => a + 1;";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [int y(int b) => b;]@21 -> @2",
            "Update [int x(int a) => a;]@2 -> [int x(int a) => a + 1;]@21");
    }

    [Fact]
    public void LocalFunctions_InWhile()
    {
        var src1 = "do { /*1*/ } while (F(x));int x(int a) => a + 1;";
        var src2 = "while (F(a => a)) { /*1*/ }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Insert [while (F(a => a)) { /*1*/ }]@2",
            "Update [int x(int a) => a + 1;]@28 -> [a => a]@11",
            "Move [int x(int a) => a + 1;]@28 -> @11",
            "Move [{ /*1*/ }]@5 -> @20",
            "Insert [a]@11",
            "Delete [do { /*1*/ } while (F(x));]@2",
            "Delete [(int a)]@33",
            "Delete [int a]@34");
    }

    [Fact]
    public void LocalFunctions_InLocalFunction_NoChangeInSignature()
    {
        var src1 = "int x() { int y(int a) => a; return y(b); }";
        var src2 = "int x() { int y() => c; return y(); }";

        var edits = GetMethodEdits(src1, src2);
        // no changes to the method were made, only within the outer local function body:
        edits.VerifyEdits();
    }

    [Fact]
    public void LocalFunctions_InLocalFunction_ChangeInSignature()
    {
        var src1 = "int x() { int y(int a) => a; return y(b); }";
        var src2 = "int x(int z) { int y() => c; return y(); }";

        var edits = GetMethodEdits(src1, src2);
        // changes were made to the outer local function signature:
        edits.VerifyEdits("Insert [int z]@8");
    }

    [Fact]
    public void LocalFunctions_InLambda()
    {
        var src1 = "F(() => { int y(int a) => a; G(y); });";
        var src2 = "F(q => { G(() => y); });";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [() => { int y(int a) => a; G(y); }]@4 -> [q => { G(() => y); }]@4",
            "Insert [q]@4",
            "Delete [()]@4");
    }

    [Fact]
    public void LocalFunctions_Update_ParameterRefness_NoBodyChange()
    {
        var src1 = @"void f(ref int a) => a = 1;";
        var src2 = @"void f(out int a) => a = 1;";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [ref int a]@9 -> [out int a]@9");
    }

    [Fact]
    public void LocalFunctions_Insert_Static()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int f(int a) => a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void LocalFunctions_Insert_Static_InGenericContext_Method()
    {
        var src1 = """

            using System;

            class C
            {
                void F<T>()
                {
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F<T>()
                {
                    int f(int a) => a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.AddMethodToExistingType |
                EditAndContinueCapabilities.GenericAddMethodToExistingType |
                EditAndContinueCapabilities.GenericUpdateMethod);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "f", GetResource("local function")),
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "void F<T>()", GetResource("method"))
            ],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunctions_Insert_Static_InGenericContext_Type()
    {
        var src1 = """

            using System;

            class C<T>
            {
                void F()
                {
                }
            }

            """;
        var src2 = """

            using System;

            class C<T>
            {
                void F()
                {
                    int f(int a) => a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.AddMethodToExistingType |
                EditAndContinueCapabilities.GenericAddMethodToExistingType |
                EditAndContinueCapabilities.GenericUpdateMethod);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "f", GetResource("local function")),
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "void F()", GetResource("method"))
            ],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunctions_Insert_Static_InGenericContext_LocalFunction()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    void L<T>()
                    {
                        void M()
                        {
                        }
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    void L<T>()
                    {
                        void M()
                        {
                            int f(int a) => a;
                        }
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.AddMethodToExistingType |
                EditAndContinueCapabilities.GenericAddMethodToExistingType |
                EditAndContinueCapabilities.GenericUpdateMethod);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "L", GetResource("local function")),
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "f", GetResource("local function"))
            ],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunctions_Insert_Static_Nested_ExpressionBodies()
    {
        var src1 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;
                
                void F()
                {
                    int localF(int a) => a;
                    G(localF);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;

                void F()
                {
                    int localF(int a) => a;
                    int localG(int a) => G(localF) + a;
                    G(localG);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void LocalFunctions_Insert_Static_Nested_BlockBodies()
    {
        var src1 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;
                
                void F()
                {
                    int localF(int a) { return a; }
                    G(localF);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;

                void F()
                {
                    int localF(int a) { return a; }
                    int localG(int a) { return G(localF) + a; }
                    G(localG);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void LocalFunctions_LocalFunction_Replace_Lambda()
    {
        var src1 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;
                
                void F()
                {
                    G(<N:0>a => a</N:0>);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;

                void F()
                {
                    <N:0>int <S:0>localF</S:0>(int a) { return a; }</N:0>
                    G(localF);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.SwitchBetweenLambdaAndLocalFunction, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Fact]
    public void LocalFunctions_Lambda_Replace_LocalFunction()
    {
        var src1 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;

                void F()
                {
                    <N:0>int localF(int a) { return a; }</N:0>
                    G(localF);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static int G(Func<int, int> f) => 0;
                
                void F()
                {
                    G(<N:0>a => a</N:0>);
                }
            }

            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.SwitchBetweenLambdaAndLocalFunction, syntaxMap.NodePosition(0), arguments: [])
            ]));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Insert_ThisOnly_Top1()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 0;

                void F()
                {
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 0;

                void F()
                {
                    int G(int a) => x; 
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/1291"), WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Insert_ThisOnly_Top2()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    int y = 1;
                    {
                        int x = 2;
                        int f1(int a) => y; 
                    }
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    int y = 1;
                    {
                        int x = 2;
                        var f2 = from a in new[] { 1 } select a + y;
                        var f3 = from a in new[] { 1 } where x > 0 select a;
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Insert_ThisOnly_Nested1()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 0;
                int G(Func<int, int> f) => 0;

                void F()
                {
                    int f(int a) => a;
                    G(f);
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 0;
                int G(Func<int, int> f) => 0;

                void F()
                {
                    int f(int a) => x;
                    int g(int a) => G(f);
                    G(g);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Insert_ThisOnly_Nested2()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 0;

                void F()
                {
                    int f1(int a) 
                    {
                        int f2(int b)
                        {
                            return b;
                        };

                        return a;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 0;

                void F()
                {
                    int f1(int a)
                    {
                        int f2(int b)
                        {
                            return b;
                        };

                        int f3(int c)
                        {
                            return c + x;
                        };

                        return a;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_InsertAndDelete_Scopes1()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                int x = 0, y = 0;                      // Group #0

                void F()
                {
                    int x0 = 0, y0 = 0;                // Group #1 

                    { int x1 = 0, y1 = 0;              // Group #2 

                        { int x2 = 0, y2 = 0;          // Group #1 

                            { int x3 = 0, y3 = 0;      // Group #2 

                                int f1(int a) => x3 + x1;
                                int f2(int a) => x0 + y0 + x2;
                                int f3(int a) => x;
                            }
                        }
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                int x = 0, y = 0;                       // Group #0

                void F()
                {
                    int x0 = 0, y0 = 0;                 // Group #1

                    { int x1 = 0, y1 = 0;               // Group #2 

                        { int x2 = 0, y2 = 0;           // Group #1

                            { int x3 = 0, y3 = 0;       // Group #2 

                                int f1(int a) => x3 + x1;
                                int f2(int a) => x0 + y0 + x2;
                                int f3(int a) => x;

                                int f4(int a) => x;         // OK
                                int f5(int a) => x0 + y0;   // OK
                                int f6(int a) => x1 + y0;   // runtime rude edit - connecting Group #1 and Group #2
                                int f7(int a) => x3 + x1;   // runtime rude edit - multi-scope (conservative)
                                int f8(int a) => x + y0;    // runtime rude edit - connecting Group #0 and Group #1
                                int f9(int a) => x + x3;    // runtime rude edit - connecting Group #0 and Group #2
                            }
                        }
                    }
                }
            }

            """;
        var insert = GetTopEdits(src1, src2);
        insert.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);

        var delete = GetTopEdits(src2, src1);
        delete.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Insert_ForEach1()
    {
        var src1 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                void F()                       
                {                              
                    foreach (int x0 in new[] { 1 })  // Group #0             
                    {                                // Group #1
                        int x1 = 0;

                        int f0(int a) => x0;
                        int f1(int a) => x1;
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G(Func<int, int> f) {}

                void F()                       
                {                              
                    foreach (int x0 in new[] { 1 })  // Group #0             
                    {                                // Group #1
                        int x1 = 0;                  

                        int f0(int a) => x0;
                        int f1(int a) => x1;

                        int f2(int a) => x0 + x1;   // runtime rude edit: connecting previously disconnected closures
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Insert_Switch1()
    {
        var src1 = """

            using System;

            class C
            {
                bool G(Func<int> f) => true;

                int a = 1;

                void F()                       
                {        
                    int x2 = 1;
                    int f2() => x2;

                    switch (a)
                    {
                        case 1:
                            int x0 = 1;
                            int f0() => x0;
                            break;

                        case 2:
                            int x1 = 1;
                            int f1() => x1;
                            break;
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                bool G(Func<int> f) => true;

                int a = 1;

                void F()                       
                {                
                    int x2 = 1;
                    int f2() => x2;

                    switch (a)
                    {
                        case 1:
                            int x0 = 1;
                            int f0() => x0;
                            goto case 2;

                        case 2:
                            int x1 = 1;
                            int f1() => x1;
                            goto default;

                        default:
                            x0 = 1;
                            x1 = 2;
                            int f01() => x0 + x1;   // ok
                            int f02() => x0 + x2;   // runtime rude edit
                            break;
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Insert_Catch1()
    {
        var src1 = """

            using System;

            class C
            {
                static void F()                       
                {                              
                    try
                    {
                    }
                    catch (Exception x0)
                    {
                        int x1 = 1;
                        int f0() => x0;
                        int f1() => x1;
                    }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                static void F()                       
                {                              
                    try
                    {
                    }
                    catch (Exception x0)
                    {
                        int x1 = 1;
                        int f0() => x0;
                        int f1() => x1;

                        int f00() => x0; //ok
                        int f01() => F(x0, x1); // runtime rude edit
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void LocalFunctions_Insert_NotSupported()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    void M()
                    {
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "M", FeaturesResources.local_function)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_This()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int f(int a) => a + x;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int f(int a) => a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void LocalFunctions_Update_Signature1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>int f(int a) => a;</N:0>
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>long <S:0>f</S:0>(long a) => a;</N:0>
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Fact]
    public void LocalFunctions_Update_Signature2()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>int f(int a) => a;</N:0>
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>int <S:0>f</S:0>(int a, int b) => a + b;</N:0>
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Fact]
    public void LocalFunctions_Update_Signature3()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>int f(int a) => a;</N:0>
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>long <S:0>f</S:0>(int a) => a;</N:0>
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Fact]
    public void LocalFunctions_Update_Signature_ReturnType1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>int f(int a) { return 1; }</N:0>
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>void <S:0>f</S:0>(int a) { }</N:0>
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Fact]
    public void LocalFunctions_Update_Signature_BodySyntaxOnly()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int f(int a) => a;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void G1(Func<int, int> f) {}
                void G2(Func<int, int> f) {}

                void F()
                {
                    int f(int a) { return a; }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void LocalFunctions_Update_Signature_ParameterName1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int f(int a) => 1;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int f(int b) => 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunctions_Update_Signature_ParameterRefness1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>int <S:0>f</S:0>(ref int a) => 1;</N:0>
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    <N:0>int <S:0>f</S:0>(int a) => 2;</N:0>
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Fact]
    public void LocalFunctions_Update_Signature_ParameterRefness2()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int f(out int a) => 1;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int f(ref int a) => 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void LocalFunctions_Update_Signature_ParameterRefness3()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int f(ref int a) => 1;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int f(out int a) => 1;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void LocalFunctions_Signature_SemanticErrors()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    Unknown f(Unknown a) => 1;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    Unknown f(Unknown a) => 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        // There are semantics errors in the case. The errors are captured during the emit execution.
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunctions_Update_CapturedParameters1()
    {
        var src1 = """

            using System;

            class C
            {
                void F(int x1)
                {
                    int f1(int a1, int a2)
                    {
                        int f2(int a3) => x1 + a2;
                        return a1;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F(int x1)
                {
                    int f1(int a1, int a2)
                    {
                        int f2(int a3) => x1 + a2 + 1;
                        return a1;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/2223")]
    public void LocalFunctions_Update_CapturedParameters2()
    {
        var src1 = """

            using System;

            class C
            {
                void F(int x1)
                {
                    int f1(int a1, int a2)
                    {
                        int f2(int a3) => x1 + a2;
                        return a1;
                    };

                    int f3(int a1, int a2)
                    {
                        int f4(int a3) => x1 + a2;
                        return a1;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F(int x1)
                {
                    int f1(int a1, int a2)
                    {
                        int f2(int a3) => x1 + a2 + 1;
                        return a1;
                    };

                    int f3(int a1, int a2)
                    {
                        int f4(int a3) => x1 + a2 + 1;
                        return a1;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_Closure1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    int f1(int a1)
                    {
                        int f2(int a2) => y + a2;
                        return a1;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    int f1(int a1)
                    {
                        int f2(int a2) => a2;
                        return a1 + y;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        // y is no longer captured in f2
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_IndexerParameter()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { int f(int a3) => a1 + a2; return f; } }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { int f(int a3) => a2; return f; } }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_MethodParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                void F(int a1, int a2)
                {
                    int f2(int a3) => a1 + a2;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F(int a1, int a2)
                {
                    int f2(int a3) => a1;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_LambdaParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int f1(int a1, int a2)
                    {
                        int f2(int a3) => a1 + a2;
                        return a1;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int f1(int a1, int a2)
                    {
                        int f2(int a3) => a2;
                        return a1;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_SetterValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                int D
                {
                    get { return 0; }
                    set { void f() { Console.Write(value); } f(); }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int D
                {
                    get { return 0; }
                    set { }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.set_D")));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_IndexerSetterValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                int this[int a1, int a2]
                {
                    get { return 0; }
                    set { void f() { Console.Write(value); } f(); }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int this[int a1, int a2]
                {
                    get { return 0; }
                    set { }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.set_Item")));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_EventAdderValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                event Action D
                {
                    add { void f() { Console.Write(value); } f(); }
                    remove { }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                event Action D
                {
                    add {  }
                    remove { }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.add_D")));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_CeaseCapture_EventRemoverValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                event Action D
                {
                    add { }
                    remove { void f() { Console.Write(value); } f(); }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                event Action D
                {
                    add { }
                    remove { }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.remove_D")));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_DeleteCapture1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    int f1(int a1)
                    {
                        int f2(int a2) => y + a2;
                        return y;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                { // error
                    int f1(int a1)
                    {
                        int f2(int a2) => a2;
                        return a1;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_Capturing_IndexerGetterParameter2()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { int f(int a3) => a2; return f; } }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { int f(int a3) => a1 + a2; return f; } }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.get_Item"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_Capturing_IndexerSetterParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { return null; } set { int f(int a3) => a2; } }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                Func<int, int> this[int a1, int a2] { get { return null; } set { int f(int a3) => a1 + a2; } }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.set_Item"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_Capturing_IndexerSetterValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                int this[int a1, int a2]
                {
                    get { return 0; }
                    set {  }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int this[int a1, int a2]
                {
                    get { return 0; }
                    set { void f() { Console.Write(value); } f(); }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.set_Item"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_Capturing_EventAdderValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                event Action D
                {
                    add {  }
                    remove { }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                event Action D
                {
                    add {  }
                    remove { void f() { Console.Write(value); } f(); }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.remove_D"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_Capturing_EventRemoverValueParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                event Action D
                {
                    add { }
                    remove {  }
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                event Action D
                {
                    add { }
                    remove { void f() { Console.Write(value); } f(); }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.remove_D"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_Capturing_MethodParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                void F(int a1, int a2)
                {
                    int f2(int a3) => a1;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F(int a1, int a2)
                {
                    int f2(int a3) => a1 + a2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_Capturing_LambdaParameter1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int f1(int a1, int a2)
                    {
                        int f2(int a3) => a2;
                        return a1;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int f1(int a1, int a2)
                    {
                        int f2(int a3) => a1 + a2;
                        return a1;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_StaticToThisOnly1()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int f(int a) => a;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int f(int a) => a + x;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_StaticToThisOnly_Partial()
    {
        var src1 = """

            using System;

            partial class C
            {
                int x = 1;
                partial void F(); // def
            }

            partial class C
            {
                partial void F()  // impl
                {
                    int f(int a) => a;
                }
            }

            """;
        var src2 = """

            using System;

            partial class C
            {
                int x = 1;
                partial void F(); // def
            }

            partial class C
            {
                partial void F()  // impl
                {
                    int f(int a) => a + x;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember<IMethodSymbol>("C.F").PartialImplementationPart, preserveLocalVariables: true, partialType: "C"));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_StaticToThisOnly3()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int f1(int a1) => a1;
                    int f2(int a2) => a2 + x;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int f1(int a1) => a1 + x;
                    int f2(int a2) => a2 + x;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_StaticToClosure1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int x = 1;
                    int f1(int a1) => a1;
                    int f2(int a2) => a2 + x;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                { 
                    int x = 1;       
                    int f1(int a1) 
                    {
                        return a1 + 
                            x+ // 1 
                            x; // 2
                    };

                    int f2(int a2) => a2 + x;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_ThisOnlyToClosure1()
    {
        var src1 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int y = 1;
                    int f1(int a1) => a1 + x;
                    int f2(int a2) => a2 + x + y;
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                int x = 1;

                void F()
                {
                    int y = 1;
                    int f1(int a1) => a1 + x + y;
                    int f2(int a2) => a2 + x + y;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void LocalFunctions_Update_Nested1()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    int f1(int a1) 
                    {
                        int f2(int a2) => a2 + y;
                        return a1;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    int f1(int a1) 
                    {
                        int f2(int a2) => a2 + y;
                        return a1 + y;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_Update_Nested2()
    {
        var src1 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    int f1(int a1)
                    {
                        int f2(int a2) => a2;
                        return a1;
                    };
                }
            }

            """;
        var src2 = """

            using System;

            class C
            {
                void F()
                {
                    int y = 1;
                    int f1(int a1)
                    {
                        int f2(int a2) => a1 + a2;
                        return a1;
                    };
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void LocalFunctions_Update_Generic()
    {
        var src1 = """

            class C
            {
                void F()
                {
                    int L<T>() => 1;
                    int M<T>() => 1;
                    int N<T>() => 1;
                    int O<T>() => 1;
                }
            }
            """;
        var src2 = """

            class C
            {
                void F()
                {
                    int L<T>() => 1;
                    int M<T>() => 2;
                    int N<T>() => 1 ;
                    int O<T>() =>  1;
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "M", GetResource("local function"))
            ],
            capabilities: EditAndContinueCapabilities.Baseline);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.GenericUpdateMethod);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void LocalFunctions_RenameCapturedLocal()
    {
        var src1 = """

            using System;
            using System.Diagnostics;

            class C
            {
                static void F()
                <N:0>{
                    int x = 1;
                    int f() => x;
                }</N:0>
            }
            """;
        var src2 = """

            using System;
            using System.Diagnostics;

            class C
            {
                static void F()
                <N:0>{
                    int <S:0>X</S:0> = 1;
                    int f() => X;
                }</N:0>
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), ["x", "X"])
                ])
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void LocalFunctions_RenameCapturedParameter()
    {
        var src1 = """

            using System;
            using System.Diagnostics;

            class C
            {
                static void F(int x)
                <N:0>{
                    int f() => x;
                }</N:0>
            }
            """;
        var src2 = """

            using System;
            using System.Diagnostics;

            class C
            {
                static void F(int <S:0>X</S:0>)
                <N:0>{
                    int f() => X;
                }</N:0>
            }
            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.RenamingCapturedVariable, syntaxMap.Position(0), ["x", "X"])
                ])
            ],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.UpdateParameters);
    }

    [Fact]
    public void LocalFunctions_Update()
    {
        var src1 = """

            using System;

            class C
            {
                void M()
                {
                    N(3);
                    
                    void N(int x)
                    {
                        if (x > 3)
                        {
                            x.ToString();
                        }
                        else
                        {
                            N(x - 1);
                        }
                    }
                }
            }
            """;
        var src2 = """

            using System;

            class C
            {
                void M()
                {
                    N(3);
                    
                    void N(int x)
                    {
                        Console.WriteLine("Hello");
                    }
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.M"), preserveLocalVariables: true));
    }

    [Fact]
    public void LocalFunction_In_Parameter_InsertWhole()
    {
        var src1 = @"class Test { void M() { } }";
        var src2 = @"class Test { void M() { void local(in int b) { throw null; } } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [void M() { }]@13 -> [void M() { void local(in int b) { throw null; } }]@13");

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void LocalFunction_In_Parameter_InsertParameter()
    {
        var src1 = @"class C { void F() { <N:0>void local() { throw null; }</N:0> } }";
        var src2 = @"class C { void F() { <N:0>void <S:0>local</S:0>(in int b) { throw null; }</N:0> } }";

        var edits = GetTopEdits(src1, src2);

        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.Position(0), arguments: [GetResource("local function")])
            ]));
    }

    [Fact]
    public void LocalFunction_In_Parameter_Update()
    {
        var src1 = @"class C { void F() { <N:0>void local(int b) { throw null; }</N:0> } }";
        var src2 = @"class C { void F() { <N:0>void <S:0>local</S:0>(in int b) { throw null; }</N:0> } }";

        var edits = GetTopEdits(src1, src2);

        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaParameters, syntaxMap.Position(0), arguments: [GetResource("local function")])
            ]));
    }

    [Fact]
    public void LocalFunction_ReadOnlyRef_ReturnType_Insert()
    {
        var src1 = @"class Test { void M() { } }";
        var src2 = @"class Test { void M() { ref readonly int local() { throw null; } } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [void M() { }]@13 -> [void M() { ref readonly int local() { throw null; } }]@13");

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void LocalFunction_ReadOnlyRef_ReturnType_Update()
    {
        var src1 = @"class C { void F() { <N:0>int local() { throw null; }</N:0> } }";
        var src2 = @"class C { void F() { <N:0>ref readonly int <S:0>local</S:0>() { throw null; }</N:0> } }";

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
            [
                RuntimeRudeEdit(0, RudeEditKind.ChangingLambdaReturnType, syntaxMap.Position(0), arguments: [GetResource("local function")])
            ]));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/37128")]
    public void LocalFunction_AddToInterfaceMethod()
    {
        var src1 = """

            using System;
            interface I
            {
                static int X = M(() => 1);
                static int M() => 1;

                static void F()
                {
                    void g() { }
                }
            }

            """;
        var src2 = """

            using System;
            interface I
            {
                static int X = M(() => { void f3() {} return 2; });
                static int M() => 1;

                static void F()
                {
                    int f1() => 1;
                    f1();

                    void g() { void f2() {} f2(); }

                    var l = new Func<int>(() => 1);
                    l();
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        // lambdas are ok as they are emitted to a nested type
        edits.VerifySemanticDiagnostics(
            targetFrameworks: [TargetFramework.NetCoreApp],
            diagnostics:
            [
                Diagnostic(RudeEditKind.InsertLocalFunctionIntoInterfaceMethod, "f1", FeaturesResources.local_function),
                Diagnostic(RudeEditKind.InsertLocalFunctionIntoInterfaceMethod, "f2", FeaturesResources.local_function),
                Diagnostic(RudeEditKind.InsertLocalFunctionIntoInterfaceMethod, "f3", FeaturesResources.local_function)
            ]);
    }

    [Fact]
    public void LocalFunction_AddStatic()
    {
        var src1 = @"class Test { void M() { int local() { throw null; } } }";
        var src2 = @"class Test { void M() { static int local() { throw null; } } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [void M() { int local() { throw null; } }]@13 -> [void M() { static int local() { throw null; } }]@13");

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunction_RemoveStatic()
    {
        var src1 = @"class Test { void M() { static int local() { throw null; } } }";
        var src2 = @"class Test { void M() { int local() { throw null; } } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [void M() { static int local() { throw null; } }]@13 -> [void M() { int local() { throw null; } }]@13");

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunction_AddUnsafe()
    {
        var src1 = @"class Test { void M() { int local() { throw null; } } }";
        var src2 = @"class Test { void M() { unsafe int local() { throw null; } } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [void M() { int local() { throw null; } }]@13 -> [void M() { unsafe int local() { throw null; } }]@13");

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunction_RemoveUnsafe()
    {
        var src1 = @"class Test { void M() { unsafe int local() { throw null; } } }";
        var src2 = @"class Test { void M() { int local() { throw null; } } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [void M() { unsafe int local() { throw null; } }]@13 -> [void M() { int local() { throw null; } }]@13");

        edits.VerifySemanticDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/37054")]
    public void LocalFunction_AddAsync()
    {
        var src1 = @"class Test { void M() { Task<int> local() => throw null; } }";
        var src2 = @"class Test { void M() { async Task<int> local() => throw null; } }";

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/37054")]
    public void LocalFunction_RemoveAsync()
    {
        var src1 = @"class Test { void M() { async int local() { throw null; } } }";
        var src2 = @"class Test { void M() { int local() { throw null; } } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.ChangingFromAsynchronousToSynchronous, "local", FeaturesResources.local_function));
    }

    [Fact]
    public void LocalFunction_AddAttribute()
    {
        var src1 = "void L() { }";
        var src2 = "[A]void L() { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [void L() { }]@2 -> [[A]void L() { }]@2");

        // Get top edits so we can validate rude edits
        GetTopEdits(edits).VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.local_function)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_RemoveAttribute()
    {
        var src1 = "[A]void L() { }";
        var src2 = "void L() { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [[A]void L() { }]@2 -> [void L() { }]@2");

        // Get top edits so we can validate rude edits
        GetTopEdits(edits).VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.local_function)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_ReorderAttribute()
    {
        var src1 = "[A, B]void L() { }";
        var src2 = "[B, A]void L() { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [[A, B]void L() { }]@2 -> [[B, A]void L() { }]@2");

        // Get top edits so we can validate rude edits
        GetTopEdits(edits).VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunction_CombineAttributeLists()
    {
        var src1 = "[A][B]void L() { }";
        var src2 = "[A, B]void L() { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [[A][B]void L() { }]@2 -> [[A, B]void L() { }]@2");

        GetTopEdits(edits).VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunction_SplitAttributeLists()
    {
        var src1 = "[A, B]void L() { }";
        var src2 = "[A][B]void L() { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [[A, B]void L() { }]@2 -> [[A][B]void L() { }]@2");

        GetTopEdits(edits).VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunction_ChangeAttributeListTarget1()
    {
        var src1 = "[return: A]void L() { }";
        var src2 = "[A]void L() { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [[return: A]void L() { }]@2 -> [[A]void L() { }]@2");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.local_function),
                Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.local_function)
            ],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_ChangeAttributeListTarget2()
    {
        var src1 = "[A]void L() { }";
        var src2 = "[return: A]void L() { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [[A]void L() { }]@2 -> [[return: A]void L() { }]@2");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.local_function),
                Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.local_function)
            ],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_ReturnType_AddAttribute()
    {
        var src1 = "int L() { return 1; }";
        var src2 = "[return: A]int L() { return 1; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [int L() { return 1; }]@2 -> [[return: A]int L() { return 1; }]@2");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.local_function)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_ReturnType_RemoveAttribute()
    {
        var src1 = "[return: A]int L() { return 1; }";
        var src2 = "int L() { return 1; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [[return: A]int L() { return 1; }]@2 -> [int L() { return 1; }]@2");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.local_function)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_ReturnType_ReorderAttribute()
    {
        var src1 = "[return: A, B]int L() { return 1; }";
        var src2 = "[return: B, A]int L() { return 1; }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [[return: A, B]int L() { return 1; }]@2 -> [[return: B, A]int L() { return 1; }]@2");

        GetTopEdits(edits).VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunction_Parameter_AddAttribute()
    {
        var src1 = "void L(int i) { }";
        var src2 = "void L([A]int i) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [int i]@9 -> [[A]int i]@9");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.parameter)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_Parameter_RemoveAttribute()
    {
        var src1 = "void L([A]int i) { }";
        var src2 = "void L(int i) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [[A]int i]@9 -> [int i]@9");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.parameter)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_Parameter_ReorderAttribute()
    {
        var src1 = "void L([A, B]int i) { }";
        var src2 = "void L([B, A]int i) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [[A, B]int i]@9 -> [[B, A]int i]@9");

        GetTopEdits(edits).VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunction_TypeParameter_AddAttribute()
    {
        var src1 = "void L<T>(T i) { }";
        var src2 = "void L<[A] T>(T i) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [T]@9 -> [[A] T]@9");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.type_parameter)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_TypeParameter_RemoveAttribute()
    {
        var src1 = "void L<[A] T>(T i) { }";
        var src2 = "void L<T>(T i) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [[A] T]@9 -> [T]@9");

        GetTopEdits(edits).VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.ChangingAttributesNotSupportedByRuntime, "L", FeaturesResources.type_parameter)],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void LocalFunction_TypeParameter_ReorderAttribute()
    {
        var src1 = "void L<[A, B] T>(T i) { }";
        var src2 = "void L<[B, A] T>(T i) { }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits("Update [[A, B] T]@9 -> [[B, A] T]@9");

        GetTopEdits(edits).VerifySemanticDiagnostics();
    }

    [Theory]
    // insert:
    [InlineData("void L<A>() {}", "void <S:0>L</S:0><A,B>() {}", new[] { "Update [<A>]@13 -> [<A,B>]@24", "Insert [B]@27" })]
    [InlineData("void L() {}", "void <S:0>L</S:0><A>() {}", new[] { "Insert [<A>]@24", "Insert [A]@25" })]
    // delete:
    [InlineData("void L<A>() {}", "void <S:0>L</S:0>() {}", new[] { "Delete [<A>]@13", "Delete [A]@14" })]
    [InlineData("void L<A,B>() {}", "void <S:0>L</S:0><B>() {}", new[] { "Update [<A,B>]@13 -> [<B>]@24", "Delete [A]@14" })]
    // update:
    [InlineData("void L<A>() {}", "void <S:0>L</S:0><B>() {}", new[] { "Update [A]@14 -> [B]@25" })]
    // reorder:
    [InlineData("void L<A,B>() {}", "void <S:0>L</S:0><B,A>() {}", new[] { "Reorder [B]@16 -> @25" })]
    // reorder and update:
    [InlineData("void L<A,B>() {}", "void <S:0>L</S:0><B,C>() {}", new[] { "Reorder [B]@16 -> @25", "Update [A]@14 -> [C]@27" })]
    public void VerifyChangingLocalFunctionTypeParameters(string localFunctionSource1, string localFunctionSource2, string[] expectedMethodEdits)
    {
        var src1 = $"<N:0>{localFunctionSource1}</N:0>";
        var src2 = $"<N:0>{localFunctionSource2}</N:0>";

        GetMethodEdits(src1, src2).VerifyEdits(expectedMethodEdits);

        var edits = GetTopEdits(src1, src2, MethodKind.Regular);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingTypeParameters, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Theory]
    [InlineData("Enum", "Delegate")]
    [InlineData("IDisposable", "IDisposable, new()")]
    public void LocalFunctions_TypeParameter_Constraint_Clause_Update(string oldConstraint, string newConstraint)
    {
        var src1 = "<N:0>void L<A>() where A : " + oldConstraint + " {}</N:0>";
        var src2 = "<N:0>void <S:0>L</S:0><A>() where A : " + newConstraint + " {}</N:0>";

        GetMethodEdits(src1, src2).VerifyEdits(
            "Update [where A : " + oldConstraint + "]@19 -> [where A : " + newConstraint + "]@30");

        var edits = GetTopEdits(src1, src2, MethodKind.Regular);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingTypeParameters, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Theory]
    [InlineData("nonnull")]
    [InlineData("struct")]
    [InlineData("class")]
    [InlineData("new()")]
    [InlineData("unmanaged")]
    [InlineData("System.IDisposable")]
    [InlineData("System.Delegate")]
    public void LocalFunctions_TypeParameter_Constraint_Clause_Delete(string oldConstraint)
    {
        var src1 = "<N:0>void L<A>() where A : " + oldConstraint + " {}</N:0>";
        var src2 = "<N:0>void <S:0>L</S:0><A>() {}</N:0>";

        GetMethodEdits(src1, src2).VerifyEdits(
            "Delete [where A : " + oldConstraint + "]@19");

        var edits = GetTopEdits(src1, src2, MethodKind.Regular);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingTypeParameters, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Fact]
    public void LocalFunctions_TypeParameter_Constraint_Clause_Add()
    {
        var src1 = "<N:0>void L<A,B>() where A : new() {}</N:0>";
        var src2 = "<N:0>void <S:0>L</S:0><A,B>() where A : new() where B : System.IDisposable {}</N:0>";

        GetMethodEdits(src1, src2).VerifyEdits(
            "Insert [where B : System.IDisposable]@48");

        var edits = GetTopEdits(src1, src2, MethodKind.Regular);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingTypeParameters, syntaxMap.Position(0), [GetResource("local function")])
                ])
            ]);
    }

    [Fact]
    public void LocalFunctions_Stackalloc_Update()
    {
        var src1 = """

            using System;
            class C
            {
                void F()
                {
                    void L1()
                    {
                        Span<int> s = stackalloc int[10];
                    }
                    void L2()
                    {
                        Span<int> s = stackalloc int[10];
                    }
                }
            }
            """;
        var src2 = """

            using System;
            class C
            {
                void F()
                {
                    void L1()
                    {
                        Span<int> s = stackalloc int[20];
                    }
                    void L2()
                    {
                        Span<int> s = stackalloc int[10 ];
                    }
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.StackAllocUpdate, "stackalloc int[20]", GetResource("local function")));
    }

    [Fact]
    public void LocalFunctions_Stackalloc_Insert()
    {
        var src1 = """

            using System;
            class C
            {
                void F()
                {
                    void L1()
                    {
                    }
                }
            }
            """;
        var src2 = """

            using System;
            class C
            {
                void F()
                {
                    void L1()
                    {
                        Span<int> s = stackalloc int[10];
                    }
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.StackAllocUpdate, "stackalloc int[10]", GetResource("local function")));
    }

    [Fact]
    public void LocalFunctions_Stackalloc_Delete()
    {
        var src1 = """

            using System;
            class C
            {
                void F()
                {
                    void L1()
                    {
                        Span<int> s = stackalloc int[10];
                    }
                }
            }
            """;
        var src2 = """

            using System;
            class C
            {
                void F()
                {
                    void L1()
                    {
                    }
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.StackAllocUpdate, "L1", GetResource("local function")));
    }

    [Fact]
    public void LocalFunctions_Stackalloc_InNestedLocalFunction()
    {
        var src1 = """

            using System;
            class C
            {
                void F()
                {
                    void L()
                    {
                        void M()
                        {
                            Span<int> s = stackalloc int[10];
                        }

                        Console.WriteLine(1);
                    }
                }
            }
            """;
        var src2 = """

            using System;
            class C
            {
                void F()
                {
                    void L()
                    {
                        void M()
                        {
                            Span<int> s = stackalloc int[10];
                        }

                        Console.WriteLine(2);
                    }
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void LocalFunctions_AsyncModifier_Add()
    {
        var src1 = """

            class Test
            {
                public void F()
                {
                    Task<int> WaitAsync()
                    {
                        return 1;
                    }
                }
            }
            """;
        var src2 = """

            class Test
            {
                public void F()
                {
                    async Task<int> WaitAsync()
                    {
                        await Task.Delay(1000);
                        return 1;
                    }
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.MakeMethodAsyncNotSupportedByRuntime, "WaitAsync")],
            capabilities: EditAndContinueCapabilities.Baseline);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddExplicitInterfaceImplementation);
    }

    [Fact]
    public void LocalFunctions_Iterator_Add()
    {
        var src1 = """

            class Test
            {
                public void F()
                {
                    IEnumerable<int> Iter()
                    {
                        return null;
                    }
                }
            }
            """;
        var src2 = """

            class Test
            {
                public void F()
                {
                    IEnumerable<int> Iter()
                    {
                        yield return 1;
                    }
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics(
            [Diagnostic(RudeEditKind.MakeMethodIteratorNotSupportedByRuntime, "Iter")],
            capabilities: EditAndContinueCapabilities.Baseline);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddExplicitInterfaceImplementation);
    }

    [Fact]
    public void LocalFunctions_WithoutBody_SemanticError()
    {
        var src1 = "Console.WriteLine(1); int C(int X);";
        var src2 = "Console.WriteLine(2); int C(int X);";
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("Program.<Main>$"))],
            [Diagnostic(RudeEditKind.UpdateMightNotHaveAnyEffect, "2", GetResource("top-level code"))]);
    }

    [Fact]
    public void LocalFunctions_BodyUpdate_RestartRequired()
    {
        var src1 = RestartRequiredOnMetadataUpdateAttributeSrc + """
            public class C
            {
                public int F([RestartRequiredOnMetadataUpdateAttribute] System.Func<int> f)
                    => f();

                public void G()
                {
                    F(L);

                    int L() => 1;
                }
            }
        
            """;

        var src2 = RestartRequiredOnMetadataUpdateAttributeSrc + """
            
            public class C
            {
                public int F([RestartRequiredOnMetadataUpdateAttribute] System.Func<int> f)
                    => f();

                public void G()
                {
                    F(L);
            
                    int L() => 2;
                }
            }
            """;

        var edits = GetTopEdits(src1, src2);

        // UpdateMightNotHaveAnyEffect not reported for local functions.
        edits.VerifySemanticDiagnostics();
    }

    #endregion

    #region Queries

    [Fact]
    public void Queries_UpdateAround_BlockBody()
    {
        var src1 = "class C { void M() { F(1, from goo in bar select baz); } }";
        var src2 = "class C { void M() { F(2, from goo in bar select baz); } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_UpdateAround_ExpressionBody()
    {
        var src1 = "class C { void M() => F(1, from goo in bar select baz); }";
        var src2 = "class C { void M() => F(2, from goo in bar select baz); }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_Update_Signature_Select1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>select a</N:0>;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1.0} <N:0>select a</N:0>;
                }
            }

            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("select clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_Select2()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>select a</N:0>;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>select a.ToString()</N:0>;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("select clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_From1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = <N:0>from a in new[] {1}</N:0> from b in new[] {2} select b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = <N:0>from long a in new[] {1}</N:0> from b in new[] {2} select b;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("from clause")])
                ])
            ], capabilities: EditAndContinueCapabilities.AddMethodToExistingType);
    }

    [Fact]
    public void Queries_Update_Signature_From2()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from System.Int64 a in new[] {1} from b in new[] {2} select b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from long a in new[] {1} from b in new[] {2} select b;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_Update_Signature_From3()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} from b in new[] {2} select b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new List<int>() from b in new List<int>() select b;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_Update_Signature_Let1()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>let b = 1</N:0> select a;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>let b = 1.0</N:0> select a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("let clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_OrderBy1()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} orderby <N:0>a + 1 descending</N:0>, a + 2 ascending select a;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} orderby <N:0>a + 1.0 descending</N:0>, a + 2 ascending select a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("orderby clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_OrderBy2()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} orderby a + 1 descending, <N:0>a + 2 ascending</N:0> select a;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} orderby a + 1 descending, <N:0>a + 2.0 ascending</N:0> select a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("orderby clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_Join1()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>join b in new[] {1} on a equals b</N:0> select b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>join b in new[] {1.0} on a equals b</N:0> select b;
                }
            }

            """;

        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("join clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_Join2()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>join b in new[] {1} on a equals b</N:0> select b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>join byte b in new[] {1} on a equals b</N:0> select b;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("join clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_Join3()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>join b in new[] {1} on a + 1 equals b</N:0> select b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>join b in new[] {1} on a + 1.0 equals b</N:0> select b;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("join clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_Join4()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>join b in new[] {1} on a equals b + 1</N:0> select b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>join b in new[] {1} on a equals b + 1.0</N:0> select b;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("join clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_GroupBy1()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>group a + 1 by a</N:0> into z select z;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>group a + 1.0 by a</N:0> into z select z;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("groupby clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_GroupBy2()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>group a by a</N:0> into z select z;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>group a by a + 1.0</N:0> into z select z;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("groupby clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_Update_Signature_GroupBy_MatchingErrorTypes()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                Unknown G1(int a) => null;
                Unknown G2(int a) => null;

                void F()
                {
                    var result = from a in new[] {1} group G1(a) by a into z select z;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                Unknown G1(int a) => null;
                Unknown G2(int a) => null;
                
                void F()
                {
                    var result = from a in new[] {1} group G2(a) by a into z select z;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_Update_Signature_GroupBy_NonMatchingErrorTypes()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                Unknown1 G1(int a) => null;
                Unknown2 G2(int a) => null;

                void F()
                {
                    var result = from a in new[] {1} <N:0>group G1(a) by a</N:0> into z select z;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                Unknown1 G1(int a) => null;
                Unknown2 G2(int a) => null;
                
                void F()
                {
                    var result = from a in new[] {1} <N:0>group G2(a) by a</N:0> into z select z;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("groupby clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_FromSelect_Update1()
    {
        var src1 = "F(from a in b from x in y select c);";
        var src2 = "F(from a in c from x in z select c + 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [from a in b]@4 -> [from a in c]@4",
            "Update [from x in y]@16 -> [from x in z]@16",
            "Update [select c]@28 -> [select c + 1]@28");
    }

    [Fact]
    public void Queries_FromSelect_Update2()
    {
        var src1 = "F(from a in b from x in y select c);";
        var src2 = "F(from a in b from x in z select c);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [from x in y]@16 -> [from x in z]@16");
    }

    [Fact]
    public void Queries_FromSelect_Update3()
    {
        var src1 = "F(from a in await b from x in y select c);";
        var src2 = "F(from a in await c from x in y select c);";

        var edits = GetMethodEdits(src1, src2, MethodKind.Async);

        edits.VerifyEdits(
            "Update [await b]@34 -> [await c]@34");
    }

    [Fact]
    public void Queries_Select_Reduced1()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} where a > 0 select a;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} where a > 0 select a + 1;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_Select_Reduced2()
    {
        var src1 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} where a > 0 select a + 1;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;
            using System.Collections.Generic;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} where a > 0 select a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_FromSelect_Delete()
    {
        var src1 = "F(from a in b from c in d select a + c);";
        var src2 = "F(from a in b select c + 1);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [select a + c]@28 -> [select c + 1]@16",
            "Delete [from c in d]@16");
    }

    [Fact]
    public void Queries_JoinInto_Update()
    {
        var src1 = "F(from a in b join b in c on a equals b into g1 select g1);";
        var src2 = "F(from a in b join b in c on a equals b into g2 select g2);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [select g1]@50 -> [select g2]@50",
            "Update [into g1]@42 -> [into g2]@42");
    }

    [Fact]
    public void Queries_JoinIn_Update()
    {
        var src1 = "F(from a in b join b in await A(1) on a equals b select g);";
        var src2 = "F(from a in b join b in await A(2) on a equals b select g);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [await A(1)]@26 -> [await A(2)]@26");
    }

    [Fact]
    public void Queries_GroupBy_Update()
    {
        var src1 = "F(from a in b  group a by a.x into g  select g);";
        var src2 = "F(from a in b  group z by z.y into h  select h);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [group a by a.x]@17 -> [group z by z.y]@17",
            "Update [into g  select g]@32 -> [into h  select h]@32",
            "Update [select g]@40 -> [select h]@40");
    }

    [Fact]
    public void Queries_OrderBy_Reorder()
    {
        var src1 = "F(from a in b  orderby a.x, a.b descending, a.c ascending  select a.d);";
        var src2 = "F(from a in b  orderby a.x, a.c ascending, a.b descending  select a.d);";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [a.c ascending]@46 -> @30");
    }

    [Fact]
    public void Queries_GroupBy_Reduced1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>group a by a</N:0>;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>group a + 1.0 by a</N:0>;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("groupby clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_GroupBy_Reduced2()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} group a by a;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} group a + 1 by a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_GroupBy_Reduced3()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>group a + 1.0 by a</N:0>;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} <N:0>group a by a</N:0>;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        var syntaxMap = edits.GetSyntaxMap();

        edits.VerifySemantics(
            [
                SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), syntaxMap, rudeEdits:
                [
                    RuntimeRudeEdit(0, RudeEditKind.ChangingQueryLambdaType, syntaxMap.NodePosition(0), [GetResource("groupby clause")])
                ])
            ]);
    }

    [Fact]
    public void Queries_GroupBy_Reduced4()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} group a + 1 by a;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                void F()
                {
                    var result = from a in new[] {1} group a by a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_OrderBy_Continuation_Update()
    {
        var src1 = "F(from a in b  orderby a.x, a.b descending  select a.d  into z  orderby a.c ascending select z);";
        var src2 = "F(from a in b  orderby a.x, a.c ascending  select a.d  into z  orderby a.b descending select z);";

        var edits = GetMethodEdits(src1, src2);

        var actual = ToMatchingPairs(edits.Match);

        var expected = new MatchingPairs
        {
            { "F(from a in b  orderby a.x, a.b descending  select a.d  into z  orderby a.c ascending select z);", "F(from a in b  orderby a.x, a.c ascending  select a.d  into z  orderby a.b descending select z);" },
            { "from a in b", "from a in b" },
            { "orderby a.x, a.b descending  select a.d  into z  orderby a.c ascending select z", "orderby a.x, a.c ascending  select a.d  into z  orderby a.b descending select z" },
            { "orderby a.x, a.b descending", "orderby a.x, a.c ascending" },
            { "a.x", "a.x" },
            { "a.b descending", "a.c ascending" },
            { "select a.d", "select a.d" },
            { "into z  orderby a.c ascending select z", "into z  orderby a.b descending select z" },
            { "orderby a.c ascending select z", "orderby a.b descending select z" },
            { "orderby a.c ascending", "orderby a.b descending" },
            { "a.c ascending", "a.b descending" },
            { "select z", "select z" }
        };

        expected.AssertEqual(actual);

        edits.VerifyEdits(
            "Update [a.b descending]@30 -> [a.c ascending]@30",
            "Update [a.c ascending]@74 -> [a.b descending]@73");
    }

    [Fact]
    public void Queries_CapturedTransparentIdentifiers_FromClause1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
            		             from b in new[] { 2 }
            		             where Z(() => a) > 0
            		             where Z(() => b) > 0
            		             where Z(() => a) > 0
            		             where Z(() => b) > 0
            		             select a;
                }
            }
            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
            		             from b in new[] { 2 }
            		             where Z(() => a) > 1  // update
            		             where Z(() => b) > 2  // update
            		             where Z(() => a) > 3  // update
            		             where Z(() => b) > 4  // update
            		             select a;
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_CapturedTransparentIdentifiers_LetClause1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
            		             let b = Z(() => a)
            		             select a + b;
                }
            }
            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
            		             let b = Z(() => a + 1)
            		             select a - b;
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_CapturedTransparentIdentifiers_JoinClause1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
                                 join b in new[] { 3 } on Z(() => a + 1) equals Z(() => b - 1) into g
                                 select Z(() => g.First());
                }
            }
            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
                                 join b in new[] { 3 } on Z(() => a + 1) equals Z(() => b - 1) into g
                                 select Z(() => g.Last());
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_CeaseCapturingTransparentIdentifiers1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
            		             from b in new[] { 2 }
            		             where Z(() => a + b) > 0
            		             select a;
                }
            }
            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
            		             from b in new[] { 2 }
            		             where Z(() => a + 1) > 0
            		             select a;
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Queries_CapturingTransparentIdentifiers1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
            		             from b in new[] { 2 }
            		             where Z(() => a + 1) > 0
            		             select a;
                }
            }
            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
            	int Z(Func<int> f)
            	{
            		return 1;
            	}

                void F()
                {
            		var result = from a in new[] { 1 }
            		             from b in new[] { 2 }
            		             where Z(() => a + b) > 0
            		             select a;
                }
            }
            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Queries_AccessingCapturedTransparentIdentifier1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;

                void F()
                {
                    var result = from a in new[] { 1 }
                                 where Z(() => a) > 0
                                 select 1;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;
               
                void F()
                {
                    var result = from a in new[] { 1 } 
                                 where Z(() => a) > 0
                                 select a;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    [Fact]
    public void Queries_AccessingCapturedTransparentIdentifier2()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;

                void F()
                {
                    var result = from a in new[] { 1 }
                                 from b in new[] { 1 }
                                 where Z(() => a) > 0
                                 select b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;
               
                void F()
                {
                    var result = from a in new[] { 1 } 
                                 from b in new[] { 1 }
                                 where Z(() => a) > 0
                                 select a + b;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Queries_AccessingCapturedTransparentIdentifier3()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;

                void F()
                {
                    var result = from a in new[] { 1 }
                                 where Z(() => a) > 0
                                 select Z(() => 1);
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;
               
                void F()
                {
                    var result = from a in new[] { 1 } 
                                 where Z(() => a) > 0
                                 select Z(() => a);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Queries_NotAccessingCapturedTransparentIdentifier1()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;

                void F()
                {
                    var result = from a in new[] { 1 }
                                 from b in new[] { 1 }
                                 where Z(() => a) > 0
                                 select a + b;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;
               
                void F()
                {
                    var result = from a in new[] { 1 } 
                                 from b in new[] { 1 }
                                 where Z(() => a) > 0
                                 select b;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Queries_NotAccessingCapturedTransparentIdentifier2()
    {
        var src1 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;

                void F()
                {
                    var result = from a in new[] { 1 }
                                 where Z(() => a) > 0
                                 select Z(() => 1);
                }
            }

            """;
        var src2 = """

            using System;
            using System.Linq;

            class C
            {
                int Z(Func<int> f) => 1;
               
                void F()
                {
                    var result = from a in new[] { 1 } 
                                 where Z(() => a) > 0
                                 select Z(() => a);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true));
    }

    [Fact]
    public void Queries_Insert_Static_First()
    {
        var src1 = """

            using System;
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                IEnumerable<int> F()
                {
                    return null;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                IEnumerable<int> F()
                {
                    return from x in new[] {1,2,3}
                           where x > 1
                           group x by x + 1 into z
                           select z.Key;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.NewTypeDefinition |
                EditAndContinueCapabilities.AddStaticFieldToExistingType |
                EditAndContinueCapabilities.AddMethodToExistingType);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "where", GetResource("where clause")),
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "group", GetResource("groupby clause")),
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "select", GetResource("select clause"))
            ],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void Queries_Insert_ThisOnly_Second()
    {
        var src1 = """

            using System;
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                int y;

                IEnumerable<int> F()
                {
                    var f = () => y;
                    return null;
                }
            }

            """;
        var src2 = """

            using System;
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                int y;

                IEnumerable<int> F()
                {
                    var f = () => y;
                    return from x in new[] {1,2,3}
                           where x > y
                           group x by x + y into z
                           select z.Key + y;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.NewTypeDefinition |
                EditAndContinueCapabilities.AddStaticFieldToExistingType |
                EditAndContinueCapabilities.AddMethodToExistingType);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "where", GetResource("where clause")),
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "group", GetResource("groupby clause")),
                Diagnostic(RudeEditKind.InsertNotSupportedByRuntime, "select", GetResource("select clause"))
            ],
            capabilities: EditAndContinueCapabilities.Baseline);
    }

    [Fact]
    public void Queries_StackAlloc()
    {
        var src1 = """

            using System;
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                IEnumerable<int> F()
                {
                    return from x in new[] {1,2,3}
                           where G(stackalloc int[1]) > 1
                           group G(stackalloc int[2]) by G(stackalloc int[3]) into z
                           select z.Key + G(stackalloc int[4]);
                }
                
                int G(Span<int> span) => span.Length;
            }
            """;
        var src2 = """

            using System;
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                IEnumerable<int> F()
                {
                    return from x in new[] {1,2,3}
                           where G(stackalloc int[10]) > 1
                           group G(stackalloc int[20]) by G(stackalloc int[30]) into z
                           select z.Key + G(stackalloc int[40]);
                }
                
                int G(Span<int> span) => span.Length;
            }
            """;

        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.StackAllocUpdate, "stackalloc int[10]", GetResource("where clause")),
            Diagnostic(RudeEditKind.StackAllocUpdate, "stackalloc int[40]", GetResource("select clause")),
            Diagnostic(RudeEditKind.StackAllocUpdate, "stackalloc int[20]", GetResource("groupby clause")),
            Diagnostic(RudeEditKind.StackAllocUpdate, "stackalloc int[30]", GetResource("groupby clause")));
    }

    #endregion

    #region Yield

    [Fact]
    public void Yield_Update1()
    {
        var src1 = """

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 1;
                    yield return 2;
                    yield break;
                }
            }

            """;
        var src2 = """

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 3;
                    yield break;
                    yield return 4;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Yield_Delete1()
    {
        var src1 = """

            yield return 1;
            yield return 2;
            yield return 3;

            """;
        var src2 = """

            yield return 1;
            yield return 3;

            """;

        var bodyEdits = GetMethodEdits(src1, src2, kind: MethodKind.Iterator);

        bodyEdits.VerifyEdits(
            "Delete [yield return 2;]@42");
    }

    [Fact]
    public void Yield_Delete2()
    {
        var src1 = """

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 1;
                    yield return 2;
                    yield return 3;
                }
            }

            """;
        var src2 = """

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 1;
                    yield return 3;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Yield_Insert1()
    {
        var src1 = """

            yield return 1;
            yield return 3;

            """;
        var src2 = """

            yield return 1;
            yield return 2;
            yield return 3;
            yield return 4;

            """;

        var bodyEdits = GetMethodEdits(src1, src2, kind: MethodKind.Iterator);

        bodyEdits.VerifyEdits(
            "Insert [yield return 2;]@42",
            "Insert [yield return 4;]@76");
    }

    [Fact]
    public void Yield_Insert2()
    {
        var src1 = """

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 1;
                    yield return 3;
                }
            }

            """;
        var src2 = """

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 1;
                    yield return 2;
                    yield return 3;
                    yield return 4;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Yield_Update_GenericType()
    {
        var src1 = """

            class C<T>
            {
                static IEnumerable<int> F()
                {
                    yield return 1;
                }
            }

            """;
        var src2 = """

            class C<T>
            {
                static IEnumerable<int> F()
                {
                    yield return 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.GenericAddFieldToExistingType |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "static IEnumerable<int> F()", GetResource("method"))
            ],
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Yield_Update_GenericMethod()
    {
        var src1 = """

            class C
            {
                static IEnumerable<int> F<T>()
                {
                    yield return 1;
                }
            }

            """;
        var src2 = """

            class C
            {
                static IEnumerable<int> F<T>()
                {
                    yield return 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.GenericAddFieldToExistingType |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "static IEnumerable<int> F<T>()", GetResource("method"))
            ],
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Yield_Update_GenericLocalFunction()
    {
        var src1 = """

            class C
            {
                void F()
                {
                    IEnumerable<int> L<T>()
                    {
                        yield return 1;
                    }
                }
            }

            """;
        var src2 = """

            class C
            {
                void F()
                {
                    IEnumerable<int> L<T>()
                    {
                        yield return 2;
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.GenericAddFieldToExistingType |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "L", GetResource("local function"))
            ],
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void MissingIteratorStateMachineAttribute()
    {
        var src1 = """

            using System.Collections.Generic;

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 1;
                }
            }

            """;
        var src2 = """

            using System.Collections.Generic;

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            targetFrameworks: [TargetFramework.Mscorlib40AndSystemCore],
            diagnostics:
            [
                Diagnostic(RudeEditKind.UpdatingStateMachineMethodMissingAttribute, "static IEnumerable<int> F()", "System.Runtime.CompilerServices.IteratorStateMachineAttribute")
            ]);
    }

    [Fact]
    public void MissingIteratorStateMachineAttribute2()
    {
        var src1 = """

            using System.Collections.Generic;

            class C
            {
                static IEnumerable<int> F()
                {
                    return null;
                }
            }

            """;
        var src2 = """

            using System.Collections.Generic;

            class C
            {
                static IEnumerable<int> F()
                {
                    yield return 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            targetFrameworks: [TargetFramework.Mscorlib40AndSystemCore],
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddExplicitInterfaceImplementation);
    }

    #endregion

    #region Await

    /// <summary>
    /// Tests spilling detection logic of <see cref="CSharpEditAndContinueAnalyzer.ReportStateMachineSuspensionPointRudeEdits"/>.
    /// </summary>
    [Theory]
    [InlineData("await F(old);")]
    [InlineData("if (await F(1)) { Console.WriteLine(old); }")]
    [InlineData("if (await F(old)) { Console.WriteLine(1); }")]
    [InlineData("if (F(1, await F(1))) { Console.WriteLine(old); }")]
    [InlineData("if (await F(1)) { Console.WriteLine(1); }", "while (await F(1)) { Console.WriteLine(1); }")]
    [InlineData("do { Console.WriteLine(old); } while (await F(old));")]
    [InlineData("for (var x = await F(old); await G(old); await H(old)) { Console.WriteLine(old); }")]
    [InlineData("foreach (var x in await F(old)) { Console.WriteLine(old); }")]
    [InlineData("using (var x = await F(old)) { Console.WriteLine(1); }")]
    [InlineData("lock (await F(old)) { Console.WriteLine(old); }")]
    [InlineData("lock (a = await F(old)) { Console.WriteLine(old); }")]
    [InlineData("var a = await F(old), b = await G(old);")]
    [InlineData("a = await F(old);")]
    [InlineData("switch (await F(2)) { case 1: return b = await F(old); }")]
    [InlineData("return await F(old);")]
    public void AwaitSpilling_OK(string oldStatement, string newStatement = null)
    {
        var src1 = """

            class C
            {
                static async Task<int> F()
                {
                    
            """ + oldStatement + """

                }
            }

            """;
        newStatement ??= oldStatement.Replace("old", "@new");

        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    
            """ + newStatement + """

                }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitSpilling_ExpressionBody()
    {
        var src1 = """

            class C
            {
                static async Task<int> G() => await F(1);
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> G() => await F(2);
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    /// <summary>
    /// Tests spilling detection logic of <see cref="CSharpEditAndContinueAnalyzer.ReportStateMachineSuspensionPointRudeEdits"/>.
    /// </summary>
    [Theory]
    [InlineData("F(old, await F(1));")]
    [InlineData("F(1, await F(old));")]
    [InlineData("F(await F(old));")]
    [InlineData("await F(await F(old));")]
    [InlineData("if (F(old, await F(1))) { Console.WriteLine(1); }", new[] { "F(@new, await F(1))" })]
    [InlineData("var a = F(1, await F(old)), b = F(1, await G(old));", new[] { "var a = F(1, await F(@new)), b = F(1, await G(@new));", "var a = F(1, await F(@new)), b = F(1, await G(@new));" })]
    [InlineData("b = F(1, await F(old));")]
    [InlineData("b += await F(old);")]
    public void AwaitSpilling_Errors(string oldStatement, string[] errorMessages = null)
    {
        var src1 = """

            class C
            {
                static async Task<int> F()
                {
                    
            """ + oldStatement + """

                }
            }

            """;
        var newStatement = oldStatement.Replace("old", "@new");

        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    
            """ + newStatement + """

                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        // consider: these edits can be allowed if we get more sophisticated
        var expectedDiagnostics = from errorMessage in errorMessages ?? [newStatement]
                                  select Diagnostic(RudeEditKind.AwaitStatementUpdate, errorMessage);

        edits.VerifySemanticDiagnostics([.. expectedDiagnostics]);
    }

    [Fact]
    public void AwaitSpilling_Errors_LocalFunction()
    {
        var src1 = """

            class C
            {
                static void F()
                {
                    async Task<int> L()
                    {
                        F(old, await F(1));
                    }
                }
            }

            """;
        var src2 = """

            class C
            {
                static void F()
                {
                    async Task<int> L()
                    {
                        F(old, await F(2));
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.AwaitStatementUpdate, "F(old, await F(2));"));
    }

    [Fact]
    public void Await_Delete1()
    {
        var src1 = """

            await F(1);
            await F(2);
            await F(3);

            """;
        var src2 = """

            await F(1);
            await F(3);

            """;

        var bodyEdits = GetMethodEdits(src1, src2, kind: MethodKind.Async);

        bodyEdits.VerifyEdits(
            "Delete [await F(2);]@37",
            "Delete [await F(2)]@37");
    }

    [Fact]
    public void Await_Delete2()
    {
        var src1 = """

            class C
            {
                static async Task<int> F()
                {
                    await F(1);
                    {
                        await F(2);
                    }
                    await F(3);
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    await F(1);
                    {
                        F(2);
                    }
                    await F(3);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Delete3()
    {
        var src1 = """

            class C
            {
                static async Task<int> F()
                {
                    await F(await F(1));
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    await F(1);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Delete4()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() => await F(await F(1));
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F() => await F(1);
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Delete5()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() => await F(1);
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F() => F(1);
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: false)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitForEach_Delete1()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await foreach (var x in G()) { } 
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    foreach (var x in G()) { } 
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: false)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitForEach_Delete2()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await foreach (var (x, y) in G()) { } 
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    foreach (var (x, y) in G()) { } 
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: false)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitForEach_Delete3()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await foreach (var x in G()) { } 
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: false)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitUsing_Delete1()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await using D x = new D(), y = new D();
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    await using D x = new D();
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitUsing_Delete2()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await using D x = new D(), y = new D();
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    await using D y = new D();
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitUsing_Delete3()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await using D x = new D(), y = new D();
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: false)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitUsing_Delete4()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await using D x = new D(), y = new D();
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    using D x = new D(), y = new D();
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: false)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Insert1()
    {
        var src1 = """

            await F(1);
            await F(3);

            """;
        var src2 = """

            await F(1);
            await F(2);
            await F(3);
            await F(4);

            """;

        var bodyEdits = GetMethodEdits(src1, src2, kind: MethodKind.Async);

        bodyEdits.VerifyEdits(
            "Insert [await F(2);]@37",
            "Insert [await F(4);]@63",
            "Insert [await F(2)]@37",
            "Insert [await F(4)]@63");
    }

    [Fact]
    public void Await_Insert2()
    {
        var src1 = """

            class C
            {
                static async IEnumerable<int> F()
                {
                    await F(1);
                    await F(3);
                }
            }

            """;
        var src2 = """

            class C
            {
                static async IEnumerable<int> F()
                {
                    await F(1);
                    await F(2);
                    await F(3);
                    await F(4);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Insert3()
    {
        var src1 = """

            class C
            {
                static async IEnumerable<int> F()
                {
                    await F(1);
                    await F(3);
                }
            }

            """;
        var src2 = """

            class C
            {
                static async IEnumerable<int> F()
                {
                    await F(await F(1));
                    await F(await F(2));
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.AwaitStatementUpdate, "await F(await F(1));"),
            Diagnostic(RudeEditKind.AwaitStatementUpdate, "await F(await F(2));"));
    }

    [Fact]
    public void Await_Insert4()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() => await F(1);
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F() => await F(await F(1));
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            Diagnostic(RudeEditKind.AwaitStatementUpdate, "await F(await F(1))"));
    }

    [Fact]
    public void Await_Insert5()
    {
        var src1 = """

            class C
            {
                static Task<int> F() => F(1);
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F() => await F(1);
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddExplicitInterfaceImplementation);
    }

    [Fact]
    public void AwaitForEach_Insert_Ok()
    {
        var src1 = """

            class C
            {
                static async Task F() 
                {
                    foreach (var x in G()) { } 
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task F()
                {
                    await foreach (var x in G()) { } 
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitForEach_Insert()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await Task.FromResult(1);

                    foreach (var x in G()) { } 
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    await Task.FromResult(1);

                    await foreach (var x in G()) { } 
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitUsing_Insert1()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await using D x = new D();
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    await using D x = new D(), y = new D();
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void AwaitUsing_Insert2()
    {
        var src1 = """

            class C
            {
                static async Task<int> F() 
                {
                    await G();
                    using D x = new D();
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task<int> F()
                {
                    await G();
                    await using D x = new D();
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Update()
    {
        var src1 = s_asyncIteratorStateMachineAttributeSource + """

            class C
            {
                static async IAsyncEnumerable<int> F() 
                {
                    await foreach (var x in G()) { }
                    await Task.FromResult(1);
                    await Task.FromResult(1);
                    await Task.FromResult(1);
                    yield return 1;
                    yield break;
                    yield break;
                }
            }

            """;
        var src2 = s_asyncIteratorStateMachineAttributeSource + """

            class C
            {
                static async IAsyncEnumerable<int> F()
                {
                    await foreach (var (x,y) in G()) { }
                    await foreach (var x in G()) { }
                    await using D x = new D(), y = new D();
                    await Task.FromResult(1);
                    await Task.FromResult(1);
                    yield return 1;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("C.F"), preserveLocalVariables: true)],
            capabilities: EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Update_GenericType()
    {
        var src1 = """

            class C<T>
            {
                static async Task F()
                {
                    await Task.FromResult(1);
                }
            }

            """;
        var src2 = """

            class C<T>
            {
                static async Task F()
                {
                    await Task.FromResult(2);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.GenericAddFieldToExistingType |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "static async Task F()", GetResource("method"))
            ],
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Update_GenericMethod()
    {
        var src1 = """

            class C
            {
                static async Task F<T>()
                {
                    await Task.FromResult(1);
                }
            }

            """;
        var src2 = """

            class C
            {
                static async Task F<T>()
                {
                    await Task.FromResult(2);
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.GenericAddFieldToExistingType |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "static async Task F<T>()", GetResource("method"))
            ],
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void Await_Update_GenericLocalFunction()
    {
        var src1 = """

            class C
            {
                void F()
                {
                    void M()
                    {
                        async Task L<T>()
                        {
                            await Task.FromResult(1);
                        }
                    }
                }
            }

            """;
        var src2 = """

            class C
            {
                void F()
                {
                    void M()
                    {
                        async Task L<T>()
                        {
                            await Task.FromResult(2);
                        }
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.GenericAddFieldToExistingType |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);

        edits.VerifySemanticDiagnostics(
            [
                Diagnostic(RudeEditKind.UpdatingGenericNotSupportedByRuntime, "L", GetResource("local function"))
            ],
            capabilities:
                EditAndContinueCapabilities.GenericUpdateMethod |
                EditAndContinueCapabilities.AddInstanceFieldToExistingType);
    }

    [Fact]
    public void MissingAsyncStateMachineAttribute()
    {
        var src1 = """

            using System.Threading.Tasks;

            class C
            {
                static async Task<int> F()
                {
                    await new Task();
                    return 1;
                }
            }

            """;
        var src2 = """

            using System.Threading.Tasks;

            class C
            {
                static async Task<int> F()
                {
                    await new Task();
                    return 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            targetFrameworks: [TargetFramework.MinimalAsync],
            diagnostics:
            [
                Diagnostic(RudeEditKind.UpdatingStateMachineMethodMissingAttribute, "static async Task<int> F()", "System.Runtime.CompilerServices.AsyncStateMachineAttribute")
            ]);
    }

    [Fact]
    public void MissingAsyncStateMachineAttribute_MakeMethodAsync()
    {
        var src1 = """

            using System.Threading.Tasks;

            class C
            {
                static Task<int> F()
                {
                    return null;
                }
            }

            """;
        var src2 = """

            using System.Threading.Tasks;

            class C
            {
                static async Task<int> F()
                {
                    await new Task();
                    return 2;
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            targetFrameworks: [TargetFramework.MinimalAsync],
            capabilities: EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.AddExplicitInterfaceImplementation);
    }

    [Fact]
    public void MissingAsyncStateMachineAttribute_LocalFunction()
    {
        var src1 = """

            using System.Threading.Tasks;

            class C
            {
                void F()
                {
                    async IAsyncEnumerable<int> L()
                    {
                        await new Task();
                        yield return 1;
                    }
                }
            }

            """;
        var src2 = """

            using System.Threading.Tasks;

            class C
            {
                void F()
                {
                    async IAsyncEnumerable<int> L()
                    {
                        await new Task();
                        yield return 2;
                    }
                }
            }

            """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemanticDiagnostics(
            targetFrameworks: [TargetFramework.MinimalAsync],
            diagnostics:
            [
                Diagnostic(RudeEditKind.UpdatingStateMachineMethodMissingAttribute, "L", "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute")
            ]);
    }

    [Fact]
    public void SemanticError_AwaitInPropertyAccessor()
    {
        var src1 = """

            using System.Threading.Tasks;

            class C
            {
               public Task<int> P
               {
                   get 
                   { 
                       await Task.Delay(1);
                       return 1;
                   }
               }
            }

            """;
        var src2 = """

            using System.Threading.Tasks;

            class C
            {
               public Task<int> P
               {
                   get 
                   { 
                       await Task.Delay(2);
                       return 1;
                   }
               }
            }

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemanticDiagnostics();
    }

    #endregion

    #region Out Var

    [Fact]
    public void OutVarType_Update()
    {
        var src1 = """

            M(out var y);

            """;
        var src2 = """

            M(out int y);

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [M(out var y);]@4 -> [M(out int y);]@4");
    }

    [Fact]
    public void OutVarNameAndType_Update()
    {
        var src1 = """

            M(out var y);

            """;
        var src2 = """

            M(out int z);

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [M(out var y);]@4 -> [M(out int z);]@4",
            "Update [y]@14 -> [z]@14");
    }

    [Fact]
    public void OutVar_Insert()
    {
        var src1 = """

            M();

            """;
        var src2 = """

            M(out int y);

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [M();]@4 -> [M(out int y);]@4",
            "Insert [y]@14");
    }

    [Fact]
    public void OutVar_Delete()
    {
        var src1 = """

            M(out int y);

            """;

        var src2 = """

            M();

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [M(out int y);]@4 -> [M();]@4",
            "Delete [y]@14");
    }

    #endregion

    #region Pattern

    [Fact]
    public void ConstantPattern_Update()
    {
        var src1 = """

            if ((o is null) && (y == 7)) return 3;
            if (a is 7) return 5;

            """;

        var src2 = """

            if ((o1 is null) && (y == 7)) return 3;
            if (a is 77) return 5;

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [if ((o is null) && (y == 7)) return 3;]@4 -> [if ((o1 is null) && (y == 7)) return 3;]@4",
            "Update [if (a is 7) return 5;]@44 -> [if (a is 77) return 5;]@45");
    }

    [Fact]
    public void DeclarationPattern_Update()
    {
        var src1 = """

            if (!(o is int i) && (y == 7)) return;
            if (!(a is string s)) return;
            if (!(b is string t)) return;
            if (!(c is int j)) return;

            """;

        var src2 = """

            if (!(o1 is int i) && (y == 7)) return;
            if (!(a is int s)) return;
            if (!(b is string t1)) return;
            if (!(c is int)) return;

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [if (!(o is int i) && (y == 7)) return;]@4 -> [if (!(o1 is int i) && (y == 7)) return;]@4",
            "Update [if (!(a is string s)) return;]@44 -> [if (!(a is int s)) return;]@45",
            "Update [if (!(c is int j)) return;]@106 -> [if (!(c is int)) return;]@105",
            "Update [t]@93 -> [t1]@91",
            "Delete [j]@121");
    }

    [Fact]
    public void DeclarationPattern_Reorder()
    {
        var src1 = @"if ((a is int i) && (b is int j)) { A(); }";
        var src2 = @"if ((b is int j) && (a is int i)) { A(); }";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [if ((a is int i) && (b is int j)) { A(); }]@2 -> [if ((b is int j) && (a is int i)) { A(); }]@2",
            "Reorder [j]@32 -> @16");
    }

    [Fact]
    public void VarPattern_Update()
    {
        var src1 = """

            if (o is (var x, var y)) return;
            if (o4 is (string a, var (b, c))) return;
            if (o2 is var (e, f, g)) return;
            if (o3 is var (k, l, m)) return;

            """;

        var src2 = """

            if (o is (int x, int y1)) return;
            if (o1 is (var a, (var b, string c1))) return;
            if (o7 is var (g, e, f)) return;
            if (o3 is (string k, int l2, int m)) return;

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [if (o is (var x, var y)) return;]@4 -> [if (o is (int x, int y1)) return;]@4",
            "Update [if (o4 is (string a, var (b, c))) return;]@38 -> [if (o1 is (var a, (var b, string c1))) return;]@39",
            "Update [if (o2 is var (e, f, g)) return;]@81 -> [if (o7 is var (g, e, f)) return;]@87",
            "Reorder [g]@102 -> @102",
            "Update [if (o3 is var (k, l, m)) return;]@115 -> [if (o3 is (string k, int l2, int m)) return;]@121",
            "Update [y]@25 -> [y1]@25",
            "Update [c]@67 -> [c1]@72",
            "Update [l]@133 -> [l2]@146");
    }

    [Fact]
    public void PositionalPattern_Update1()
    {
        var src1 = @"var r = (x, y, z) switch { (0, var b, int c) when c > 1 => 2, _ => 4 };";
        var src2 = @"var r = ((x, y, z)) switch { (_, int b1, double c1) when c1 > 2 => c1, _ => 4 };";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(x, y, z) switch { (0, var b, int c) when c > 1 => 2, _ => 4 }]@10 -> [((x, y, z)) switch { (_, int b1, double c1) when c1 > 2 => c1, _ => 4 }]@10",
            "Update [(0, var b, int c) when c > 1 => 2]@29 -> [(_, int b1, double c1) when c1 > 2 => c1]@31",
            "Reorder [c]@44 -> @39",
            "Update [c]@44 -> [b1]@39",
            "Update [b]@37 -> [c1]@50",
            "Update [when c > 1]@47 -> [when c1 > 2]@54");
    }

    [Fact]
    public void PositionalPattern_Update2()
    {
        var src1 = @"var r = (x, y, z) switch { (var a, 3, 4) => a, (1, 1, Point { X: 0 } p) => 3, _ => 4 };";

        var src2 = @"var r = ((x, y, z)) switch { (var a1, 3, 4) => a1 * 2, (1, 1, Point { Y: 0 } p1) => 3, _ => 4 };";

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(x, y, z) switch { (var a, 3, 4) => a, (1, 1, Point { X: 0 } p) => 3, _ => 4 }]@10 -> [((x, y, z)) switch { (var a1, 3, 4) => a1 * 2, (1, 1, Point { Y: 0 } p1) => 3, _ => 4 }]@10",
            "Update [(var a, 3, 4) => a]@29 -> [(var a1, 3, 4) => a1 * 2]@31",
            "Update [(1, 1, Point { X: 0 } p) => 3]@49 -> [(1, 1, Point { Y: 0 } p1) => 3]@57",
            "Update [a]@34 -> [a1]@36",
            "Update [p]@71 -> [p1]@79");
    }

    [Fact]
    public void PositionalPattern_Reorder()
    {
        var src1 = """
            var r = (x, y, z) switch {
            (1, 2, 3) => 0,
            (var a, 3, 4) => a,
            (0, var b, int c) when c > 1 => 2,
            (1, 1, Point { X: 0 } p) => 3,
            _ => 4
            };

            """;

        var src2 = """
            var r = ((x, y, z)) switch {
            (1, 1, Point { X: 0 } p) => 3,
            (0, var b, int c) when c > 1 => 2,
            (var a, 3, 4) => a,
            (1, 2, 3) => 0,
            _ => 4
            };

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            """
            Update [(x, y, z) switch {
            (1, 2, 3) => 0,
            (var a, 3, 4) => a,
            (0, var b, int c) when c > 1 => 2,
            (1, 1, Point { X: 0 } p) => 3,
            _ => 4
            }]@10 -> [((x, y, z)) switch {
            (1, 1, Point { X: 0 } p) => 3,
            (0, var b, int c) when c > 1 => 2,
            (var a, 3, 4) => a,
            (1, 2, 3) => 0,
            _ => 4
            }]@10
            """,
            "Reorder [(var a, 3, 4) => a]@47 -> @100",
            "Reorder [(0, var b, int c) when c > 1 => 2]@68 -> @64",
            "Reorder [(1, 1, Point { X: 0 } p) => 3]@104 -> @32");
    }

    [Fact]
    public void PropertyPattern_Update()
    {
        var src1 = """

            if (address is { State: "WA" }) return 1;
            if (obj is { Color: Color.Purple }) return 2;
            if (o is string { Length: 5 } s) return 3;

            """;

        var src2 = """

            if (address is { ZipCode: 98052 }) return 4;
            if (obj is { Size: Size.M }) return 2;
            if (o is string { Length: 7 } s7) return 5;

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [if (address is { State: \"WA\" }) return 1;]@4 -> [if (address is { ZipCode: 98052 }) return 4;]@4",
            "Update [if (obj is { Color: Color.Purple }) return 2;]@47 -> [if (obj is { Size: Size.M }) return 2;]@50",
            "Update [if (o is string { Length: 5 } s) return 3;]@94 -> [if (o is string { Length: 7 } s7) return 5;]@90",
            "Update [return 1;]@36 -> [return 4;]@39",
            "Update [s]@124 -> [s7]@120",
            "Update [return 3;]@127 -> [return 5;]@124");
    }

    [Fact]
    public void RecursivePatterns_Reorder()
    {
        var src1 = """
            var r = obj switch
            {
                string s when s.Length > 0 => (s, obj1) switch
                {
                    ("a", int i) => i,
                    _ => 0
                },
                int i => i * i,
                _ => -1
            };

            """;

        var src2 = """
            var r = obj switch
            {
                int i => i * i,
                string s when s.Length > 0 => (s, obj1) switch
                {
                    ("a", int i) => i,
                    _ => 0
                },
                _ => -1
            };

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [int i => i * i]@140 -> @29",
            "Move [i]@102 -> @33",
            "Move [i]@144 -> @123");
    }

    [Fact]
    public void CasePattern_UpdateInsert()
    {
        var src1 = """

            switch(shape)
            {
                case Circle c: return 1;
                default: return 4;
            }

            """;

        var src2 = """

            switch(shape)
            {
                case Circle c1: return 1;
                case Point p: return 0;
                default: return 4;
            }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [case Circle c: return 1;]@26 -> [case Circle c1: return 1;]@26",
            "Insert [case Point p: return 0;]@57",
            "Insert [case Point p:]@57",
            "Insert [return 0;]@71",
            "Update [c]@38 -> [c1]@38",
            "Insert [p]@68");
    }

    [Fact]
    public void CasePattern_UpdateDelete()
    {
        var src1 = """

            switch(shape)
            {
                case Point p: return 0;
                case Circle c: A(c); break;
                default: return 4;
            }

            """;

        var src2 = """

            switch(shape)
            {
                case Circle c1: A(c1); break;
                default: return 4;
            }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [case Circle c: A(c); break;]@55 -> [case Circle c1: A(c1); break;]@26",
            "Update [A(c);]@70 -> [A(c1);]@42",
            "Update [c]@67 -> [c1]@38",
            "Delete [case Point p: return 0;]@26",
            "Delete [case Point p:]@26",
            "Delete [p]@37",
            "Delete [return 0;]@40");
    }

    [Fact]
    public void WhenCondition_Update()
    {
        var src1 = """

            switch(shape)
            {
                case Circle c when (c < 10): return 1;
                case Circle c when (c > 100): return 2;
            }

            """;

        var src2 = """

            switch(shape)
            {
                case Circle c when (c < 5): return 1;
                case Circle c2 when (c2 > 100): return 2;
            }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [case Circle c when (c < 10): return 1;]@26 -> [case Circle c when (c < 5): return 1;]@26",
            "Update [case Circle c when (c > 100): return 2;]@70 -> [case Circle c2 when (c2 > 100): return 2;]@69",
            "Update [when (c < 10)]@40 -> [when (c < 5)]@40",
            "Update [c]@82 -> [c2]@81",
            "Update [when (c > 100)]@84 -> [when (c2 > 100)]@84");
    }

    [Fact]
    public void CasePatternWithWhenCondition_UpdateReorder()
    {
        var src1 = """

            switch(shape)
            {
                case Rectangle r: return 0;
                case Circle c when (c.Radius < 10): return 1;
                case Circle c when (c.Radius > 100): return 2;
            }

            """;

        var src2 = """

            switch(shape)
            {
                case Circle c when (c.Radius > 99): return 2;
                case Circle c when (c.Radius < 10): return 1;
                case Rectangle r: return 0;
            }

            """;
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [case Circle c when (c.Radius < 10): return 1;]@59 -> @77",
            "Reorder [case Circle c when (c.Radius > 100): return 2;]@110 -> @26",
            "Update [case Circle c when (c.Radius > 100): return 2;]@110 -> [case Circle c when (c.Radius > 99): return 2;]@26",
            "Move [c]@71 -> @38",
            "Update [when (c.Radius > 100)]@124 -> [when (c.Radius > 99)]@40",
            "Move [c]@122 -> @89");
    }

    #endregion

    #region Ref

    [Fact]
    public void Ref_Update()
    {
        var src1 = """

            ref int a = ref G(new int[] { 1, 2 });
            ref int G(int[] p) { return ref p[1];  }

            """;

        var src2 = """

            ref int32 a = ref G1(new int[] { 1, 2 });
            ref int G1(int[] p) { return ref p[2]; }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [ref int G(int[] p) { return ref p[1];  }]@44 -> [ref int G1(int[] p) { return ref p[2]; }]@47",
            "Update [ref int a = ref G(new int[] { 1, 2 })]@4 -> [ref int32 a = ref G1(new int[] { 1, 2 })]@4",
            "Update [a = ref G(new int[] { 1, 2 })]@12 -> [a = ref G1(new int[] { 1, 2 })]@14");
    }

    [Fact]
    public void Ref_Insert()
    {
        var src1 = """

            int a = G(new int[] { 1, 2 });
            int G(int[] p) { return p[1];  }

            """;

        var src2 = """

            ref int32 a = ref G1(new int[] { 1, 2 });
            ref int G1(int[] p) { return ref p[2]; }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [int G(int[] p) { return p[1];  }]@36 -> [ref int G1(int[] p) { return ref p[2]; }]@47",
            "Update [int a = G(new int[] { 1, 2 })]@4 -> [ref int32 a = ref G1(new int[] { 1, 2 })]@4",
            "Update [a = G(new int[] { 1, 2 })]@8 -> [a = ref G1(new int[] { 1, 2 })]@14");
    }

    [Fact]
    public void Ref_Delete()
    {
        var src1 = """

            ref int a = ref G(new int[] { 1, 2 });
            ref int G(int[] p) { return ref p[1];  }

            """;

        var src2 = """

            int32 a = G1(new int[] { 1, 2 });
            int G1(int[] p) { return p[2]; }

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Update [ref int G(int[] p) { return ref p[1];  }]@44 -> [int G1(int[] p) { return p[2]; }]@39",
            "Update [ref int a = ref G(new int[] { 1, 2 })]@4 -> [int32 a = G1(new int[] { 1, 2 })]@4",
            "Update [a = ref G(new int[] { 1, 2 })]@12 -> [a = G1(new int[] { 1, 2 })]@10");
    }

    #endregion

    #region Tuples

    [Fact]
    public void TupleType_LocalVariables()
    {
        var src1 = """

            (int a, string c) x = (a, string2);
            (int a, int b) y = (3, 4);
            (int a, int b, int c) z = (5, 6, 7);

            """;

        var src2 = """

            (int a, int b)  x = (a, string2);
            (int a, int b, string c) z1 = (5, 6, 7);
            (int a, int b) y2 = (3, 4);

            """;

        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(
            "Reorder [(int a, int b, int c) z = (5, 6, 7);]@69 -> @39",
            "Update [(int a, string c) x = (a, string2)]@4 -> [(int a, int b)  x = (a, string2)]@4",
            "Update [(int a, int b, int c) z = (5, 6, 7)]@69 -> [(int a, int b, string c) z1 = (5, 6, 7)]@39",
            "Update [z = (5, 6, 7)]@91 -> [z1 = (5, 6, 7)]@64",
            "Update [y = (3, 4)]@56 -> [y2 = (3, 4)]@96");
    }

    [Fact]
    public void TupleElementName()
    {
        var src1 = @"class C { (int a, int b) F(); }";
        var src2 = @"class C { (int x, int b) F(); }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(int a, int b) F();]@10 -> [(int x, int b) F();]@10");
    }

    [Fact]
    public void TupleInField()
    {
        var src1 = @"class C { private (int, int) _x = (1, 2); }";
        var src2 = @"class C { private (int, string) _y = (1, 2); }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [(int, int) _x = (1, 2)]@18 -> [(int, string) _y = (1, 2)]@18",
            "Update [_x = (1, 2)]@29 -> [_y = (1, 2)]@32");
    }

    [Fact]
    public void TupleInProperty()
    {
        var src1 = @"class C { public (int, int) Property1 { get { return (1, 2); } } }";
        var src2 = @"class C { public (int, string) Property2 { get { return (1, string.Empty); } } }";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [public (int, int) Property1 { get { return (1, 2); } }]@10 -> [public (int, string) Property2 { get { return (1, string.Empty); } }]@10",
            "Update [get { return (1, 2); }]@40 -> [get { return (1, string.Empty); }]@43");
    }

    [Fact]
    public void TupleInDelegate()
    {
        var src1 = @"public delegate void EventHandler1((int, int) x);";
        var src2 = @"public delegate void EventHandler2((int, int) y);";

        var edits = GetTopEdits(src1, src2);

        edits.VerifyEdits(
            "Update [public delegate void EventHandler1((int, int) x);]@0 -> [public delegate void EventHandler2((int, int) y);]@0",
            "Update [(int, int) x]@35 -> [(int, int) y]@35");
    }

    #endregion

    #region With Expressions

    [Fact]
    public void WithExpression_PropertyAdd()
    {
        var src1 = @"var x = y with { X = 1 };";
        var src2 = @"var x = y with { X = 1, Y = 2 };";
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(@"Update [x = y with { X = 1 }]@6 -> [x = y with { X = 1, Y = 2 }]@6");
    }

    [Fact]
    public void WithExpression_PropertyDelete()
    {
        var src1 = @"var x = y with { X = 1, Y = 2 };";
        var src2 = @"var x = y with { X = 1 };";
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(@"Update [x = y with { X = 1, Y = 2 }]@6 -> [x = y with { X = 1 }]@6");
    }

    [Fact]
    public void WithExpression_PropertyChange()
    {
        var src1 = @"var x = y with { X = 1 };";
        var src2 = @"var x = y with { Y = 1 };";
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(@"Update [x = y with { X = 1 }]@6 -> [x = y with { Y = 1 }]@6");
    }

    [Fact]
    public void WithExpression_PropertyValueChange()
    {
        var src1 = @"var x = y with { X = 1, Y = 1 };";
        var src2 = @"var x = y with { X = 1, Y = 2 };";
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(@"Update [x = y with { X = 1, Y = 1 }]@6 -> [x = y with { X = 1, Y = 2 }]@6");
    }

    [Fact]
    public void WithExpression_PropertyValueReorder()
    {
        var src1 = @"var x = y with { X = 1, Y = 1 };";
        var src2 = @"var x = y with { Y = 1, X = 1 };";
        var edits = GetMethodEdits(src1, src2);

        edits.VerifyEdits(@"Update [x = y with { X = 1, Y = 1 }]@6 -> [x = y with { Y = 1, X = 1 }]@6");
    }

    #endregion

    #region Top Level Statements

    [Fact]
    public void TopLevelStatement_Lambda_Update()
    {
        var src1 = """

            using System;

            var x = new Func<int>(() => 1);

            """;
        var src2 = """

            using System;

            var x = new Func<int>(() => 2);

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("Program.<Main>$"), preserveLocalVariables: true)]);
    }

    [Fact]
    public void TopLevelStatement_Lambda_Insert()
    {
        var src1 = """

            using System;

            Console.WriteLine(1);

            """;
        var src2 = """

            using System;

            Console.WriteLine(1);
            var x = new Func<int>(() => 2);

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("Program.<Main>$"), preserveLocalVariables: true)],
            [Diagnostic(RudeEditKind.UpdateMightNotHaveAnyEffect, "var", GetResource("top-level code"))],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.AddStaticFieldToExistingType | EditAndContinueCapabilities.NewTypeDefinition);
    }

    [Fact]
    public void TopLevelStatement_Capture_Args()
    {
        var src1 = """

            using System;

            var x = new Func<string[]>(() => null);

            """;
        var src2 = """

            using System;

            var x = new Func<string[]>(() => args);

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("Program.<Main>$"), preserveLocalVariables: true)]);
    }

    [Fact]
    public void TopLevelStatement_CeaseCapture_Args()
    {
        var src1 = """

            using System;

            var x = new Func<string[]>(() => args);

            """;
        var src2 = """

            using System;

            var x = new Func<string[]>(() => null);

            """;
        var edits = GetTopEdits(src1, src2);
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("Program.<Main>$"), preserveLocalVariables: true));
    }

    [Fact]
    public void TopLevelStatement_CeaseCapture_Args_Closure()
    {
        var src1 = """

            using System;

            var f1 = new Func<int, int>(a1 => 
            {
                var f2 = new Func<int, int>(a2 => args.Length + a2);
                return a1;
            });

            """;
        var src2 = """

            using System;

            var f1 = new Func<int, int>(a1 => 
            {
                var f2 = new Func<int, int>(a2 => a2);
                return a1 + args.Length;
            });

            """;
        var edits = GetTopEdits(src1, src2);

        // y is no longer captured in f2
        edits.VerifySemantics(
            SemanticEdit(SemanticEditKind.Update, c => c.GetMember("Program.<Main>$"), preserveLocalVariables: true));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/21499")]
    public void TopLevelStatement_InsertMultiScopeCapture()
    {
        var src1 = """
        using System;

        foreach (int x0 in new[] { 1 })  // Group #0
        {                                // Group #1
            int x1 = 0;

            int f0(int a) => x0;
            int f1(int a) => x1;
        }
        """;

        var src2 = """
        using System;

        foreach (int x0 in new[] { 1 })  // Group #0
        {                                // Group #1
            int x1 = 0;

            int f0(int a) => x0;
            int f1(int a) => x1;

            int f2(int a) => x0 + x1;   // runtime rude edit: connecting previously disconnected closures
        }
        """;
        var edits = GetTopEdits(src1, src2);

        edits.VerifySemantics(
            [SemanticEdit(SemanticEditKind.Update, c => c.GetMember("Program.<Main>$"), preserveLocalVariables: true)],
            [Diagnostic(RudeEditKind.UpdateMightNotHaveAnyEffect, "int", GetResource("top-level code"))],
            capabilities: EditAndContinueCapabilities.AddMethodToExistingType | EditAndContinueCapabilities.NewTypeDefinition | EditAndContinueCapabilities.UpdateParameters);
    }

    #endregion
}

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.ReplaceConditionalWithStatements;

using VerifyCS = CSharpCodeRefactoringVerifier<CSharpReplaceConditionalWithStatementsCodeRefactoringProvider>;

[UseExportProvider]
public sealed class ReplaceConditionalWithStatementsTests
{
    [Fact]
    public Task TestAssignment_ObjectType()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                void M(bool b)
                {
                    object a;
                    a = $$b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                void M(bool b)
                {
                    object a;
                    if (b)
                    {
                        a = (long)0;
                    }
                    else
                    {
                        a = 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestAssignment_ObjectType_OnAssigment()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                void M(bool b)
                {
                    object a;
                    $$a = b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                void M(bool b)
                {
                    object a;
                    if (b)
                    {
                        a = (long)0;
                    }
                    else
                    {
                        a = 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestAssignment_SameType()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                void M(bool b)
                {
                    long a;
                    a = $$b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                void M(bool b)
                {
                    long a;
                    if (b)
                    {
                        a = 0;
                    }
                    else
                    {
                        a = 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestAssignment_RefConditional()
        => VerifyCS.VerifyRefactoringAsync(
            """
            #nullable enable

            public class C
            {
                private C? y, z;
                void M(bool b, ref C? x)
                {
                    x = ref ($$b ? ref y : ref z);
                }
            }
            """,
            """
            #nullable enable

            public class C
            {
                private C? y, z;
                void M(bool b, ref C? x)
                {
                    if (b)
                    {
                        x = ref (y);
                    }
                    else
                    {
                        x = ref (z);
                    }
                }
            }
            """);

    [Fact]
    public Task TestAssignment_Discard()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                void M(bool b)
                {
                    _ = $$b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                void M(bool b)
                {
                    if (b)
                    {
                        _ = (long)0;
                    }
                    else
                    {
                        _ = 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestAssignment_GlobalStatement()
        => new VerifyCS.Test
        {
            LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
            TestState = { OutputKind = OutputKind.ConsoleApplication },
            TestCode =
            """
            bool b = true;
            long a;
            a = $$b ? 0 : 1L;
            """,
            FixedCode =
            """
            bool b = true;
            long a;
            if (b)
            {
                a = 0;
            }
            else
            {
                a = 1L;
            }
            """
        }.RunAsync();

    [Fact]
    public Task TestAssignment_GlobalStatement_OnAssignment()
        => new VerifyCS.Test
        {
            LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
            TestState = { OutputKind = OutputKind.ConsoleApplication },
            TestCode =
            """
            bool b = true;
            long a;
            $$a = b ? 0 : 1L;
            """,
            FixedCode =
            """
            bool b = true;
            long a;
            if (b)
            {
                a = 0;
            }
            else
            {
                a = 1L;
            }
            """
        }.RunAsync();

    [Fact]
    public async Task TestRefLocalDeclaration1()
    {
        var source =
            """
            class C
            {
                void M(bool b)
                {
                    var y = new C();
                    var z = new C();
                    ref var x = ref ($$b ? ref y : ref z);
                }
            }
            """;
        await VerifyCS.VerifyRefactoringAsync(source, source);
    }

    [Fact]
    public async Task TestUsingLocalDeclaration1()
    {
        var source =
            """
            using System;
            class C
            {
                void M(bool b, IDisposable d1, IDisposable d2)
                {
                    using var x = $$b ? d1 : d2;
                }
            }
            """;
        await VerifyCS.VerifyRefactoringAsync(source, source);
    }

    [Fact]
    public Task TestCompoundAssignment()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                void M(bool b)
                {
                    int a = 0;
                    a += $$b ? 1 : 2;
                }
            }
            """,
            """
            class C
            {
                void M(bool b)
                {
                    int a = 0;
                    if (b)
                    {
                        a += 1;
                    }
                    else
                    {
                        a += 2;
                    }
                }
            }
            """);

    [Fact]
    public Task TestWithExpression()
        => new VerifyCS.Test
        {
            LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            TestCode =
            """
            record X(int A);
            class C
            {
                void M(bool b, X x)
                {
                    x = x with { A = $$b ? 1 : 2 };
                }
            }
            namespace System.Runtime.CompilerServices
            {
                public sealed class IsExternalInit
                {
                }
            }
            """,
            FixedCode =
            """
            record X(int A);
            class C
            {
                void M(bool b, X x)
                {
                    if (b)
                    {
                        x = x with { A = 1 };
                    }
                    else
                    {
                        x = x with { A = 2 };
                    }
                }
            }
            namespace System.Runtime.CompilerServices
            {
                public sealed class IsExternalInit
                {
                }
            }
            """
        }.RunAsync();

    [Fact]
    public Task TestLocalDeclarationStatement1()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                void M(bool b)
                {
                    object a = $$b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                void M(bool b)
                {
                    object a;
                    if (b)
                    {
                        a = (long)0;
                    }
                    else
                    {
                        a = 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestLocalDeclarationStatement1_OnDeclaration()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                void M(bool b)
                {
                    $$object a = b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                void M(bool b)
                {
                    object a;
                    if (b)
                    {
                        a = (long)0;
                    }
                    else
                    {
                        a = 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestLocalDeclarationStatement_WithVar()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                void M(bool b)
                {
                    var a = $$b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                void M(bool b)
                {
                    long a;
                    if (b)
                    {
                        a = 0;
                    }
                    else
                    {
                        a = 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestLocalDeclarationStatement_ThrowExpression()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    int v = N(N($$b ? 42 : throw new Exception()));
                }

                int N(int v) => v;
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    int v;
                    if (b)
                    {
                        v = N(N(42));
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            
                int N(int v) => v;
            }
            """);

    [Fact]
    public Task TestLocalDeclarationStatement_TopLevel1()
        => new VerifyCS.Test
        {
            LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            TestState = { OutputKind = OutputKind.ConsoleApplication },
            TestCode =
            """
            bool b = true;
            object a = $$b ? 0 : 1L;
            """,
            FixedCode =
            """
            bool b = true;
            object a;

            if (b)
            {
                a = (long)0;
            }
            else
            {
                a = 1L;
            }
            """
        }.RunAsync();

    [Fact]
    public Task TestLocalDeclarationStatement_TopLevel_OnDeclaration1()
        => new VerifyCS.Test
        {
            LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            TestState = { OutputKind = OutputKind.ConsoleApplication },
            TestCode =
            """
            bool b = true;
            $$object a = b ? 0 : 1L;
            """,
            FixedCode =
            """
            bool b = true;
            object a;

            if (b)
            {
                a = (long)0;
            }
            else
            {
                a = 1L;
            }
            """
        }.RunAsync();

    [Fact]
    public Task TestLocalDeclarationStatement_TopLevel_WithVar1()
        => new VerifyCS.Test
        {
            LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            TestState = { OutputKind = OutputKind.ConsoleApplication },
            TestCode =
            """
            bool b = true;
            var a = $$b ? 0 : 1L;
            """,
            FixedCode =
            """
            bool b = true;
            long a;

            if (b)
            {
                a = 0;
            }
            else
            {
                a = 1L;
            }
            """
        }.RunAsync();

    [Fact]
    public Task TestReturnStatement_ObjectReturn()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                object M(bool b)
                {
                    return $$b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                object M(bool b)
                {
                    if (b)
                    {
                        return (long)0;
                    }
                    else
                    {
                        return 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestReturnStatement_ObjectReturn_OnReturn()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                object M(bool b)
                {
                    $$return b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                object M(bool b)
                {
                    if (b)
                    {
                        return (long)0;
                    }
                    else
                    {
                        return 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestReturnStatement_AcualTypeReturn()
        => VerifyCS.VerifyRefactoringAsync(
            """
            class C
            {
                long M(bool b)
                {
                    return $$b ? 0 : 1L;
                }
            }
            """,
            """
            class C
            {
                long M(bool b)
                {
                    if (b)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestExpressionStatement_SimpleInvocationArgument()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    Console.WriteLine($$b ? 0 : 1L);
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    if (b)
                    {
                        Console.WriteLine((long)0);
                    }
                    else
                    {
                        Console.WriteLine(1L);
                    }
                }
            }
            """);

    [Fact]
    public Task TestExpressionStatement_NestedInvocationArgument_Outer()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b, bool c)
                {
                    Console.WriteLine($$b ? c ? 0 : 1 : c ? 2 : 3);
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b, bool c)
                {
                    if (b)
                    {
                        Console.WriteLine(c ? 0 : 1);
                    }
                    else
                    {
                        Console.WriteLine(c ? 2 : 3);
                    }
                }
            }
            """);

    [Fact]
    public Task TestExpressionStatement_NestedInvocationArgument_Inner1()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b, bool c)
                {
                    Console.WriteLine(b ? $$c ? 0 : 1 : c ? 2 : 3);
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b, bool c)
                {
                    if (c)
                    {
                        Console.WriteLine(b ? 0 : c ? 2 : 3);
                    }
                    else
                    {
                        Console.WriteLine(b ? 1 : c ? 2 : 3);
                    }
                }
            }
            """);

    [Fact]
    public Task TestExpressionStatement_NestedInvocationArgument_Inner22()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b, bool c)
                {
                    Console.WriteLine(b ? c ? 0 : 1 : $$c ? 2 : 3);
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b, bool c)
                {
                    if (c)
                    {
                        Console.WriteLine(b ? c ? 0 : 1 : 2);
                    }
                    else
                    {
                        Console.WriteLine(b ? c ? 0 : 1 : 3);
                    }
                }
            }
            """);

    [Fact]
    public Task TestExpressionStatement_InvocationWithInference1()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    F($$b ? (int)42 : (int?)null);
                }

                void F<T>(T value) => Console.WriteLine(typeof(T));
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    if (b)
                    {
                        F((int?)(int)42);
                    }
                    else
                    {
                        F((int?)null);
                    }
                }
            
                void F<T>(T value) => Console.WriteLine(typeof(T));
            }
            """);

    [Fact(Skip = "Causes assert in compiler layer")]
    public Task TestExpressionStatement_InvocationWithSimpleObjectCreation()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    F($$b ? new X() : new());
                }

                void F(object value) => Console.WriteLine(value.GetType());
            }
            class X { }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    if (b)
                    {
                        F(new X());
                    }
                    else
                    {
                        F((X)new());
                    }
                }
            
                void F(object value) => Console.WriteLine(value.GetType());
            }
            """);

    [Fact]
    public Task TestExpressionStatement_SimpleInvocationArgument_OnStatement()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    $$Console.WriteLine(b ? 0 : 1L);
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    if (b)
                    {
                        Console.WriteLine((long)0);
                    }
                    else
                    {
                        Console.WriteLine(1L);
                    }
                }
            }
            """);

    [Fact]
    public Task TestExpressionStatement_SecondInvocationArgument()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    Console.WriteLine(b ? "" : "", $$b ? 0 : 1L);
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    if (b)
                    {
                        Console.WriteLine(b ? "" : "", (long)0);
                    }
                    else
                    {
                        Console.WriteLine(b ? "" : "", 1L);
                    }
                }
            }
            """);

    [Fact]
    public Task TestExpressionStatement_NestedInvocationArgument()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                bool M(bool b)
                {
                    M(M(M($$b ? true : false)));
                    return default;
                }
            }
            """,
            """
            using System;
            class C
            {
                bool M(bool b)
                {
                    if (b)
                    {
                        M(M(M(true)));
                    }
                    else
                    {
                        M(M(M(false)));
                    }
                    return default;
                }
            }
            """);

    [Fact]
    public Task TestAwaitExpression1()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            using System.Threading.Tasks;
            class C
            {
                async void M(bool b, Task x, Task y)
                {
                    await ($$b ? x : y);
                }
            }
            """,
            """
            using System;
            using System.Threading.Tasks;
            class C
            {
                async void M(bool b, Task x, Task y)
                {
                    if (b)
                    {
                        await (x);
                    }
                    else
                    {
                        await (y);
                    }
                }
            }
            """);

    [Fact]
    public Task TestAwaitExpression_OnAwait()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            using System.Threading.Tasks;
            class C
            {
                async void M(bool b, Task x, Task y)
                {
                    $$await (b ? x : y);
                }
            }
            """,
            """
            using System;
            using System.Threading.Tasks;
            class C
            {
                async void M(bool b, Task x, Task y)
                {
                    if (b)
                    {
                        await (x);
                    }
                    else
                    {
                        await (y);
                    }
                }
            }
            """);

    [Fact]
    public Task TestThrowStatement1()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    throw $$b ? new Exception("x") : new Exception("y");
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    if (b)
                    {
                        throw new Exception("x");
                    }
                    else
                    {
                        throw new Exception("y");
                    }
                }
            }
            """);

    [Fact]
    public Task TestThrowStatement_OnThrow1()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    $$throw b ? new Exception("x") : new Exception("y");
                }
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    if (b)
                    {
                        throw new Exception("x");
                    }
                    else
                    {
                        throw new Exception("y");
                    }
                }
            }
            """);

    [Fact]
    public Task TestDeepThrowOnOneSide()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                int M(bool b)
                {
                    return N(N($$b ? 42 : throw new Exception()));
                }

                int N(int v) => v;
            }
            """,
            """
            using System;
            class C
            {
                int M(bool b)
                {
                    if (b)
                    {
                        return N(N(42));
                    }
                    else
                    {
                        throw new Exception();
                    }
                }

                int N(int v) => v;
            }
            """);

    [Fact]
    public Task TestDeepThrowOnOneSide_LocalDeclaration()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    int v = N(N($$b ? 42 : throw new Exception()));
                }

                int N(int v) => v;
            }
            """,
            """
            using System;
            class C
            {
                void M(bool b)
                {
                    int v;
                    if (b)
                    {
                        v = N(N(42));
                    }
                    else
                    {
                        throw new Exception();
                    }
                }

                int N(int v) => v;
            }
            """);

    [Fact]
    public Task TestYieldReturn1()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                IEnumerable<object> M(bool b)
                {
                    yield return $$b ? 0 : 1L;
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                IEnumerable<object> M(bool b)
                {
                    if (b)
                    {
                        yield return (long)0;
                    }
                    else
                    {
                        yield return 1L;
                    }
                }
            }
            """);

    [Fact]
    public Task TestYieldReturn_OnYield1()
        => VerifyCS.VerifyRefactoringAsync(
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                IEnumerable<object> M(bool b)
                {
                    $$yield return b ? 0 : 1L;
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                IEnumerable<object> M(bool b)
                {
                    if (b)
                    {
                        yield return (long)0;
                    }
                    else
                    {
                        yield return 1L;
                    }
                }
            }
            """);
}

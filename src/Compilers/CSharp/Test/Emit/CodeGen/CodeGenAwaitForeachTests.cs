﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    [CompilerTrait(CompilerFeature.AsyncStreams)]
    public class CodeGenAwaitForeachTests : EmitMetadataTestBase
    {
        [Fact]
        public void TestWithCSharp7_3()
        {
            string source = @"
using System.Collections.Generic;
class C : IAsyncEnumerable<int>
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (int i in new C())
        {
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
        => throw null;
}";
            var expected = new[]
            {
                // (7,9): error CS8652: The feature 'async streams' is not available in C# 7.3. Please use language version 8.0 or greater.
                //         await foreach (int i in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "await").WithArguments("async streams", "8.0").WithLocation(7, 9)
            };

            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, parseOptions: TestOptions.Regular7_3);
            comp.VerifyDiagnostics(expected);

            comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TestDeconstructionWithCSharp7_3()
        {
            string source = @"
using System.Collections.Generic;
class C : IAsyncEnumerable<(int, int)>
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var (i, j) in new C())
        {
        }
    }
    IAsyncEnumerator<(int, int)> IAsyncEnumerable<(int, int)>.GetAsyncEnumerator(System.Threading.CancellationToken token)
        => throw null;
}";
            var expected = new[]
            {
                // (7,9): error CS8652: The feature 'async streams' is not available in C# 7.3. Please use language version 8.0 or greater.
                //         await foreach (int i in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "await").WithArguments("async streams", "8.0").WithLocation(7, 9)
            };

            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, parseOptions: TestOptions.Regular7_3);
            comp.VerifyDiagnostics(expected);

            comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TestWithMissingValueTask()
        {
            string lib_cs = @"
using System.Collections.Generic;
public class C : IAsyncEnumerable<int>
{
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
        => throw null;
}";

            var lib = CreateCompilationWithTasksExtensions(new[] { lib_cs, s_IAsyncEnumerable });
            lib.VerifyDiagnostics();

            string source = @"
class D
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (int i in new C()) { }
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(source, references: new[] { lib.EmitToImageReference() });
            comp.MakeTypeMissing(WellKnownType.System_Threading_Tasks_ValueTask);
            comp.VerifyDiagnostics(
                // (6,9): error CS0518: Predefined type 'System.Threading.Tasks.ValueTask' is not defined or imported
                //         await foreach (int i in new C()) { }
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "await foreach (int i in new C()) { }").WithArguments("System.Threading.Tasks.ValueTask").WithLocation(6, 9)
                );
        }

        [Fact]
        public void TestWithIAsyncEnumerator()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
public class C
{
    async Task M(IAsyncEnumerator<int> enumerator)
    {
        await foreach (int i in enumerator) { }
    }
}
";
            var comp_checked = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp_checked.VerifyDiagnostics(
                // (8,33): error CS8411: Async foreach statement cannot operate on variables of type 'IAsyncEnumerator<int>' because 'IAsyncEnumerator<int>' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (int i in enumerator) { }
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "enumerator").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestWithUIntToIntConversion()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
public class C : IAsyncEnumerable<uint>
{
    public static async Task Main()
    {
        try
        {
            REPLACE
            {
                await foreach (int i in new C()) { System.Console.Write($""0x{i:X8}""); }
            }
        }
        catch (System.OverflowException)
        {
            System.Console.Write(""overflow"");
        }
    }

    public IAsyncEnumerator<uint> GetAsyncEnumerator(System.Threading.CancellationToken token)
        => new AsyncEnumerator();
    public sealed class AsyncEnumerator : IAsyncEnumerator<uint>
    {
        bool firstValue = true;
        public uint Current => 0xFFFFFFFF;
        public async ValueTask<bool> MoveNextAsync()
        {
            await Task.Yield();
            bool result = firstValue;
            firstValue = false;
            return result;
        }
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
        }
    }
}
";
            var comp_checked = CreateCompilationWithTasksExtensions(new[] { source.Replace("REPLACE", "checked"), s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp_checked.VerifyDiagnostics();
            CompileAndVerify(comp_checked, expectedOutput: "overflow");

            var comp_unchecked = CreateCompilationWithTasksExtensions(new[] { source.Replace("REPLACE", "unchecked"), s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp_unchecked.VerifyDiagnostics();
            CompileAndVerify(comp_unchecked, expectedOutput: "0xFFFFFFFF");
        }

        [Fact]
        public void TestWithTwoIAsyncEnumerableImplementations()
        {
            string source = @"
using System.Collections.Generic;
class C : IAsyncEnumerable<int>, IAsyncEnumerable<string>
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (int i in new C())
        {
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
        => throw null;
    IAsyncEnumerator<string> IAsyncEnumerable<string>.GetAsyncEnumerator(System.Threading.CancellationToken token)
        => throw null;
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp.VerifyDiagnostics(
                // (7,33): error CS8413: Async foreach statement cannot operate on variables of type 'C' because it implements multiple instantiations of 'IAsyncEnumerable<T>'; try casting to a specific interface instantiation
                //         await foreach (int i in new C())
                Diagnostic(ErrorCode.ERR_MultipleIAsyncEnumOfT, "new C()").WithArguments("C", "System.Collections.Generic.IAsyncEnumerable<T>").WithLocation(7, 33)
                );
        }

        [Fact]
        public void TestWithMissingPattern()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8411: Async foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithStaticGetEnumerator()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public static Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        throw null;
    }
    public sealed class Enumerator
    {
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8411: Async foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithInaccessibleGetEnumerator()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    internal Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    public sealed class Enumerator
    {
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): warning CS0279: 'C' does not implement the 'async streams' pattern. 'C.GetAsyncEnumerator(System.Threading.CancellationToken)' is not a public instance or extension method.
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_PatternNotPublicOrNotInstance, "new C()").WithArguments("C", "async streams", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 33),
                // (6,33): error CS8411: Async foreach statement cannot operate on variables of type 'C' because 'C' does not contain a public definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithObsoletePatternMethods()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    [System.Obsolete]
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    public sealed class Enumerator
    {
        [System.Obsolete]
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
        {
            throw null;
        }
        [System.Obsolete]
        public int Current
        {
            get => throw null;
        }
     }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,15): warning CS0612: 'C.GetAsyncEnumerator(CancellationToken)' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 15),
                // (6,15): warning CS0612: 'C.Enumerator.MoveNextAsync()' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.Enumerator.MoveNextAsync()").WithLocation(6, 15),
                // (6,15): warning CS0612: 'C.Enumerator.Current' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.Enumerator.Current").WithLocation(6, 15)
                );
        }

        [Fact]
        public void TestWithMoveNextAsync_ReturnsValueTaskOfObject()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C()) { }
    }
    public Enumerator GetAsyncEnumerator() => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<object> MoveNextAsync() => throw null;
        public int Current => throw null;
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator()' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C()) { }
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator()").WithLocation(6, 33)
                );
        }

        [Theory, CombinatorialData, WorkItem(65363, "https://github.com/dotnet/roslyn/issues/65363")]
        public void TestWithMoveNextAsync_ReturnsValueTaskOfObject_Extension(bool useCsharp8)
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C()) { }
    }
}
static class Extension
{
    public static Enumerator GetAsyncEnumerator(this C c) => throw null;
}
public sealed class Enumerator
{
    public System.Threading.Tasks.Task<object> MoveNextAsync() => throw null;
    public int Current => throw null;
}
";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: useCsharp8 ? TestOptions.Regular8 : TestOptions.Regular9);
            if (useCsharp8)
            {
                comp.VerifyDiagnostics(
                    // (6,33): error CS8400: Feature 'extension GetAsyncEnumerator' is not available in C# 8.0. Please use language version 9.0 or greater.
                    //         await foreach (var i in new C()) { }
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion8, "new C()").WithArguments("extension GetAsyncEnumerator", "9.0").WithLocation(6, 33),
                    // (6,33): error CS8412: Asynchronous foreach requires that the return type 'Enumerator' of 'Extension.GetAsyncEnumerator(C)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                    //         await foreach (var i in new C()) { }
                    Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("Enumerator", "Extension.GetAsyncEnumerator(C)").WithLocation(6, 33)
                    );
            }
            else
            {
                comp.VerifyDiagnostics(
                    // (6,33): error CS8412: Asynchronous foreach requires that the return type 'Enumerator' of 'Extension.GetAsyncEnumerator(C)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                    //         await foreach (var i in new C()) { }
                    Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("Enumerator", "Extension.GetAsyncEnumerator(C)").WithLocation(6, 33)
                    );
            }
        }

        [Fact]
        public void TestWithMoveNextAsync_ReturnsObject()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator() => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<object> MoveNextAsync() => throw null; // returns Task<object>
        public int Current { get => throw null; }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator()' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator()").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithMoveNextAsync_Static()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    public sealed class Enumerator
    {
        public static System.Threading.Tasks.Task<bool> MoveNextAsync()
        {
            throw null;
        }
        public int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator(CancellationToken)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithCurrent_Static()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C()) { }
    }
    public Enumerator GetAsyncEnumerator()
        => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public static int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator()' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C()) { }
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator()").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithMoveNextAsync_NonPublic()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C()) { }
    }
    public Enumerator GetAsyncEnumerator()
        => throw null;
    public sealed class Enumerator
    {
        internal System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current
            => throw null;
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator()' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C()) { }
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator()").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithCurrent_NonPublicProperty()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C()) { }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
        => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        private int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS0122: 'C.Enumerator.Current' is inaccessible due to its protection level
                //         await foreach (var i in new C()) { }
                Diagnostic(ErrorCode.ERR_BadAccess, "new C()").WithArguments("C.Enumerator.Current").WithLocation(6, 33),
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator(CancellationToken)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C()) { }
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithCurrent_NonPublicGetter()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C()) { }
    }
    public Enumerator GetAsyncEnumerator()
        => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current
        {
            private get => throw null;
            set => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator()' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C()) { }
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator()").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithCurrent_MissingGetter()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C()) { }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
        => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current
        {
            set => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator(CancellationToken)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C()) { }
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithCurrent_MissingGetterOnInterface()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task M(C c)
    {
        await foreach (var i in c)
        {
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token = default) => throw null;
}
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default);
    }

    public interface IAsyncEnumerator<out T> : System.IAsyncDisposable
    {
        System.Threading.Tasks.Task<bool> MoveNextAsync();
        T Current { set; }
    }
}
namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyEmitDiagnostics(
                // (8,33): error CS8412: Asynchronous foreach requires that the return type 'IAsyncEnumerator<int>' of 'C.GetAsyncEnumerator(CancellationToken)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "c").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(8, 33),
                // (24,9): error CS1961: Invalid variance: The type parameter 'T' must be contravariantly valid on 'IAsyncEnumerator<T>.Current'. 'T' is covariant.
                //         T Current { set; }
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "T").WithArguments("System.Collections.Generic.IAsyncEnumerator<T>.Current", "T", "covariant", "contravariantly").WithLocation(24, 9)
                );
        }

        [Fact]
        public void TestWithCurrent_MissingPropertyOnInterface()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task M(C c)
    {
        await foreach (var i in c)
        {
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token = default) => throw null;
}
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default);
    }

    public interface IAsyncEnumerator<out T> : System.IAsyncDisposable
    {
        System.Threading.Tasks.Task<bool> MoveNextAsync();
    }
}
namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyEmitDiagnostics(
                // (8,33): error CS0117: 'IAsyncEnumerator<int>' does not contain a definition for 'Current'
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_NoSuchMember, "c").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "Current").WithLocation(8, 33),
                // (8,33): error CS8412: Asynchronous foreach requires that the return type 'IAsyncEnumerator<int>' of 'C.GetAsyncEnumerator(CancellationToken)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "c").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestMoveNextAsync_ReturnsTask()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task MoveNextAsync()
        {
            throw null;
        }
        public int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator(CancellationToken)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestMoveNextAsync_ReturnsTaskOfInt()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<int> MoveNextAsync()
        {
            throw null;
        }
        public int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator(CancellationToken)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestMoveNextAsync_WithOptionalParameter()
        {
            string source = @"
public class C
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async System.Threading.Tasks.Task<bool> MoveNextAsync(int ok = 1)
        {
            System.Console.Write($""MoveNextAsync {ok}"");
            await System.Threading.Tasks.Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync 1");
        }

        [Fact]
        public void TestMoveNextAsync_WithParamsParameter()
        {
            string source = @"
public class C
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async System.Threading.Tasks.Task<bool> MoveNextAsync(params int[] ok)
        {
            System.Console.Write($""MoveNextAsync {ok.Length}"");
            await System.Threading.Tasks.Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync 0");
        }

        [Fact]
        public void TestMoveNextAsync_Missing()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task M(C c)
    {
        await foreach (var i in c)
        {
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token = default) => throw null;
}
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default);
    }

    public interface IAsyncEnumerator<out T> : System.IAsyncDisposable
    {
        T Current { get; }
    }
}
namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyEmitDiagnostics(
                // (8,33): error CS0117: 'IAsyncEnumerator<int>' does not contain a definition for 'MoveNextAsync'
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_NoSuchMember, "c").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "MoveNextAsync").WithLocation(8, 33),
                // (8,33): error CS8412: Asynchronous foreach requires that the return type 'IAsyncEnumerator<int>' of 'C.GetAsyncEnumerator(CancellationToken)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "c").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestWithNonConvertibleElementType()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (string i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator()
        => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (6,15): error CS0030: Cannot convert type 'int' to 'string'
                //         await foreach (string i in new C())
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "foreach").WithArguments("int", "string").WithLocation(6, 15)
                );

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.True(info.IsAsynchronous);
            Assert.Equal("C.Enumerator C.GetAsyncEnumerator()", info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.Task<System.Boolean> C.Enumerator.MoveNextAsync()", info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 C.Enumerator.Current { get; }", info.CurrentProperty.ToTestDisplayString());
            Assert.Null(info.DisposeMethod);
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.NoConversion, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);
        }

        [Fact]
        public void TestWithNonConvertibleElementType2()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    async Task M()
    {
        await foreach (Element i in new C())
        {
        }
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
            => throw null;
    public sealed class AsyncEnumerator
    {
        public int Current
        {
            get => throw null;
        }
        public Task<bool> MoveNextAsync()
            => throw null;
        public ValueTask DisposeAsync()
            => throw null;
    }
}
class Element
{
}";
            var comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyDiagnostics(
                // (7,15): error CS0030: Cannot convert type 'int' to 'Element'
                //         await foreach (Element i in new C())
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "foreach").WithArguments("int", "Element").WithLocation(7, 15)
                );
        }

        [Fact]
        public void TestWithExplicitlyConvertibleElementType()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
class C
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (Element i in new C())
        {
            Write($""Got({i}) "");
        }
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new AsyncEnumerator();
    }
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        int i = 0;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async Task<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            bool more = await Task.FromResult(i < 4);
            return more;
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Dispose({i}) "");
            await Task.Yield();
        }
    }
}
class Element
{
    int i;
    public static explicit operator Element(int value) { Write($""Convert({value}) ""); return new Element(value); }
    private Element(int value) { i = value; }
    public override string ToString() => i.ToString();
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp,
                expectedOutput: "NextAsync(0) Current(1) Convert(1) Got(1) NextAsync(1) Current(2) Convert(2) Got(2) NextAsync(2) Current(3) Convert(3) Got(3) NextAsync(3) Dispose(4)");
        }

        [Fact]
        public void TestWithCaptureOfIterationVariable()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async System.Threading.Tasks.Task Main()
    {
        System.Action f = null;
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
            if (f == null) f = () => Write($""Captured({i})"");
        }
        f();
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new AsyncEnumerator();
    }
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        int i = 0;
        public int Current => i;
        public async Task<bool> MoveNextAsync()
        {
            i++;
            return await Task.FromResult(i < 3);
        }
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "Got(1) Got(2) Captured(1)");
        }

        [Fact]
        public void TestWithGenericIterationVariable()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
class IntContainer
{
    public int Value { get; set; }
}
public class Program
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C<IntContainer>())
        {
            Write($""Got({i.Value}) "");
        }
    }
}
class C<T> where T : IntContainer, new()
{
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new AsyncEnumerator();
    }
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        int i = 0;
        public T Current
        {
            get
            {
                Write($""Current({i}) "");
                var result = new T();
                ((IntContainer)result).Value = i;
                return result;
            }
        }
        public async Task<bool> MoveNextAsync()
        {
            i++;
            Write($""NextAsync({i}) "");
            return await Task.FromResult(i < 4);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Dispose({i}) "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "NextAsync(1) Current(1) Got(1) NextAsync(2) Current(2) Got(2) NextAsync(3) Current(3) Got(3) NextAsync(4) Dispose(4)");
        }

        [Fact]
        public void TestWithThrowingGetAsyncEnumerator()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        try
        {
            await foreach (var i in new C())
            {
                throw null;
            }
            throw null;
        }
        catch (System.ArgumentException e)
        {
            Write(e.Message);
        }
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
        => throw new System.ArgumentException(""exception"");
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current
            => throw null;
        public Task<bool> MoveNextAsync()
            => throw null;
        public ValueTask DisposeAsync()
            => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "exception");
        }

        [Fact]
        public void TestWithThrowingMoveNextAsync()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        try
        {
            await foreach (var i in new C())
            {
                throw null;
            }
            throw null;
        }
        catch (System.ArgumentException e)
        {
            Write(e.Message);
        }
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
        => new AsyncEnumerator();
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current
            => throw null;
        public Task<bool> MoveNextAsync()
            => throw new System.ArgumentException(""exception"");
        public async ValueTask DisposeAsync()
        {
            Write(""dispose "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "dispose exception");
        }

        [Fact]
        public void TestWithThrowingCurrent()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        try
        {
            await foreach (var i in new C())
            {
                throw null;
            }
            throw null;
        }
        catch (System.ArgumentException e)
        {
            Write(e.Message);
        }
    }
    public AsyncEnumerator GetAsyncEnumerator()
        => new AsyncEnumerator();
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current
            => throw new System.ArgumentException(""exception"");
        public async Task<bool> MoveNextAsync()
        {
            Write(""wait "");
            await Task.Yield();
            return true;
        }
        public async ValueTask DisposeAsync()
        {
            Write(""dispose "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "wait dispose exception");
        }

        [Fact]
        public void TestWithThrowingDisposeAsync()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        try
        {
            await foreach (var i in new C())
            {
                throw null;
            }
            throw null;
        }
        catch (System.ArgumentException e)
        {
            Write(e.Message);
        }
    }
    public AsyncEnumerator GetAsyncEnumerator()
        => new AsyncEnumerator();
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current => throw null;
        public async Task<bool> MoveNextAsync()
        {
            Write(""wait "");
            await Task.Yield();
            return false;
        }
        public ValueTask DisposeAsync()
            => throw new System.ArgumentException(""exception"");
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "wait exception");
        }

        [Fact]
        public void TestWithDynamicCollection()
        {
            string source = @"
class C
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in (dynamic)new C())
        {
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp.VerifyDiagnostics(
                // (6,33): error CS8416: Cannot use a collection of dynamic type in an asynchronous foreach
                //         await foreach (var i in (dynamic)new C())
                Diagnostic(ErrorCode.ERR_BadDynamicAwaitForEach, "(dynamic)new C()").WithLocation(6, 33));
        }

        [Fact]
        public void TestWithIncompleteInterface()
        {
            string source = @"
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
    }
}
class C
{
    async System.Threading.Tasks.Task M(System.Collections.Generic.IAsyncEnumerable<int> collection)
    {
        await foreach (var i in collection)
        {
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (12,33): error CS8411: Async foreach statement cannot operate on variables of type 'IAsyncEnumerable<int>' because 'IAsyncEnumerable<int>' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in collection)
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "collection").WithArguments("System.Collections.Generic.IAsyncEnumerable<int>", "GetAsyncEnumerator").WithLocation(12, 33)
                );
        }

        [Fact]
        public void TestWithIncompleteInterface2()
        {
            string source = @"
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default);
    }

    public interface IAsyncEnumerator<out T>
    {
        System.Threading.Tasks.Task<bool> MoveNextAsync();
    }
}
class C
{
    async System.Threading.Tasks.Task M(System.Collections.Generic.IAsyncEnumerable<int> collection)
    {
        await foreach (var i in collection)
        {
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (18,33): error CS0117: 'IAsyncEnumerator<int>' does not contain a definition for 'Current'
                //         await foreach (var i in collection)
                Diagnostic(ErrorCode.ERR_NoSuchMember, "collection").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "Current").WithLocation(18, 33),
                // (18,33): error CS8412: Async foreach requires that the return type 'IAsyncEnumerator<int>' of 'IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken)' must have a suitable public MoveNextAsync method and public Current property
                //         await foreach (var i in collection)
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "collection").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "System.Collections.Generic.IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(18, 33)
                );
        }

        [Fact]
        public void TestWithIncompleteInterface3()
        {
            string source = @"
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default);
    }

    public interface IAsyncEnumerator<out T>
    {
        T Current { get; }
    }
}
class C
{
    async System.Threading.Tasks.Task M(System.Collections.Generic.IAsyncEnumerable<int> collection)
    {
        await foreach (var i in collection)
        {
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (18,33): error CS0117: 'IAsyncEnumerator<int>' does not contain a definition for 'MoveNextAsync'
                //         await foreach (var i in collection)
                Diagnostic(ErrorCode.ERR_NoSuchMember, "collection").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "MoveNextAsync").WithLocation(18, 33),
                // (18,33): error CS8412: Async foreach requires that the return type 'IAsyncEnumerator<int>' of 'IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken)' must have a suitable public MoveNextAsync method and public Current property
                //         await foreach (var i in collection)
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "collection").WithArguments("System.Collections.Generic.IAsyncEnumerator<int>", "System.Collections.Generic.IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(18, 33)
                );
        }

        [Fact]
        public void TestWithSyncPattern()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetEnumerator()
    {
        throw null;
    }
    public sealed class Enumerator
    {
        bool MoveNext() => throw null;
        int Current => throw null;
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,33): error CS8411: Async foreach statement cannot operate on variables of type 'C' because 'C' does not contain a public definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestRegularForeachWithAsyncPattern()
        {
            string source = @"
class C
{
    void M()
    {
        foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default) => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync() => throw null;
        public int Current { get => throw null; }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                // (6,27): error CS8414: foreach statement cannot operate on variables of type 'C' because 'C' does not contain a public instance or extension definition for 'GetEnumerator'. Did you mean 'await foreach' rather than 'foreach'?
                //         foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_ForEachMissingMemberWrongAsync, "new C()").WithArguments("C", "GetEnumerator").WithLocation(6, 27)
                );
        }

        [Fact]
        public void TestRegularForeachWithAsyncInterface()
        {
            string source = @"
using System.Collections.Generic;
class C
{
    void M(IAsyncEnumerable<int> collection)
    {
        foreach (var i in collection)
        {
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (7,27): error CS8414: foreach statement cannot operate on variables of type 'IAsyncEnumerable<int>' because 'IAsyncEnumerable<int>' does not contain a public instance or extension definition for 'GetEnumerator'. Did you mean 'await foreach'?
                //         foreach (var i in collection)
                Diagnostic(ErrorCode.ERR_ForEachMissingMemberWrongAsync, "collection").WithArguments("System.Collections.Generic.IAsyncEnumerable<int>", "GetEnumerator").WithLocation(7, 27)
                );
        }

        [Fact]
        public void TestWithSyncInterfaceInRegularMethod()
        {
            string source = @"
using System.Collections.Generic;
class C
{
    void M(IEnumerable<int> collection)
    {
        await foreach (var i in collection)
        {
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (7,9): error CS4033: The 'await' operator can only be used within an async method. Consider marking this method with the 'async' modifier and changing its return type to 'Task'.
                //         await foreach (var i in collection)
                Diagnostic(ErrorCode.ERR_BadAwaitWithoutVoidAsyncMethod, "await").WithLocation(7, 9),
                // (7,33): error CS8415: Asynchronous foreach statement cannot operate on variables of type 'IEnumerable<int>' because 'IEnumerable<int>' does not contain a public instance or extension definition for 'GetAsyncEnumerator'. Did you mean 'foreach' rather than 'await foreach'?
                //         await foreach (var i in collection)
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMemberWrongAsync, "collection").WithArguments("System.Collections.Generic.IEnumerable<int>", "GetAsyncEnumerator").WithLocation(7, 33)
                );
        }

        [Fact]
        public void TestPatternBasedAsyncEnumerableWithRegularForeach()
        {
            string source = @"
class C
{
    void M()
    {
        foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
        => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current { get => throw null; }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (6,27): error CS8414: foreach statement cannot operate on variables of type 'C' because 'C' does not contain a public instance or extension definition for 'GetEnumerator'. Did you mean 'await foreach'?
                //         foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_ForEachMissingMemberWrongAsync, "new C()").WithArguments("C", "GetEnumerator").WithLocation(6, 27)
                );
        }

        [Fact]
        public void TestPatternBased_GetEnumeratorWithoutCancellationToken()
        {
            string source = @"
public class C
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator() // no parameter
        => new Enumerator(); 
    public sealed class Enumerator
    {
        public async System.Threading.Tasks.Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync"");
            await System.Threading.Tasks.Task.Yield();
            return false;
        }
        public int Current => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync");
        }

        [Fact]
        public void TestPatternBasedEnumerableWithAwaitForeach()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator<int> GetEnumerator()
        => throw null;
    public class Enumerator<T>
    {
        public T Current { get; }
        public bool MoveNext()
            => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (6,33): error CS8415: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a public instance or extension definition for 'GetAsyncEnumerator'. Did you mean 'foreach' rather than 'await foreach'?
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMemberWrongAsync, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(6, 33)
                );
        }

        [Fact]
        public void TestWithPattern()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
        {
            throw null;
        }
        public int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("C.Enumerator C.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])", info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.Task<System.Boolean> C.Enumerator.MoveNextAsync()", info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 C.Enumerator.Current { get; }", info.CurrentProperty.ToTestDisplayString());
            Assert.Null(info.DisposeMethod);
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);

            var memberModel = model.GetMemberModel(foreachSyntax);
            BoundForEachStatement boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.False(internalInfo.NeedsDisposal);
        }

        [Fact]
        public void TestWithPattern_Ref()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (ref var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
            => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current { get => throw null; }
    }
}" + s_IAsyncEnumerable;

            var comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12);
            comp.VerifyDiagnostics(
                // (6,32): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (ref var i in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "i").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(6, 32));

            var expectedDiagnostics = new[]
            {
                // (6,37): error CS1510: A ref or out value must be an assignable variable
                //         await foreach (ref var i in new C())
                Diagnostic(ErrorCode.ERR_RefLvalueExpected, "new C()").WithLocation(6, 37)
            };

            comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular13);
            comp.VerifyDiagnostics(expectedDiagnostics);

            comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyDiagnostics(expectedDiagnostics);

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            Assert.Equal(default, model.GetForEachStatementInfo(foreachSyntax));
        }

        [Theory, CombinatorialData]
        public void TestWithPattern_Ref_Iterator([CombinatorialValues("        ", "readonly")] string modifier)
        {
            var source = $$"""
                using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;

                class C
                {
                    static async Task Main()
                    {
                        await foreach (int i in F())
                        {
                            Console.Write(i);
                        }
                    }

                    static async IAsyncEnumerable<int> F()
                    {
                        await foreach (ref {{modifier}} var i in new C())
                        {
                            yield return i;
                        }
                    }

                    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default) => new();

                    public sealed class Enumerator
                    {
                        private readonly int[] _array = [1, 2, 3];
                        private int _index = -1;
                        public Task<bool> MoveNextAsync()
                        {
                            if (_index < _array.Length) _index++;
                            return Task.FromResult(_index < _array.Length);
                        }       
                        public ref int Current => ref _array[_index];
                    }
                }
                """ + AsyncStreamsTypes;

            var comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12);
            comp.VerifyDiagnostics(
                // (17,41): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (ref readonly var i in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "i").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(17, 41));

            var expectedOutput = "123";

            comp = CreateCompilationWithTasksExtensions(source, options: TestOptions.ReleaseExe, parseOptions: TestOptions.Regular13);
            CompileAndVerify(comp, expectedOutput: expectedOutput).VerifyDiagnostics();

            comp = CreateCompilationWithTasksExtensions(source, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput).VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_Ref_Iterator_Used()
        {
            var source = """
                using System.Collections.Generic;
                using System.Threading.Tasks;

                class C
                {
                    static async IAsyncEnumerable<int> F()
                    {
                        await foreach (ref var i in new C())
                        {
                            yield return i;
                            M(ref i);
                        }
                    }

                    static void M(ref int i) { }

                    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default) => new();

                    public sealed class Enumerator
                    {
                        private readonly int[] _array = [1, 2, 3];
                        private int _index = -1;
                        public Task<bool> MoveNextAsync()
                        {
                            if (_index < _array.Length) _index++;
                            return Task.FromResult(_index < _array.Length);
                        }       
                        public ref int Current => ref _array[_index];
                    }
                }
                """ + AsyncStreamsTypes;

            var comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12);
            comp.VerifyDiagnostics(
                // (8,32): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (ref var i in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "i").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(8, 32));

            var expectedDiagnostics = new[]
            {
                // (11,19): error CS9217: A 'ref' local cannot be preserved across 'await' or 'yield' boundary.
                //             M(ref i);
                Diagnostic(ErrorCode.ERR_RefLocalAcrossAwait, "i").WithLocation(11, 19)
            };

            comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular13);
            comp.VerifyEmitDiagnostics(expectedDiagnostics);

            comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyEmitDiagnostics(expectedDiagnostics);
        }

        [Fact]
        public void TestWithPattern_PointerType()
        {
            string source = @"
unsafe class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default) => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync() => throw null;
        public int* Current { get => throw null; }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.UnsafeDebugDll);
            comp.VerifyDiagnostics(
                // (6,9): error CS4004: Cannot await in an unsafe context
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitInUnsafeContext, "await").WithLocation(6, 9));

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            Assert.Equal(default, model.GetForEachStatementInfo(foreachSyntax));
        }

        [Fact]
        public void TestWithPattern_InaccessibleGetAsyncEnumerator()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new D())
        {
        }
    }
}
class D
{
    private Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token)
            => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current { get => throw null; }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (6,33): error CS8411: Async foreach statement cannot operate on variables of type 'D' because 'D' does not contain a public definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new D())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new D()").WithArguments("D", "GetAsyncEnumerator").WithLocation(6, 33)
                );

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            Assert.Equal(default, model.GetForEachStatementInfo(foreachSyntax));
        }

        [Fact]
        public void TestWithPattern_InaccessibleMoveNextAsync()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new D())
        {
        }
    }
}
class D
{
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
            => throw null;
    public sealed class Enumerator
    {
        private System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current { get => throw null; }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (6,33): error CS0122: 'D.Enumerator.MoveNextAsync()' is inaccessible due to its protection level
                //         await foreach (var i in new D())
                Diagnostic(ErrorCode.ERR_BadAccess, "new D()").WithArguments("D.Enumerator.MoveNextAsync()").WithLocation(6, 33),
                // (6,33): error CS8412: Async foreach requires that the return type 'D.Enumerator' of 'D.GetAsyncEnumerator(System.Threading.CancellationToken)' must have a suitable public MoveNextAsync method and public Current property
                //         await foreach (var i in new D())
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new D()").WithArguments("D.Enumerator", "D.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 33)
                );

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            Assert.Equal(default, model.GetForEachStatementInfo(foreachSyntax));
        }

        [Fact]
        public void TestWithPattern_InaccessibleCurrent()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new D()) { }
    }
}
class D
{
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
            => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync() => throw null;
        private int Current { get => throw null; }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (6,33): error CS0122: 'D.Enumerator.Current' is inaccessible due to its protection level
                //         await foreach (var i in new D()) { }
                Diagnostic(ErrorCode.ERR_BadAccess, "new D()").WithArguments("D.Enumerator.Current").WithLocation(6, 33),
                // (6,33): error CS8412: Async foreach requires that the return type 'D.Enumerator' of 'D.GetAsyncEnumerator(System.Threading.CancellationToken)' must have a suitable public MoveNextAsync method and public Current property
                //         await foreach (var i in new D()) { }
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new D()").WithArguments("D.Enumerator", "D.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(6, 33)
                );

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            Assert.Equal(default, model.GetForEachStatementInfo(foreachSyntax));
        }

        [Fact]
        public void TestWithPattern_InaccessibleCurrentGetter()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new D()) { }
    }
}
class D
{
    public Enumerator GetAsyncEnumerator()
            => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync() => throw null;
        public int Current { private get => throw null; set => throw null; }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (6,33): error CS8412: Async foreach requires that the return type 'D.Enumerator' of 'D.GetAsyncEnumerator()' must have a suitable public MoveNextAsync method and public Current property
                //         await foreach (var i in new D()) { }
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new D()").WithArguments("D.Enumerator", "D.GetAsyncEnumerator()").WithLocation(6, 33)
                );

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            Assert.Equal(default, model.GetForEachStatementInfo(foreachSyntax));
        }

        [Fact]
        public void TestWithPattern_RefStruct()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var s in new C())
        {
            Write($""{s.ToString()} "");
        }
        Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator() => new Enumerator();
    public sealed class Enumerator : System.IAsyncDisposable
    {
        int i = 0;
        public int Current => i;
        public async Task<bool> MoveNextAsync()
        {
            i++;
            return await Task.FromResult(i < 3);
        }
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
        }
    }
}
public ref struct S
{
    int i;
    public S(int i)
    {
        this.i = i;
    }
    public override string ToString() => i.ToString();
}
";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "1 2 Done");
        }

        [Theory]
        [InlineData("")]
        [InlineData("await Task.Yield();")]
        public void TestWithPattern_RefStructEnumerator_Async(string body)
        {
            var source = $$"""
                using System.Threading.Tasks;
                public class C
                {
                    public static async Task Main()
                    {
                        await foreach (var s in new C())
                        {
                            {{body}}
                        }
                    }
                    public Enumerator GetAsyncEnumerator() => new Enumerator();
                    public ref struct Enumerator
                    {
                        public int Current => 0;
                        public Task<bool> MoveNextAsync() => throw null;
                    }
                }
                """;

            CreateCompilation(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(
                // (6,15): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (var s in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "foreach").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(6, 15));

            var expectedDiagnostics = new[]
            {
                // (6,9): error CS4007: Instance of type 'C.Enumerator' cannot be preserved across 'await' or 'yield' boundary.
                //         await foreach (var s in new C())
                Diagnostic(ErrorCode.ERR_ByRefTypeAndAwait, @"await foreach (var s in new C())
        {
            " + body + @"
        }").WithArguments("C.Enumerator").WithLocation(6, 9)
            };

            CreateCompilation(source, parseOptions: TestOptions.Regular13).VerifyEmitDiagnostics(expectedDiagnostics);
            CreateCompilation(source).VerifyEmitDiagnostics(expectedDiagnostics);
        }

        [Theory]
        [InlineData("")]
        [InlineData("await Task.Yield();")]
        [InlineData("yield return x;")]
        [InlineData("yield return x; await Task.Yield();")]
        [InlineData("await Task.Yield(); yield return x;")]
        public void TestWithPattern_RefStructEnumerator_AsyncIterator(string body)
        {
            var source = $$"""
                using System.Collections.Generic;
                using System.Threading.Tasks;
                public class C
                {
                    public static async IAsyncEnumerable<int> M()
                    {
                        await foreach (var x in new C())
                        {
                            {{body}}
                        }
                        yield return -1;
                    }
                    public Enumerator GetAsyncEnumerator() => new Enumerator();
                    public ref struct Enumerator
                    {
                        public int Current => 0;
                        public Task<bool> MoveNextAsync() => throw null;
                    }
                }
                """ + AsyncStreamsTypes;

            CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(
                // (7,15): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (var x in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "foreach").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(7, 15));

            var expectedDiagnostics = new[]
            {
                // (7,9): error CS4007: Instance of type 'C.Enumerator' cannot be preserved across 'await' or 'yield' boundary.
                //         await foreach (var x in new C())
                Diagnostic(ErrorCode.ERR_ByRefTypeAndAwait, @"await foreach (var x in new C())
        {
            " + body + @"
        }").WithArguments("C.Enumerator").WithLocation(7, 9)
            };

            CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular13).VerifyEmitDiagnostics(expectedDiagnostics);
            CreateCompilationWithTasksExtensions(source).VerifyEmitDiagnostics(expectedDiagnostics);
        }

        [Fact]
        public void TestWithPattern_RefStructEnumerator_Iterator()
        {
            var source = """
                using System.Collections.Generic;
                public class C
                {
                    public static IEnumerable<int> M()
                    {
                        foreach (var x in new C())
                        {
                            yield return x;
                        }
                    }
                    public Enumerator GetEnumerator() => new Enumerator();
                    public ref struct Enumerator
                    {
                        public int Current => 0;
                        public bool MoveNext() => throw null;
                    }
                }
                """;

            CreateCompilation(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(
                // (6,9): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         foreach (var x in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "foreach").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(6, 9));

            var expectedDiagnostics = new[]
            {
                // (6,9): error CS4007: Instance of type 'C.Enumerator' cannot be preserved across 'await' or 'yield' boundary.
                //         foreach (var x in new C())
                Diagnostic(ErrorCode.ERR_ByRefTypeAndAwait, @"foreach (var x in new C())
        {
            yield return x;
        }").WithArguments("C.Enumerator").WithLocation(6, 9)
            };

            CreateCompilation(source, parseOptions: TestOptions.Regular13).VerifyEmitDiagnostics(expectedDiagnostics);
            CreateCompilation(source).VerifyEmitDiagnostics(expectedDiagnostics);
        }

        [Fact]
        public void TestWithPattern_RefStructEnumerable_Async()
        {
            var source = """
                using System;
                using System.Threading.Tasks;
                public class C
                {
                    public static async Task Main()
                    {
                        await foreach (var x in new Enumerable())
                        {
                            await Task.Yield();
                            Console.Write($"{x} ");
                        }
                        Console.Write("Done");
                    }
                    public ref struct Enumerable
                    {
                        public Enumerator GetAsyncEnumerator() => new();
                    }
                    public class Enumerator
                    {
                        int i = 0;
                        public int Current => i;
                        public async Task<bool> MoveNextAsync()
                        {
                            i++;
                            await Task.Yield();
                            return i < 3;
                        }
                    }
                }
                """ + s_IAsyncEnumerable;
            var comp = CreateCompilationWithTasksExtensions(source, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: "1 2 Done").VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefStructEnumerable_AsyncIterator()
        {
            var source = """
                using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                public class C
                {
                    public static async Task Main()
                    {
                        await foreach (var i in M())
                        {
                            Console.Write($"{i} ");
                        }
                        Console.Write("Done");
                    }
                    public static async IAsyncEnumerable<int> M()
                    {
                        await foreach (var x in new Enumerable())
                        {
                            await Task.Yield();
                            yield return x * 2;
                        }
                        yield return -1;
                    }
                    public ref struct Enumerable
                    {
                        public Enumerator GetAsyncEnumerator() => new();
                    }
                    public class Enumerator
                    {
                        int i = 0;
                        public int Current => i;
                        public async Task<bool> MoveNextAsync()
                        {
                            i++;
                            await Task.Yield();
                            return i < 3;
                        }
                    }
                }
                """ + AsyncStreamsTypes;
            var comp = CreateCompilationWithTasksExtensions(source, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: "2 4 -1 Done").VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefStructEnumerable_Iterator()
        {
            var source = """
                using System;
                using System.Collections.Generic;
                public class C
                {
                    public static void Main()
                    {
                        foreach (var i in M())
                        {
                            Console.Write($"{i} ");
                        }
                        Console.Write("Done");
                    }
                    public static IEnumerable<int> M()
                    {
                        foreach (var x in new Enumerable())
                        {
                            yield return x * 2;
                        }
                        yield return -1;
                    }
                    public ref struct Enumerable
                    {
                        public Enumerator GetEnumerator() => new();
                    }
                    public class Enumerator
                    {
                        int i = 0;
                        public int Current => i;
                        public bool MoveNext()
                        {
                            i++;
                            return i < 3;
                        }
                    }
                }
                """;
            CompileAndVerify(source, expectedOutput: "2 4 -1 Done").VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefStructCurrent_Async()
        {
            var source = """
                using System;
                using System.Threading.Tasks;
                public class C
                {
                    public static async Task Main()
                    {
                        await foreach (var s in new C())
                        {
                            Console.Write($"{s.ToString()} ");
                        }
                        Console.Write("Done");
                    }
                    public Enumerator GetAsyncEnumerator() => new Enumerator();
                    public sealed class Enumerator : IAsyncDisposable
                    {
                        int i = 0;
                        public S Current => new S(i);
                        public async Task<bool> MoveNextAsync()
                        {
                            i++;
                            await Task.Yield();
                            return i < 3;
                        }
                        public async ValueTask DisposeAsync()
                        {
                            await Task.Yield();
                        }
                    }
                }
                public ref struct S
                {
                    int i;
                    public S(int i)
                    {
                        this.i = i;
                    }
                    public override string ToString() => i.ToString();
                }
                """ + s_IAsyncEnumerable;

            var expectedDiagnostics = new[]
            {
                // (7,24): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (var s in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "var").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(7, 24)
            };

            CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(expectedDiagnostics);

            var expectedOutput = "1 2 Done";

            var comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular13, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput, verify: Verification.FailsILVerify).VerifyDiagnostics();

            comp = CreateCompilationWithTasksExtensions(source, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput, verify: Verification.FailsILVerify).VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefStructCurrent_AsyncIterator()
        {
            var source = """
                using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                public class C
                {
                    public static async Task Main()
                    {
                        await foreach (var s in M())
                        {
                            Console.Write($"M:{s} ");
                        }
                        Console.Write("MainDone");
                    }
                    public static async IAsyncEnumerable<string> M()
                    {
                        await foreach (var s in new C())
                        {
                            yield return s.ToString();
                        }
                        yield return "Done";
                    }
                    public Enumerator GetAsyncEnumerator() => new Enumerator();
                    public sealed class Enumerator : IAsyncDisposable
                    {
                        int i = 0;
                        public S Current => new S(i);
                        public async Task<bool> MoveNextAsync()
                        {
                            i++;
                            await Task.Yield();
                            return i < 3;
                        }
                        public async ValueTask DisposeAsync()
                        {
                            await Task.Yield();
                        }
                    }
                }
                public ref struct S
                {
                    int i;
                    public S(int i)
                    {
                        this.i = i;
                    }
                    public override string ToString() => i.ToString();
                }
                """ + AsyncStreamsTypes;

            var expectedDiagnostics = new[]
            {
                // (16,24): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (var s in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "var").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(16, 24)
            };

            CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(expectedDiagnostics);

            var expectedOutput = "M:1 M:2 M:Done MainDone";

            var comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular13, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput, verify: Verification.FailsILVerify).VerifyDiagnostics();

            comp = CreateCompilationWithTasksExtensions(source, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput, verify: Verification.FailsILVerify).VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefStructCurrent_Iterator()
        {
            var source = """
                using System;
                using System.Collections.Generic;
                public class C
                {
                    public static void Main()
                    {
                        foreach (var s in M())
                        {
                            Console.Write($"M:{s} ");
                        }
                        Console.Write("MainDone");
                    }
                    public static IEnumerable<string> M()
                    {
                        foreach (var s in new C())
                        {
                            yield return s.ToString();
                        }
                        yield return "Done";
                    }
                    public Enumerator GetEnumerator() => new Enumerator();
                    public sealed class Enumerator
                    {
                        int i = 0;
                        public S Current => new S(i);
                        public bool MoveNext()
                        {
                            i++;
                            return i < 3;
                        }
                    }
                }
                public ref struct S
                {
                    int i;
                    public S(int i)
                    {
                        this.i = i;
                    }
                    public override string ToString() => i.ToString();
                }
                """;

            var expectedOutput = "M:1 M:2 M:Done MainDone";

            CompileAndVerify(source, parseOptions: TestOptions.Regular12, expectedOutput: expectedOutput, verify: Verification.FailsILVerify).VerifyDiagnostics();
            CompileAndVerify(source, parseOptions: TestOptions.Regular13, expectedOutput: expectedOutput, verify: Verification.FailsILVerify).VerifyDiagnostics();
            CompileAndVerify(source, expectedOutput: expectedOutput, verify: Verification.FailsILVerify).VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefReturningCurrent_Async()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var s in new C())
        {
            Write($""{s.ToString()} "");
        }
        Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator() => new Enumerator();
    public sealed class Enumerator
    {
        int i = 0;
        S current;
        public ref S Current
        {
            get
            {
                current = new S(i);
                return ref current;
            }
        }
        public async Task<bool> MoveNextAsync()
        {
            i++;
            return await Task.FromResult(i < 4);
        }
    }
}
public struct S
{
    int i;
    public S(int i)
    {
        this.i = i;
    }
    public override string ToString()
        => i.ToString();
}
";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "1 2 3 Done", verify: Verification.Fails);
        }

        [Fact]
        public void TestWithPattern_RefReturningCurrent_Async_RefVariable()
        {
            string source = """
                using System;
                using System.Threading.Tasks;
                public class C
                {
                    public static async Task Main()
                    {
                        await foreach (ref var s in new C())
                        {
                            Console.Write($"{s} ");
                            s.F++;
                        }
                        Console.Write("Done");
                    }
                    public Enumerator GetAsyncEnumerator() => new Enumerator();
                    public sealed class Enumerator
                    {
                        S _current;
                        public ref S Current => ref _current;
                        public async Task<bool> MoveNextAsync()
                        {
                            Current = new S(Current.F + 1);
                            await Task.Yield();
                            return Current.F < 4;
                        }
                    }
                }
                public struct S
                {
                    public int F;
                    public S(int i)
                    {
                        this.F = i;
                    }
                    public override string ToString() => F.ToString();
                }
                """ + s_IAsyncEnumerable;

            CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(
                // (7,32): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (ref var s in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "s").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(7, 32));

            var expectedOutput = "1 3 Done";

            var comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular13, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput).VerifyDiagnostics();

            comp = CreateCompilationWithTasksExtensions(source, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput).VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefReturningCurrent_AsyncIterator_RefVariable_01()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                public class C
                {
                    public static async Task Main()
                    {
                        await foreach (var s in M())
                        {
                            Console.Write($"M:{s} ");
                        }
                        Console.Write("MainDone");
                    }
                    public static async IAsyncEnumerable<string> M()
                    {
                        await foreach (ref var s in new C())
                        {
                            s.F++;
                            yield return s.ToString();
                        }
                        yield return "Done";
                    }
                    public Enumerator GetAsyncEnumerator() => new Enumerator();
                    public sealed class Enumerator
                    {
                        S _current;
                        public ref S Current => ref _current;
                        public async Task<bool> MoveNextAsync()
                        {
                            Current = new S(Current.F + 1);
                            await Task.Yield();
                            return Current.F < 4;
                        }
                    }
                }
                public struct S
                {
                    public int F;
                    public S(int i)
                    {
                        this.F = i;
                    }
                    public override string ToString() => F.ToString();
                }
                """ + AsyncStreamsTypes;

            CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(
                // (16,32): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (ref var s in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "s").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(16, 32));

            var expectedOutput = "M:2 M:4 M:Done MainDone";

            var comp = CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular13, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput).VerifyDiagnostics();

            comp = CreateCompilationWithTasksExtensions(source, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput).VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefReturningCurrent_AsyncIterator_RefVariable_02()
        {
            string source = """
                using System.Collections.Generic;
                using System.Threading.Tasks;
                public class C
                {
                    public static async IAsyncEnumerable<string> M()
                    {
                        await foreach (ref var s in new C())
                        {
                            yield return s.ToString();
                            s.F++;
                        }
                        yield return "Done";
                    }
                    public Enumerator GetAsyncEnumerator() => new Enumerator();
                    public sealed class Enumerator
                    {
                        public ref S Current => throw null;
                        public Task<bool> MoveNextAsync() => throw null;
                    }
                }
                public struct S
                {
                    public int F;
                }
                """ + AsyncStreamsTypes;

            CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(
                // (7,32): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         await foreach (ref var s in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "s").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(7, 32));

            var expectedDiagnostics = new[]
            {
                // (10,13): error CS9217: A 'ref' local cannot be preserved across 'await' or 'yield' boundary.
                //             s.F++;
                Diagnostic(ErrorCode.ERR_RefLocalAcrossAwait, "s.F").WithLocation(10, 13)
            };

            CreateCompilationWithTasksExtensions(source, parseOptions: TestOptions.Regular13).VerifyEmitDiagnostics(expectedDiagnostics);
            CreateCompilationWithTasksExtensions(source).VerifyEmitDiagnostics(expectedDiagnostics);
        }

        [Fact]
        public void TestWithPattern_RefReturningCurrent_Iterator_RefVariable_01()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                public class C
                {
                    public static void Main()
                    {
                        foreach (var s in M())
                        {
                            Console.Write($"M:{s} ");
                        }
                        Console.Write("MainDone");
                    }
                    public static IEnumerable<string> M()
                    {
                        foreach (ref var s in new C())
                        {
                            s.F++;
                            yield return s.ToString();
                        }
                        yield return "Done";
                    }
                    public Enumerator GetEnumerator() => new Enumerator();
                    public sealed class Enumerator
                    {
                        S _current;
                        public ref S Current => ref _current;
                        public bool MoveNext()
                        {
                            Current = new S(Current.F + 1);
                            return Current.F < 4;
                        }
                    }
                }
                public struct S
                {
                    public int F;
                    public S(int i)
                    {
                        this.F = i;
                    }
                    public override string ToString() => F.ToString();
                }
                """;

            CreateCompilation(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(
                // (15,26): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         foreach (ref var s in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "s").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(15, 26));

            var expectedOutput = "M:2 M:4 M:Done MainDone";

            CompileAndVerify(source, parseOptions: TestOptions.Regular13, expectedOutput: expectedOutput).VerifyDiagnostics();
            CompileAndVerify(source, expectedOutput: expectedOutput).VerifyDiagnostics();
        }

        [Fact]
        public void TestWithPattern_RefReturningCurrent_Iterator_RefVariable_02()
        {
            string source = """
                using System.Collections.Generic;
                public class C
                {
                    public static IEnumerable<string> M()
                    {
                        foreach (ref var s in new C())
                        {
                            yield return s.ToString();
                            s.F++;
                        }
                        yield return "Done";
                    }
                    public Enumerator GetEnumerator() => new Enumerator();
                    public sealed class Enumerator
                    {
                        public ref S Current => throw null;
                        public bool MoveNext() => throw null;
                    }
                }
                public struct S
                {
                    public int F;
                }
                """;

            CreateCompilation(source, parseOptions: TestOptions.Regular12).VerifyDiagnostics(
                // (6,26): error CS9202: Feature 'ref and unsafe in async and iterator methods' is not available in C# 12.0. Please use language version 13.0 or greater.
                //         foreach (ref var s in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion12, "s").WithArguments("ref and unsafe in async and iterator methods", "13.0").WithLocation(6, 26));

            var expectedDiagnostics = new[]
            {
                // (9,13): error CS9217: A 'ref' local cannot be preserved across 'await' or 'yield' boundary.
                //             s.F++;
                Diagnostic(ErrorCode.ERR_RefLocalAcrossAwait, "s.F").WithLocation(9, 13)
            };

            CreateCompilation(source, parseOptions: TestOptions.Regular13).VerifyEmitDiagnostics(expectedDiagnostics);
            CreateCompilation(source).VerifyEmitDiagnostics(expectedDiagnostics);
        }

        [Fact]
        public void TestWithPattern_IterationVariableIsReadOnly()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
            i = 1;
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
        => throw null;
    public sealed class Enumerator
    {
        public System.Threading.Tasks.Task<bool> MoveNextAsync()
            => throw null;
        public int Current { get => throw null; }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp.VerifyDiagnostics(
                // (8,13): error CS1656: Cannot assign to 'i' because it is a 'foreach iteration variable'
                //             i = 1;
                Diagnostic(ErrorCode.ERR_AssgReadonlyLocalCause, "i").WithArguments("i", "foreach iteration variable").WithLocation(8, 13)
                );
        }

        [Fact]
        public void TestWithPattern_WithStruct_MoveNextAsyncReturnsTask()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
        Write($""Done"");
    }
    public AsyncEnumerator GetAsyncEnumerator()
    {
        return new AsyncEnumerator(0);
    }
    public struct AsyncEnumerator
    {
        int i;
        internal AsyncEnumerator(int start) { i = start; }
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                i++;
                return i;
            }
        }
        public async Task<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            bool more = await Task.FromResult(i < 4);
            i = i + 100; // Note: side-effects of async methods in structs are lost
            return more;
        }
        public async ValueTask DisposeAsync()
        {
            Write($""DisposeAsync "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp,
                expectedOutput: "NextAsync(0) Current(0) Got(1) NextAsync(1) Current(1) Got(2) NextAsync(2) Current(2) Got(3) NextAsync(3) Current(3) Got(4) NextAsync(4) DisposeAsync Done");
        }

        [Fact]
        public void TestWithPattern_MoveNextAsyncReturnsValueTask()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
        Write($""Done"");
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new AsyncEnumerator(0);
    }
    public class AsyncEnumerator : System.IAsyncDisposable
    {
        int i;
        internal AsyncEnumerator(int start) { i = start; }
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Dispose({i}) "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Got(2) NextAsync(2) Current(3) Got(3) NextAsync(3) Dispose(4) Done");

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> C.AsyncEnumerator.MoveNextAsync()", info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);
        }

        [Fact]
        [WorkItem(31609, "https://github.com/dotnet/roslyn/issues/31609")]
        public void TestWithPattern_MoveNextAsyncReturnsAwaitable()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Item({i}) "");
            break;
        }
        Write($""Done"");
    }
    public AsyncEnumerator GetAsyncEnumerator()
    {
        return new AsyncEnumerator();
    }
    public class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current => 1;
        public Awaitable MoveNextAsync()
        {
            return new Awaitable();
        }
        public async ValueTask DisposeAsync()
        {
            Write(""Dispose "");
            await Task.Yield();
        }
    }

    public class Awaitable
    {
        public Awaiter GetAwaiter() { return new Awaiter(); }
    }
    public class Awaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        public bool IsCompleted { get { return true; } }
        public bool GetResult() { return true; }
        public void OnCompleted(System.Action continuation) { }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "Item(1) Dispose Done");

            var tree = comp.SyntaxTrees.First();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("C.Awaitable C.AsyncEnumerator.MoveNextAsync()", info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
        }

        [Fact]
        [WorkItem(31609, "https://github.com/dotnet/roslyn/issues/31609")]
        public void TestWithPattern_MoveNextAsyncReturnsAwaitable_WithoutGetAwaiter()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default) => throw null;
    public class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current => 1;
        public Awaitable MoveNextAsync() => throw null;
        public ValueTask DisposeAsync() => throw null;
    }
    public class Awaitable
    {
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,33): error CS1061: 'C.Awaitable' does not contain a definition for 'GetAwaiter' and no accessible extension method 'GetAwaiter' accepting a first argument of type 'C.Awaitable' could be found (are you missing a using directive or an assembly reference?)
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "new C()").WithArguments("C.Awaitable", "GetAwaiter").WithLocation(7, 33)
                );
            VerifyEmptyForEachStatementInfo(comp);
        }

        [Fact]
        [WorkItem(31609, "https://github.com/dotnet/roslyn/issues/31609")]
        public void TestWithPattern_MoveNextAsyncReturnsAwaitable_WithoutIsCompleted()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public AsyncEnumerator GetAsyncEnumerator() => throw null;
    public class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current => 1;
        public Awaitable MoveNextAsync() => throw null;
        public ValueTask DisposeAsync() => throw null;
    }
    public class Awaitable
    {
        public Awaiter GetAwaiter() => throw null;
    }
    public class Awaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        public bool GetResult() { return true; }
        public void OnCompleted(System.Action continuation) { }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,33): error CS0117: 'C.Awaiter' does not contain a definition for 'IsCompleted'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("C.Awaiter", "IsCompleted").WithLocation(7, 33)
                );
            VerifyEmptyForEachStatementInfo(comp);
        }

        private static void VerifyEmptyForEachStatementInfo(CSharpCompilation comp)
        {
            var tree = comp.SyntaxTrees.First();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Null(info.MoveNextMethod);
            Assert.Null(info.ElementType);
        }

        [Fact]
        [WorkItem(31609, "https://github.com/dotnet/roslyn/issues/31609")]
        public void TestWithPattern_MoveNextAsyncReturnsAwaitable_WithoutGetResult()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public AsyncEnumerator GetAsyncEnumerator() => throw null;
    public class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current => 1;
        public Awaitable MoveNextAsync() => throw null;
        public ValueTask DisposeAsync() => throw null;
    }
    public class Awaitable
    {
        public Awaiter GetAwaiter() => throw null;
    }
    public class Awaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        public bool IsCompleted { get { return true; } }
        public void OnCompleted(System.Action continuation) { }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,33): error CS1061: 'C.Awaiter' does not contain a definition for 'GetResult' and no accessible extension method 'GetResult' accepting a first argument of type 'C.Awaiter' could be found (are you missing a using directive or an assembly reference?)
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "new C()").WithArguments("C.Awaiter", "GetResult").WithLocation(7, 33)
                );
            VerifyEmptyForEachStatementInfo(comp);
        }

        [Fact]
        [WorkItem(31609, "https://github.com/dotnet/roslyn/issues/31609")]
        public void TestWithPattern_MoveNextAsyncReturnsAwaitable_WithoutOnCompleted()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default) => throw null;
    public class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current => 1;
        public Awaitable MoveNextAsync() => throw null;
        public ValueTask DisposeAsync() => throw null;
    }

    public class Awaitable
    {
        public Awaiter GetAwaiter() => throw null;
    }
    public class Awaiter
    {
        public bool IsCompleted { get { return true; } }
        public bool GetResult() { return true; }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,33): error CS4027: 'C.Awaiter' does not implement 'INotifyCompletion'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_DoesntImplementAwaitInterface, "new C()").WithArguments("C.Awaiter", "System.Runtime.CompilerServices.INotifyCompletion").WithLocation(7, 33)
                );
            VerifyEmptyForEachStatementInfo(comp);
        }

        [Fact]
        public void TestWithPattern_MoveNextAsyncReturnsBadType()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public AsyncEnumerator GetAsyncEnumerator() => throw null;
    public class AsyncEnumerator
    {
        public int Current => throw null;
        public int MoveNextAsync() => throw null;
        public ValueTask DisposeAsync() => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp.VerifyDiagnostics(
                // (7,33): error CS1061: 'int' does not contain a definition for 'GetAwaiter' and no accessible extension method 'GetAwaiter' accepting a first argument of type 'int' could be found (are you missing a using directive or an assembly reference?)
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "new C()").WithArguments("int", "GetAwaiter").WithLocation(7, 33)
                );

            var tree = comp.SyntaxTrees.First();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);
            Assert.Null(info.MoveNextMethod);
            Assert.Null(info.ElementType);
        }

        [Fact]
        public void TestWithPattern_WithUnsealed()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public class Enumerator
    {
        int i = 0;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async Task<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();

            var memberModel = model.GetMemberModel(foreachSyntax);
            BoundForEachStatement boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);

            CompileAndVerify(comp,
                expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Got(2) NextAsync(2) Current(3) Got(3) NextAsync(3) Dispose(4)");
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = ConditionalSkipReason.NativePdbRequiresDesktop)]
        public void TestWithPattern_WithUnsealed_WithIAsyncDisposable()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
    }
    public Enumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new DerivedEnumerator();
    }
    public class Enumerator
    {
        protected int i = 0;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async Task<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
    }
    public class DerivedEnumerator : Enumerator, System.IAsyncDisposable
    {
        public ValueTask DisposeAsync() => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.False(internalInfo.NeedsDisposal);

            var verifier = CompileAndVerify(comp,
                expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Got(2) NextAsync(2) Current(3) Got(3) NextAsync(3)");

            verifier.VerifyIL("C.<Main>d__0.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size      273 (0x111)
  .maxstack  3
  .locals init (int V_0,
                System.Threading.CancellationToken V_1,
                System.Runtime.CompilerServices.TaskAwaiter<bool> V_2,
                C.<Main>d__0 V_3,
                System.Exception V_4)
  // sequence point: <hidden>
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<Main>d__0.<>1__state""
  IL_0006:  stloc.0
  .try
  {
    // sequence point: <hidden>
    IL_0007:  ldloc.0
    IL_0008:  brfalse.s  IL_0012
    IL_000a:  br.s       IL_000c
    IL_000c:  ldloc.0
    IL_000d:  ldc.i4.1
    IL_000e:  beq.s      IL_0017
    IL_0010:  br.s       IL_001c
    IL_0012:  br         IL_00a5
    IL_0017:  br         IL_00a5
    // sequence point: {
    IL_001c:  nop
    // sequence point: await foreach
    IL_001d:  nop
    // sequence point: new C()
    IL_001e:  ldarg.0
    IL_001f:  newobj     ""C..ctor()""
    IL_0024:  ldloca.s   V_1
    IL_0026:  initobj    ""System.Threading.CancellationToken""
    IL_002c:  ldloc.1
    IL_002d:  call       ""C.Enumerator C.GetAsyncEnumerator(System.Threading.CancellationToken)""
    IL_0032:  stfld      ""C.Enumerator C.<Main>d__0.<>s__1""
    // sequence point: <hidden>
    IL_0037:  br.s       IL_0067
    // sequence point: var i
    IL_0039:  ldarg.0
    IL_003a:  ldarg.0
    IL_003b:  ldfld      ""C.Enumerator C.<Main>d__0.<>s__1""
    IL_0040:  callvirt   ""int C.Enumerator.Current.get""
    IL_0045:  stfld      ""int C.<Main>d__0.<i>5__2""
    // sequence point: {
    IL_004a:  nop
    // sequence point: Write($""Got({i}) "");
    IL_004b:  ldstr      ""Got({0}) ""
    IL_0050:  ldarg.0
    IL_0051:  ldfld      ""int C.<Main>d__0.<i>5__2""
    IL_0056:  box        ""int""
    IL_005b:  call       ""string string.Format(string, object)""
    IL_0060:  call       ""void System.Console.Write(string)""
    IL_0065:  nop
    // sequence point: }
    IL_0066:  nop
    // sequence point: in
    IL_0067:  ldarg.0
    IL_0068:  ldfld      ""C.Enumerator C.<Main>d__0.<>s__1""
    IL_006d:  callvirt   ""System.Threading.Tasks.Task<bool> C.Enumerator.MoveNextAsync()""
    IL_0072:  callvirt   ""System.Runtime.CompilerServices.TaskAwaiter<bool> System.Threading.Tasks.Task<bool>.GetAwaiter()""
    IL_0077:  stloc.2
    // sequence point: <hidden>
    IL_0078:  ldloca.s   V_2
    IL_007a:  call       ""bool System.Runtime.CompilerServices.TaskAwaiter<bool>.IsCompleted.get""
    IL_007f:  brtrue.s   IL_00c1
    IL_0081:  ldarg.0
    IL_0082:  ldc.i4.0
    IL_0083:  dup
    IL_0084:  stloc.0
    IL_0085:  stfld      ""int C.<Main>d__0.<>1__state""
    // async: yield
    IL_008a:  ldarg.0
    IL_008b:  ldloc.2
    IL_008c:  stfld      ""System.Runtime.CompilerServices.TaskAwaiter<bool> C.<Main>d__0.<>u__1""
    IL_0091:  ldarg.0
    IL_0092:  stloc.3
    IL_0093:  ldarg.0
    IL_0094:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
    IL_0099:  ldloca.s   V_2
    IL_009b:  ldloca.s   V_3
    IL_009d:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<bool>, C.<Main>d__0>(ref System.Runtime.CompilerServices.TaskAwaiter<bool>, ref C.<Main>d__0)""
    IL_00a2:  nop
    IL_00a3:  leave.s    IL_0110
    // async: resume
    IL_00a5:  ldarg.0
    IL_00a6:  ldfld      ""System.Runtime.CompilerServices.TaskAwaiter<bool> C.<Main>d__0.<>u__1""
    IL_00ab:  stloc.2
    IL_00ac:  ldarg.0
    IL_00ad:  ldflda     ""System.Runtime.CompilerServices.TaskAwaiter<bool> C.<Main>d__0.<>u__1""
    IL_00b2:  initobj    ""System.Runtime.CompilerServices.TaskAwaiter<bool>""
    IL_00b8:  ldarg.0
    IL_00b9:  ldc.i4.m1
    IL_00ba:  dup
    IL_00bb:  stloc.0
    IL_00bc:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_00c1:  ldarg.0
    IL_00c2:  ldloca.s   V_2
    IL_00c4:  call       ""bool System.Runtime.CompilerServices.TaskAwaiter<bool>.GetResult()""
    IL_00c9:  stfld      ""bool C.<Main>d__0.<>s__3""
    IL_00ce:  ldarg.0
    IL_00cf:  ldfld      ""bool C.<Main>d__0.<>s__3""
    IL_00d4:  brtrue     IL_0039
    IL_00d9:  ldarg.0
    IL_00da:  ldnull
    IL_00db:  stfld      ""C.Enumerator C.<Main>d__0.<>s__1""
    IL_00e0:  leave.s    IL_00fc
  }
  catch System.Exception
  {
    // async: catch handler, sequence point: <hidden>
    IL_00e2:  stloc.s    V_4
    IL_00e4:  ldarg.0
    IL_00e5:  ldc.i4.s   -2
    IL_00e7:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_00ec:  ldarg.0
    IL_00ed:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
    IL_00f2:  ldloc.s    V_4
    IL_00f4:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetException(System.Exception)""
    IL_00f9:  nop
    IL_00fa:  leave.s    IL_0110
  }
  // sequence point: }
  IL_00fc:  ldarg.0
  IL_00fd:  ldc.i4.s   -2
  IL_00ff:  stfld      ""int C.<Main>d__0.<>1__state""
  // sequence point: <hidden>
  IL_0104:  ldarg.0
  IL_0105:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
  IL_010a:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetResult()""
  IL_010f:  nop
  IL_0110:  ret
}
", sequencePoints: "C+<Main>d__0.MoveNext", source: source + s_IAsyncEnumerable);
        }

        [Fact]
        public void TestWithPattern_WithIAsyncDisposable()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator : System.IAsyncDisposable
    {
        int i = 0;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async Task<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();

            var memberModel = model.GetMemberModel(foreachSyntax);
            BoundForEachStatement boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);

            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Got(2) NextAsync(2) Current(3) Got(3) NextAsync(3) Dispose(4)");
        }

        [Fact]
        public void TestWithPattern_WithIAsyncDisposableUseSiteError()
        {
            string enumerator = @"
using System.Threading.Tasks;
public class C
{
    public Enumerator GetAsyncEnumerator() => throw null;
    public sealed class Enumerator : System.IAsyncDisposable
    {
        public int Current { get => throw null; }
        public Task<bool> MoveNextAsync() => throw null;
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
        }
    }
}";
            string source = @"
using System.Threading.Tasks;
class Client
{
    async Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
}";
            var lib = CreateCompilationWithTasksExtensions(enumerator + s_IAsyncEnumerable);
            lib.VerifyDiagnostics();

            var comp = CreateCompilationWithTasksExtensions(source, references: new[] { lib.EmitToImageReference() });
            comp.MakeTypeMissing(WellKnownType.System_IAsyncDisposable);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);
        }

        [Fact]
        public void TestWithMultipleInterface()
        {
            string source = @"
using System.Collections.Generic;
class C : IAsyncEnumerable<int>, IAsyncEnumerable<string>
{
    async System.Threading.Tasks.Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        throw null;
    }
    IAsyncEnumerator<string> IAsyncEnumerable<string>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (7,33): error CS8413: Async foreach statement cannot operate on variables of type 'C' because it implements multiple instantiations of 'IAsyncEnumerable<T>'; try casting to a specific interface instantiation
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_MultipleIAsyncEnumOfT, "new C()").WithArguments("C", "System.Collections.Generic.IAsyncEnumerable<T>").WithLocation(7, 33)
                );
        }

        [Fact]
        public void TestWithMultipleImplementations()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class Base : IAsyncEnumerable<string>
{
    IAsyncEnumerator<string> IAsyncEnumerable<string>.GetAsyncEnumerator(System.Threading.CancellationToken token)
        => throw null;
}
class C : Base, IAsyncEnumerable<int>
{
    async Task M()
    {
        await foreach (var i in new C())
        {
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
        => throw null;
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (13,33): error CS8413: Async foreach statement cannot operate on variables of type 'C' because it implements multiple instantiations of 'IAsyncEnumerable<T>'; try casting to a specific interface instantiation
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_MultipleIAsyncEnumOfT, "new C()").WithArguments("C", "System.Collections.Generic.IAsyncEnumerable<T>").WithLocation(13, 33)
                );
        }

        [Fact]
        public void TestWithInterface()
        {
            string source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static System.Console;
class C : IAsyncEnumerable<int>
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator();
    }
    sealed class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i = 0;
        int IAsyncEnumerator<int>.Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        async ValueTask<bool> IAsyncEnumerator<int>.MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Got(2) NextAsync(2) Current(3) Got(3) NextAsync(3) Dispose(4)");

            var tree = comp.SyntaxTrees.First();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("System.Collections.Generic.IAsyncEnumerator<System.Int32> System.Collections.Generic.IAsyncEnumerable<System.Int32>.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> System.Collections.Generic.IAsyncEnumerator<System.Int32>.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 System.Collections.Generic.IAsyncEnumerator<System.Int32>.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()", info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);
        }

        [Fact]
        public void TestWithInterface_OnStruct_ImplicitInterfaceImplementation()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
struct C : IAsyncEnumerable<int>
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator();
    }
    class AsyncEnumerator : IAsyncEnumerator<int>
    {
        public int i;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}";
            // Note: the enumerator type should not be a struct, otherwise you will loop forever
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            var verifier = CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Got(2) NextAsync(2) Current(3) Got(3) NextAsync(3) Dispose(4)");

            // The thing to notice here is that the call to GetAsyncEnumerator is a constrained call (we're not boxing to `IAsyncEnumerable<int>`)
            verifier.VerifyIL("C.<Main>d__0.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size      496 (0x1f0)
  .maxstack  3
  .locals init (int V_0,
                C V_1,
                System.Threading.CancellationToken V_2,
                System.Runtime.CompilerServices.ValueTaskAwaiter<bool> V_3,
                System.Threading.Tasks.ValueTask<bool> V_4,
                C.<Main>d__0 V_5,
                object V_6,
                System.Runtime.CompilerServices.ValueTaskAwaiter V_7,
                System.Threading.Tasks.ValueTask V_8,
                System.Exception V_9)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<Main>d__0.<>1__state""
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldloc.0
    IL_0008:  brfalse.s  IL_0012
    IL_000a:  br.s       IL_000c
    IL_000c:  ldloc.0
    IL_000d:  ldc.i4.1
    IL_000e:  beq.s      IL_0014
    IL_0010:  br.s       IL_0019
    IL_0012:  br.s       IL_004c
    IL_0014:  br         IL_015c
    IL_0019:  nop
    IL_001a:  nop
    IL_001b:  ldarg.0
    IL_001c:  ldloca.s   V_1
    IL_001e:  dup
    IL_001f:  initobj    ""C""
    IL_0025:  ldloca.s   V_2
    IL_0027:  initobj    ""System.Threading.CancellationToken""
    IL_002d:  ldloc.2
    IL_002e:  constrained. ""C""
    IL_0034:  callvirt   ""System.Collections.Generic.IAsyncEnumerator<int> System.Collections.Generic.IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken)""
    IL_0039:  stfld      ""System.Collections.Generic.IAsyncEnumerator<int> C.<Main>d__0.<>s__1""
    IL_003e:  ldarg.0
    IL_003f:  ldnull
    IL_0040:  stfld      ""object C.<Main>d__0.<>s__2""
    IL_0045:  ldarg.0
    IL_0046:  ldc.i4.0
    IL_0047:  stfld      ""int C.<Main>d__0.<>s__3""
    IL_004c:  nop
    .try
    {
      IL_004d:  ldloc.0
      IL_004e:  brfalse.s  IL_0052
      IL_0050:  br.s       IL_0054
      IL_0052:  br.s       IL_00ca
      IL_0054:  br.s       IL_0084
      IL_0056:  ldarg.0
      IL_0057:  ldarg.0
      IL_0058:  ldfld      ""System.Collections.Generic.IAsyncEnumerator<int> C.<Main>d__0.<>s__1""
      IL_005d:  callvirt   ""int System.Collections.Generic.IAsyncEnumerator<int>.Current.get""
      IL_0062:  stfld      ""int C.<Main>d__0.<i>5__4""
      IL_0067:  nop
      IL_0068:  ldstr      ""Got({0}) ""
      IL_006d:  ldarg.0
      IL_006e:  ldfld      ""int C.<Main>d__0.<i>5__4""
      IL_0073:  box        ""int""
      IL_0078:  call       ""string string.Format(string, object)""
      IL_007d:  call       ""void System.Console.Write(string)""
      IL_0082:  nop
      IL_0083:  nop
      IL_0084:  ldarg.0
      IL_0085:  ldfld      ""System.Collections.Generic.IAsyncEnumerator<int> C.<Main>d__0.<>s__1""
      IL_008a:  callvirt   ""System.Threading.Tasks.ValueTask<bool> System.Collections.Generic.IAsyncEnumerator<int>.MoveNextAsync()""
      IL_008f:  stloc.s    V_4
      IL_0091:  ldloca.s   V_4
      IL_0093:  call       ""System.Runtime.CompilerServices.ValueTaskAwaiter<bool> System.Threading.Tasks.ValueTask<bool>.GetAwaiter()""
      IL_0098:  stloc.3
      IL_0099:  ldloca.s   V_3
      IL_009b:  call       ""bool System.Runtime.CompilerServices.ValueTaskAwaiter<bool>.IsCompleted.get""
      IL_00a0:  brtrue.s   IL_00e6
      IL_00a2:  ldarg.0
      IL_00a3:  ldc.i4.0
      IL_00a4:  dup
      IL_00a5:  stloc.0
      IL_00a6:  stfld      ""int C.<Main>d__0.<>1__state""
      IL_00ab:  ldarg.0
      IL_00ac:  ldloc.3
      IL_00ad:  stfld      ""System.Runtime.CompilerServices.ValueTaskAwaiter<bool> C.<Main>d__0.<>u__1""
      IL_00b2:  ldarg.0
      IL_00b3:  stloc.s    V_5
      IL_00b5:  ldarg.0
      IL_00b6:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
      IL_00bb:  ldloca.s   V_3
      IL_00bd:  ldloca.s   V_5
      IL_00bf:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.ValueTaskAwaiter<bool>, C.<Main>d__0>(ref System.Runtime.CompilerServices.ValueTaskAwaiter<bool>, ref C.<Main>d__0)""
      IL_00c4:  nop
      IL_00c5:  leave      IL_01ef
      IL_00ca:  ldarg.0
      IL_00cb:  ldfld      ""System.Runtime.CompilerServices.ValueTaskAwaiter<bool> C.<Main>d__0.<>u__1""
      IL_00d0:  stloc.3
      IL_00d1:  ldarg.0
      IL_00d2:  ldflda     ""System.Runtime.CompilerServices.ValueTaskAwaiter<bool> C.<Main>d__0.<>u__1""
      IL_00d7:  initobj    ""System.Runtime.CompilerServices.ValueTaskAwaiter<bool>""
      IL_00dd:  ldarg.0
      IL_00de:  ldc.i4.m1
      IL_00df:  dup
      IL_00e0:  stloc.0
      IL_00e1:  stfld      ""int C.<Main>d__0.<>1__state""
      IL_00e6:  ldarg.0
      IL_00e7:  ldloca.s   V_3
      IL_00e9:  call       ""bool System.Runtime.CompilerServices.ValueTaskAwaiter<bool>.GetResult()""
      IL_00ee:  stfld      ""bool C.<Main>d__0.<>s__5""
      IL_00f3:  ldarg.0
      IL_00f4:  ldfld      ""bool C.<Main>d__0.<>s__5""
      IL_00f9:  brtrue     IL_0056
      IL_00fe:  leave.s    IL_010c
    }
    catch object
    {
      IL_0100:  stloc.s    V_6
      IL_0102:  ldarg.0
      IL_0103:  ldloc.s    V_6
      IL_0105:  stfld      ""object C.<Main>d__0.<>s__2""
      IL_010a:  leave.s    IL_010c
    }
    IL_010c:  ldarg.0
    IL_010d:  ldfld      ""System.Collections.Generic.IAsyncEnumerator<int> C.<Main>d__0.<>s__1""
    IL_0112:  brfalse.s  IL_0181
    IL_0114:  ldarg.0
    IL_0115:  ldfld      ""System.Collections.Generic.IAsyncEnumerator<int> C.<Main>d__0.<>s__1""
    IL_011a:  callvirt   ""System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()""
    IL_011f:  stloc.s    V_8
    IL_0121:  ldloca.s   V_8
    IL_0123:  call       ""System.Runtime.CompilerServices.ValueTaskAwaiter System.Threading.Tasks.ValueTask.GetAwaiter()""
    IL_0128:  stloc.s    V_7
    IL_012a:  ldloca.s   V_7
    IL_012c:  call       ""bool System.Runtime.CompilerServices.ValueTaskAwaiter.IsCompleted.get""
    IL_0131:  brtrue.s   IL_0179
    IL_0133:  ldarg.0
    IL_0134:  ldc.i4.1
    IL_0135:  dup
    IL_0136:  stloc.0
    IL_0137:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_013c:  ldarg.0
    IL_013d:  ldloc.s    V_7
    IL_013f:  stfld      ""System.Runtime.CompilerServices.ValueTaskAwaiter C.<Main>d__0.<>u__2""
    IL_0144:  ldarg.0
    IL_0145:  stloc.s    V_5
    IL_0147:  ldarg.0
    IL_0148:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
    IL_014d:  ldloca.s   V_7
    IL_014f:  ldloca.s   V_5
    IL_0151:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.ValueTaskAwaiter, C.<Main>d__0>(ref System.Runtime.CompilerServices.ValueTaskAwaiter, ref C.<Main>d__0)""
    IL_0156:  nop
    IL_0157:  leave      IL_01ef
    IL_015c:  ldarg.0
    IL_015d:  ldfld      ""System.Runtime.CompilerServices.ValueTaskAwaiter C.<Main>d__0.<>u__2""
    IL_0162:  stloc.s    V_7
    IL_0164:  ldarg.0
    IL_0165:  ldflda     ""System.Runtime.CompilerServices.ValueTaskAwaiter C.<Main>d__0.<>u__2""
    IL_016a:  initobj    ""System.Runtime.CompilerServices.ValueTaskAwaiter""
    IL_0170:  ldarg.0
    IL_0171:  ldc.i4.m1
    IL_0172:  dup
    IL_0173:  stloc.0
    IL_0174:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_0179:  ldloca.s   V_7
    IL_017b:  call       ""void System.Runtime.CompilerServices.ValueTaskAwaiter.GetResult()""
    IL_0180:  nop
    IL_0181:  ldarg.0
    IL_0182:  ldfld      ""object C.<Main>d__0.<>s__2""
    IL_0187:  stloc.s    V_6
    IL_0189:  ldloc.s    V_6
    IL_018b:  brfalse.s  IL_01aa
    IL_018d:  ldloc.s    V_6
    IL_018f:  isinst     ""System.Exception""
    IL_0194:  stloc.s    V_9
    IL_0196:  ldloc.s    V_9
    IL_0198:  brtrue.s   IL_019d
    IL_019a:  ldloc.s    V_6
    IL_019c:  throw
    IL_019d:  ldloc.s    V_9
    IL_019f:  call       ""System.Runtime.ExceptionServices.ExceptionDispatchInfo System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(System.Exception)""
    IL_01a4:  callvirt   ""void System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()""
    IL_01a9:  nop
    IL_01aa:  ldarg.0
    IL_01ab:  ldfld      ""int C.<Main>d__0.<>s__3""
    IL_01b0:  pop
    IL_01b1:  ldarg.0
    IL_01b2:  ldnull
    IL_01b3:  stfld      ""object C.<Main>d__0.<>s__2""
    IL_01b8:  ldarg.0
    IL_01b9:  ldnull
    IL_01ba:  stfld      ""System.Collections.Generic.IAsyncEnumerator<int> C.<Main>d__0.<>s__1""
    IL_01bf:  leave.s    IL_01db
  }
  catch System.Exception
  {
    IL_01c1:  stloc.s    V_9
    IL_01c3:  ldarg.0
    IL_01c4:  ldc.i4.s   -2
    IL_01c6:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_01cb:  ldarg.0
    IL_01cc:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
    IL_01d1:  ldloc.s    V_9
    IL_01d3:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetException(System.Exception)""
    IL_01d8:  nop
    IL_01d9:  leave.s    IL_01ef
  }
  IL_01db:  ldarg.0
  IL_01dc:  ldc.i4.s   -2
  IL_01de:  stfld      ""int C.<Main>d__0.<>1__state""
  IL_01e3:  ldarg.0
  IL_01e4:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
  IL_01e9:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetResult()""
  IL_01ee:  nop
  IL_01ef:  ret
}");
        }

        [Fact]
        public void TestWithInterface_WithEarlyCompletion1()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
        Write($""Done"");
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator(2);
    }
    internal sealed class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i;
        internal AsyncEnumerator(int start) { i = start; }
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
        public ValueTask DisposeAsync()
        {
            Write($""Dispose({i}) "");
            return new ValueTask(Task.CompletedTask); // return a completed task
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp, expectedOutput: "NextAsync(2) Current(3) Got(3) NextAsync(3) Dispose(4) Done");
        }

        [Fact]
        public void TestWithInterface_WithBreakAndContinue()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
            if (i == 2 || i == 3) { Write($""Continue({i}) ""); continue; }
            if (i == 4) { Write(""Break ""); break; }
            Write($""Got({i}) "");
        }
        Write(""Done"");
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator();
    }
    sealed class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i = 0;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 10);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp,
                expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Continue(2) NextAsync(2) Current(3) Continue(3) NextAsync(3) Current(4) Break Dispose(4) Done");
        }

        [Fact]
        public void TestWithInterface_WithGoto()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
            if (i == 2 || i == 3) { Write($""Continue({i}) ""); continue; }
            if (i == 4) { Write(""Goto ""); goto done; }
            Write($""Got({i}) "");
        }
        done:
        Write(""Done"");
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator();
    }
    sealed class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i = 0;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 10);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp,
                expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Continue(2) NextAsync(2) Current(3) Continue(3) NextAsync(3) Current(4) Goto Dispose(4) Done");
        }

        [Fact]
        public void TestWithInterface_WithStruct()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    static async Task Main()
    {
        await foreach (var i in new C())
        {
            Write($""Got({i}) "");
        }
        Write($""Done"");
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator(0);
    }
    internal struct AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i;
        internal AsyncEnumerator(int start) { i = start; }
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                i++;
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            bool more = await Task.FromResult(i < 4);
            i = i + 100; // Note: side-effects of async methods in structs are lost
            return more;
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Dispose({i}) "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp,
                expectedOutput: "NextAsync(0) Current(0) Got(1) NextAsync(1) Current(1) Got(2) NextAsync(2) Current(2) Got(3) NextAsync(3) Current(3) Got(4) NextAsync(4) Dispose(4) Done");
        }

        [Fact, WorkItem(27651, "https://github.com/dotnet/roslyn/issues/27651")]
        public void TestControlFlowAnalysis()
        {
            string source = @"
class C
{
    async System.Threading.Tasks.Task M(System.Collections.Generic.IAsyncEnumerable<int> collection)
    {
        await foreach (var item in collection) { }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var loop = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();

            var ctrlFlowAnalysis = model.AnalyzeControlFlow(loop);
            Assert.Equal(0, ctrlFlowAnalysis.ExitPoints.Count());
        }

        [Fact]
        public void TestWithNullLiteralCollection()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    async Task M()
    {
        await foreach (var i in null)
        {
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (8,33): error CS0186: Use of null is not valid in this context
                //         await foreach (var i in null)
                Diagnostic(ErrorCode.ERR_NullNotValid, "null").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestWithNullCollection()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task Main()
    {
        C c = null;
        try
        {
            await foreach (var i in c)
            {
            }
        }
        catch (System.NullReferenceException)
        {
            System.Console.Write(""Success"");
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator(0);
    }
    internal struct AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i;
        internal AsyncEnumerator(int start) { i = start; }
        public int Current { get => throw new System.Exception(); }
        public ValueTask<bool> MoveNextAsync() => throw new System.Exception();
        public ValueTask DisposeAsync() => throw new System.Exception();
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "Success");
        }

        [Fact]
        public void TestInCatch()
        {
            string source = @"
using System.Collections.Generic;
using static System.Console;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task Main()
    {
        try
        {
            Write($""Try "");
            throw null;
        }
        catch (System.NullReferenceException)
        {
            await foreach (var i in new C())
            {
                Write($""Got({i}) "");
            }
        }
        Write($""Done"");
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator(0);
    }
    internal class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i;
        internal AsyncEnumerator(int start) { i = start; }
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "Try NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Got(2) NextAsync(2) Current(3) Got(3) NextAsync(3) Dispose(4) Done");
        }

        /// Covered in greater details by <see cref="CodeGenAsyncIteratorTests.TryFinally_AwaitForeachInFinally"/>
        [Fact]
        public void TestInFinally()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task Main()
    {
        try
        {
        }
        finally
        {
            await foreach (var i in new C())
            {
            }
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TestWithConversionToElement()
        {
            string source = @"
using System.Collections.Generic;
using static System.Console;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task Main()
    {
        await foreach (Element i in new C())
        {
            Write($""Got({i}) "");
        }
        Write($""Done"");
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator(0);
    }
    internal class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i;
        internal AsyncEnumerator(int start) { i = start; }
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 3);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}
class Element
{
    int i;
    public static implicit operator Element(int value) { Write($""Convert({value}) ""); return new Element(value); }
    private Element(int value) { i = value; }
    public override string ToString() => i.ToString();
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Convert(1) Got(1) NextAsync(1) Current(2) Convert(2) Got(2) NextAsync(2) Dispose(3) Done");

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("System.Collections.Generic.IAsyncEnumerator<System.Int32> System.Collections.Generic.IAsyncEnumerable<System.Int32>.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> System.Collections.Generic.IAsyncEnumerator<System.Int32>.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 System.Collections.Generic.IAsyncEnumerator<System.Int32>.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()", info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.ExplicitUserDefined, info.ElementConversion.Kind);
            Assert.Equal("Element Element.op_Implicit(System.Int32 value)", info.ElementConversion.MethodSymbol.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);
        }

        [Fact]
        public void TestWithNullableCollection()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
struct C : IAsyncEnumerable<int>
{
    static async System.Threading.Tasks.Task Main()
    {
        C? c = new C(); // non-null value
        await foreach (var i in c)
        {
            Write($""Got({i}) "");
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator();
    }
    sealed class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i = 0;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 3);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got(1) NextAsync(1) Current(2) Got(2) NextAsync(2) Dispose(3)");

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("System.Collections.Generic.IAsyncEnumerator<System.Int32> System.Collections.Generic.IAsyncEnumerable<System.Int32>.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> System.Collections.Generic.IAsyncEnumerator<System.Int32>.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 System.Collections.Generic.IAsyncEnumerator<System.Int32>.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()", info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);
        }

        [Fact]
        public void TestWithNullableCollection2()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
struct C : IAsyncEnumerable<int>
{
    static async Task Main()
    {
        C? c = null; // null value
        try
        {
            await foreach (var i in c)
            {
                Write($""UNREACHABLE"");
            }
        }
        catch (System.InvalidOperationException)
        {
            Write($""Success"");
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        throw new System.Exception();
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "Success");
        }

        [Fact]
        public void TestWithInterfaceAndDeconstruction()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var (i, j) in new C())
        {
            Write($""Got({i},{j}) "");
        }
        Write($""Done"");
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator();
    }
    sealed class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i = 0;
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 3);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Dispose({i}) "");
            await Task.Yield();
        }
    }
}
public static class Extensions
{
    public static void Deconstruct(this int i, out string x1, out int x2) { Write($""Deconstruct({i}) ""); x1 = i.ToString(); x2 = -i; }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Deconstruct(1) Got(1,-1) NextAsync(1) Current(2) Deconstruct(2) Got(2,-2) NextAsync(2) Dispose(3) Done");

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachVariableStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("System.Collections.Generic.IAsyncEnumerator<System.Int32> System.Collections.Generic.IAsyncEnumerable<System.Int32>.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> System.Collections.Generic.IAsyncEnumerator<System.Int32>.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 System.Collections.Generic.IAsyncEnumerator<System.Int32>.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()", info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);
        }

        [Fact]
        public void TestWithDeconstructionInNonAsyncMethod()
        {
            string source = @"
using System.Collections.Generic;
class C : IAsyncEnumerable<int>
{
    static void Main()
    {
        await foreach (var (i, j) in new C())
        {
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
        => throw null;
}
public static class Extensions
{
    public static void Deconstruct(this int i, out int x1, out int x2) { x1 = i; x2 = -i; }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (7,9): error CS4033: The 'await' operator can only be used within an async method. Consider marking this method with the 'async' modifier and changing its return type to 'Task'.
                //         await foreach (var (i, j) in new C())
                Diagnostic(ErrorCode.ERR_BadAwaitWithoutVoidAsyncMethod, "await").WithLocation(7, 9)
                );
        }

        [Fact]
        public void TestWithPatternAndDeconstructionOfTuple()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<(string, int)>
{
    public static async System.Threading.Tasks.Task Main()
    {
        await foreach (var (i, j) in new C())
        {
            Write($""Got({i},{j}) "");
        }
        Write($""Done"");
    }
    IAsyncEnumerator<(string, int)> IAsyncEnumerable<(string, int)>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator();
    }
    sealed class AsyncEnumerator : IAsyncEnumerator<(string, int)>
    {
        int i = 0;
        public (string, int) Current
        {
            get
            {
                Write($""Current({i}) "");
                return (i.ToString(), -i);
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 3);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Dispose({i}) "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got(1,-1) NextAsync(1) Current(2) Got(2,-2) NextAsync(2) Dispose(3) Done");

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachVariableStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("System.Collections.Generic.IAsyncEnumerator<(System.String, System.Int32)> System.Collections.Generic.IAsyncEnumerable<(System.String, System.Int32)>.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> System.Collections.Generic.IAsyncEnumerator<(System.String, System.Int32)>.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("(System.String, System.Int32) System.Collections.Generic.IAsyncEnumerator<(System.String, System.Int32)>.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()", info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("(System.String, System.Int32)", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);
        }

        [Fact]
        public void TestWithInterfaceAndDeconstruction_ManualIteration()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    static async Task Main()
    {
        var e = ((IAsyncEnumerable<int>)new C()).GetAsyncEnumerator();
        try
        {
            while (await e.MoveNextAsync())
            {
                (int i, int j) = e.Current;
                Write($""Got({i},{j}) "");
            }
        }
        finally { await e.DisposeAsync(); }

        Write($""Done"");
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator(0);
    }
    internal class AsyncEnumerator : IAsyncEnumerator<int>
    {
        int i;
        internal AsyncEnumerator(int start) { i = start; }
        public int Current
        {
            get
            {
                Write($""Current({i}) "");
                return i;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 3);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Disp"");
            await Task.Yield();
            Write($""ose({i}) "");
        }
    }
}
public static class Extensions
{
    public static void Deconstruct(this int i, out int x1, out int x2) { x1 = i; x2 = -i; }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got(1,-1) NextAsync(1) Current(2) Got(2,-2) NextAsync(2) Dispose(3) Done");
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/30257")]
        public void TestWithPatternAndObsolete_WithDisposableInterface()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    [System.Obsolete]
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    [System.Obsolete]
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        [System.Obsolete]
        public int Current { get => throw null; }
        [System.Obsolete]
        public Task<bool> MoveNextAsync() => throw null;
        [System.Obsolete]
        public ValueTask DisposeAsync() => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,15): warning CS0612: 'C.AsyncEnumerator.DisposeAsync()' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.AsyncEnumerator.DisposeAsync()").WithLocation(7, 15),
                // (7,15): warning CS0612: 'C.GetAsyncEnumerator(CancellationToken)' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(7, 15),
                // (7,15): warning CS0612: 'C.AsyncEnumerator.MoveNextAsync()' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.AsyncEnumerator.MoveNextAsync()").WithLocation(7, 15),
                // (7,15): warning CS0612: 'C.AsyncEnumerator.Current' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.AsyncEnumerator.Current").WithLocation(7, 15)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/30257")]
        public void TestWithPatternAndObsolete_WithoutDisposableInterface()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    [System.Obsolete]
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    [System.Obsolete]
    public sealed class AsyncEnumerator
    {
        [System.Obsolete]
        public int Current { get => throw null; }
        [System.Obsolete]
        public Task<bool> MoveNextAsync() => throw null;
        [System.Obsolete]
        public ValueTask DisposeAsync() => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,15): warning CS0612: 'C.AsyncEnumerator.DisposeAsync()' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.AsyncEnumerator.DisposeAsync()").WithLocation(7, 15),
                // (7,15): warning CS0612: 'C.GetAsyncEnumerator(CancellationToken)' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.GetAsyncEnumerator(System.Threading.CancellationToken)").WithLocation(7, 15),
                // (7,15): warning CS0612: 'C.AsyncEnumerator.MoveNextAsync()' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.AsyncEnumerator.MoveNextAsync()").WithLocation(7, 15),
                // (7,15): warning CS0612: 'C.AsyncEnumerator.Current' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.AsyncEnumerator.Current").WithLocation(7, 15)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/30257")]
        public void TestWithPatternAndObsolete_WithExplicitInterfaceImplementation()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
    public sealed class AsyncEnumerator : System.IAsyncDisposable
    {
        public int Current { get => throw null; }
        public Task<bool> MoveNextAsync() => throw null;
        [System.Obsolete]
        ValueTask System.IAsyncDisposable.DisposeAsync() => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyEmitDiagnostics();
        }

        [Fact]
        public void TestWithUnassignedCollection()
        {
            string source = @"
using System.Collections.Generic;
class C
{
    async System.Threading.Tasks.Task M()
    {
        C c;
        await foreach (var i in c)
        {
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token = default) => throw null;
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (8,33): error CS0165: Use of unassigned local variable 'c'
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_UseDefViolation, "c").WithArguments("c").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestInRegularForeach()
        {
            string source = @"
using System.Collections.Generic;
class C
{
    void M()
    {
        foreach (var i in new C())
        {
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable);
            comp.VerifyDiagnostics(
                // (7,27): error CS8414: foreach statement cannot operate on variables of type 'C' because 'C' does not contain a public instance or extension definition for 'GetEnumerator'. Did you mean 'await foreach'?
                //         foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_ForEachMissingMemberWrongAsync, "new C()").WithArguments("C", "GetEnumerator").WithLocation(7, 27)
                );
        }

        [Fact]
        public void TestWithGenericCollection()
        {
            string source = @"
using static System.Console;
using System.Collections.Generic;
using System.Threading.Tasks;
class Collection<T> : IAsyncEnumerable<T>
{
    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        return new AsyncEnumerator();
    }
    sealed class AsyncEnumerator : IAsyncEnumerator<T>
    {
        int i = 0;
        public T Current
        {
            get
            {
                Write($""Current({i}) "");
                return default;
            }
        }
        public async ValueTask<bool> MoveNextAsync()
        {
            Write($""NextAsync({i}) "");
            i++;
            return await Task.FromResult(i < 4);
        }
        public async ValueTask DisposeAsync()
        {
            Write($""Dispose({i}) "");
            await Task.Yield();
        }
    }
}
class C
{
    static async System.Threading.Tasks.Task Main()
    {
        await foreach (var i in new Collection<int>())
        {
            Write($""Got "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got NextAsync(1) Current(2) Got NextAsync(2) Current(3) Got NextAsync(3) Dispose(4)");

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("System.Collections.Generic.IAsyncEnumerator<System.Int32> System.Collections.Generic.IAsyncEnumerable<System.Int32>.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> System.Collections.Generic.IAsyncEnumerator<System.Int32>.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 System.Collections.Generic.IAsyncEnumerator<System.Int32>.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()", info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.True(internalInfo.NeedsDisposal);
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = ConditionalSkipReason.NativePdbRequiresDesktop)]
        public void TestWithInterfaceImplementingPattern()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;

public interface ICollection<T>
{
    IMyAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default);
}
public interface IMyAsyncEnumerator<T>
{
    T Current { get; }
    Task<bool> MoveNextAsync();
}

public class Collection<T> : ICollection<T>
{
    public IMyAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new MyAsyncEnumerator<T>();
    }
}
public sealed class MyAsyncEnumerator<T> : IMyAsyncEnumerator<T>
{
    int i = 0;
    public T Current
    {
        get
        {
            Write($""Current({i}) "");
            return default;
        }
    }
    public async Task<bool> MoveNextAsync()
    {
        Write($""NextAsync({i}) "");
        i++;
        return await Task.FromResult(i < 4);
    }
}

class C
{
    static async System.Threading.Tasks.Task Main()
    {
        ICollection<int> c = new Collection<int>();
        await foreach (var i in c)
        {
            Write($""Got "");
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            var verifier = CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got NextAsync(1) Current(2) Got NextAsync(2) Current(3) Got NextAsync(3)");

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("IMyAsyncEnumerator<System.Int32> ICollection<System.Int32>.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.Task<System.Boolean> IMyAsyncEnumerator<System.Int32>.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 IMyAsyncEnumerator<System.Int32>.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Null(info.DisposeMethod);
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);

            var memberModel = model.GetMemberModel(foreachSyntax);
            var boundNode = (BoundForEachStatement)memberModel.GetUpperBoundNode(foreachSyntax);
            ForEachEnumeratorInfo internalInfo = boundNode.EnumeratorInfoOpt;
            Assert.False(internalInfo.NeedsDisposal);

            verifier.VerifyIL("C.<Main>d__0.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size      283 (0x11b)
  .maxstack  3
  .locals init (int V_0,
                System.Threading.CancellationToken V_1,
                System.Runtime.CompilerServices.TaskAwaiter<bool> V_2,
                C.<Main>d__0 V_3,
                System.Exception V_4)
  // sequence point: <hidden>
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<Main>d__0.<>1__state""
  IL_0006:  stloc.0
  .try
  {
    // sequence point: <hidden>
    IL_0007:  ldloc.0
    IL_0008:  brfalse.s  IL_0012
    IL_000a:  br.s       IL_000c
    IL_000c:  ldloc.0
    IL_000d:  ldc.i4.1
    IL_000e:  beq.s      IL_0017
    IL_0010:  br.s       IL_001c
    IL_0012:  br         IL_00a1
    IL_0017:  br         IL_00a1
    // sequence point: {
    IL_001c:  nop
    // sequence point: ICollection<int> c = new Collection<int>();
    IL_001d:  ldarg.0
    IL_001e:  newobj     ""Collection<int>..ctor()""
    IL_0023:  stfld      ""ICollection<int> C.<Main>d__0.<c>5__1""
    // sequence point: await foreach
    IL_0028:  nop
    // sequence point: c
    IL_0029:  ldarg.0
    IL_002a:  ldarg.0
    IL_002b:  ldfld      ""ICollection<int> C.<Main>d__0.<c>5__1""
    IL_0030:  ldloca.s   V_1
    IL_0032:  initobj    ""System.Threading.CancellationToken""
    IL_0038:  ldloc.1
    IL_0039:  callvirt   ""IMyAsyncEnumerator<int> ICollection<int>.GetAsyncEnumerator(System.Threading.CancellationToken)""
    IL_003e:  stfld      ""IMyAsyncEnumerator<int> C.<Main>d__0.<>s__2""
    // sequence point: <hidden>
    IL_0043:  br.s       IL_0063
    // sequence point: var i
    IL_0045:  ldarg.0
    IL_0046:  ldarg.0
    IL_0047:  ldfld      ""IMyAsyncEnumerator<int> C.<Main>d__0.<>s__2""
    IL_004c:  callvirt   ""int IMyAsyncEnumerator<int>.Current.get""
    IL_0051:  stfld      ""int C.<Main>d__0.<i>5__3""
    // sequence point: {
    IL_0056:  nop
    // sequence point: Write($""Got "");
    IL_0057:  ldstr      ""Got ""
    IL_005c:  call       ""void System.Console.Write(string)""
    IL_0061:  nop
    // sequence point: }
    IL_0062:  nop
    // sequence point: in
    IL_0063:  ldarg.0
    IL_0064:  ldfld      ""IMyAsyncEnumerator<int> C.<Main>d__0.<>s__2""
    IL_0069:  callvirt   ""System.Threading.Tasks.Task<bool> IMyAsyncEnumerator<int>.MoveNextAsync()""
    IL_006e:  callvirt   ""System.Runtime.CompilerServices.TaskAwaiter<bool> System.Threading.Tasks.Task<bool>.GetAwaiter()""
    IL_0073:  stloc.2
    // sequence point: <hidden>
    IL_0074:  ldloca.s   V_2
    IL_0076:  call       ""bool System.Runtime.CompilerServices.TaskAwaiter<bool>.IsCompleted.get""
    IL_007b:  brtrue.s   IL_00bd
    IL_007d:  ldarg.0
    IL_007e:  ldc.i4.0
    IL_007f:  dup
    IL_0080:  stloc.0
    IL_0081:  stfld      ""int C.<Main>d__0.<>1__state""
    // async: yield
    IL_0086:  ldarg.0
    IL_0087:  ldloc.2
    IL_0088:  stfld      ""System.Runtime.CompilerServices.TaskAwaiter<bool> C.<Main>d__0.<>u__1""
    IL_008d:  ldarg.0
    IL_008e:  stloc.3
    IL_008f:  ldarg.0
    IL_0090:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
    IL_0095:  ldloca.s   V_2
    IL_0097:  ldloca.s   V_3
    IL_0099:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<bool>, C.<Main>d__0>(ref System.Runtime.CompilerServices.TaskAwaiter<bool>, ref C.<Main>d__0)""
    IL_009e:  nop
    IL_009f:  leave.s    IL_011a
    // async: resume
    IL_00a1:  ldarg.0
    IL_00a2:  ldfld      ""System.Runtime.CompilerServices.TaskAwaiter<bool> C.<Main>d__0.<>u__1""
    IL_00a7:  stloc.2
    IL_00a8:  ldarg.0
    IL_00a9:  ldflda     ""System.Runtime.CompilerServices.TaskAwaiter<bool> C.<Main>d__0.<>u__1""
    IL_00ae:  initobj    ""System.Runtime.CompilerServices.TaskAwaiter<bool>""
    IL_00b4:  ldarg.0
    IL_00b5:  ldc.i4.m1
    IL_00b6:  dup
    IL_00b7:  stloc.0
    IL_00b8:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_00bd:  ldarg.0
    IL_00be:  ldloca.s   V_2
    IL_00c0:  call       ""bool System.Runtime.CompilerServices.TaskAwaiter<bool>.GetResult()""
    IL_00c5:  stfld      ""bool C.<Main>d__0.<>s__4""
    IL_00ca:  ldarg.0
    IL_00cb:  ldfld      ""bool C.<Main>d__0.<>s__4""
    IL_00d0:  brtrue     IL_0045
    IL_00d5:  ldarg.0
    IL_00d6:  ldnull
    IL_00d7:  stfld      ""IMyAsyncEnumerator<int> C.<Main>d__0.<>s__2""
    IL_00dc:  leave.s    IL_00ff
  }
  catch System.Exception
  {
    // async: catch handler, sequence point: <hidden>
    IL_00de:  stloc.s    V_4
    IL_00e0:  ldarg.0
    IL_00e1:  ldc.i4.s   -2
    IL_00e3:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_00e8:  ldarg.0
    IL_00e9:  ldnull
    IL_00ea:  stfld      ""ICollection<int> C.<Main>d__0.<c>5__1""
    IL_00ef:  ldarg.0
    IL_00f0:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
    IL_00f5:  ldloc.s    V_4
    IL_00f7:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetException(System.Exception)""
    IL_00fc:  nop
    IL_00fd:  leave.s    IL_011a
  }
  // sequence point: }
  IL_00ff:  ldarg.0
  IL_0100:  ldc.i4.s   -2
  IL_0102:  stfld      ""int C.<Main>d__0.<>1__state""
  // sequence point: <hidden>
  IL_0107:  ldarg.0
  IL_0108:  ldnull
  IL_0109:  stfld      ""ICollection<int> C.<Main>d__0.<c>5__1""
  IL_010e:  ldarg.0
  IL_010f:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
  IL_0114:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetResult()""
  IL_0119:  nop
  IL_011a:  ret
}", sequencePoints: "C+<Main>d__0.MoveNext", source: source);
        }

        [ConditionalFact(typeof(WindowsDesktopOnly), Reason = ConditionalSkipReason.NativePdbRequiresDesktop)]
        public void TestWithInterfaceImplementingPattern_ChildImplementsDisposeAsync()
        {
            string source = @"
using static System.Console;
using System.Threading.Tasks;

public interface ICollection<T>
{
    IMyAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default);
}
public interface IMyAsyncEnumerator<T>
{
    T Current { get; }
    Task<bool> MoveNextAsync();
}

public class Collection<T> : ICollection<T>
{
    public IMyAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token = default)
    {
        return new MyAsyncEnumerator<T>();
    }
}
public sealed class MyAsyncEnumerator<T> : IMyAsyncEnumerator<T>
{
    int i = 0;
    public T Current
    {
        get
        {
            Write($""Current({i}) "");
            return default;
        }
    }
    public async Task<bool> MoveNextAsync()
    {
        Write($""NextAsync({i}) "");
        i++;
        return await Task.FromResult(i < 4);
    }
    public System.Threading.Tasks.ValueTask DisposeAsync()
        => throw null;
}

class C
{
    static async System.Threading.Tasks.Task Main()
    {
        ICollection<int> c = new Collection<int>();
        await foreach (var i in c)
        {
            Write($""Got "");
        }
    }
}";
            // DisposeAsync on implementing type is ignored, since we don't do runtime check
            var comp = CreateCompilationWithTasksExtensions(source + s_IAsyncEnumerable, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();

            var verifier = CompileAndVerify(comp, expectedOutput: "NextAsync(0) Current(1) Got NextAsync(1) Current(2) Got NextAsync(2) Current(3) Got NextAsync(3)");

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Null(info.DisposeMethod);
        }

        [Fact]
        public void GetAsyncEnumerator_CancellationTokenMustBeOptional()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token)
    {
        throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp.VerifyDiagnostics(
                // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        [WorkItem(50182, "https://github.com/dotnet/roslyn/issues/50182")]
        public void GetAsyncEnumerator_CancellationTokenMustBeOptional_OnIAsyncEnumerable()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C
{
    public static async Task M(IAsyncEnumerable<int> e)
    {
        await foreach (var i in e)
        {
        }
    }
}
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token);
    }

    public interface IAsyncEnumerator<out T> : System.IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask<bool> MoveNextAsync();
        T Current { get; }
    }
}
namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyEmitDiagnostics(
                // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'IAsyncEnumerable<int>' because 'IAsyncEnumerable<int>' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in e)
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "e").WithArguments("System.Collections.Generic.IAsyncEnumerable<int>", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        [WorkItem(50182, "https://github.com/dotnet/roslyn/issues/50182")]
        public void GetAsyncEnumerator_CancellationTokenMustBeOptional_OnIAsyncEnumerable_ImplicitImplementation()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task M(C c)
    {
        await foreach (var i in c)
        {
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(System.Threading.CancellationToken token) => throw null;
}
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token);
    }

    public interface IAsyncEnumerator<out T> : System.IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask<bool> MoveNextAsync();
        T Current { get; }
    }
}
namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyEmitDiagnostics(
                // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "c").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        [WorkItem(50182, "https://github.com/dotnet/roslyn/issues/50182")]
        public void GetAsyncEnumerator_CancellationTokenMustBeOptional_OnIAsyncEnumerable_ExplicitImplementation()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task M(C c)
    {
        await foreach (var i in c)
        {
        }
    }
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken token) => throw null;
}
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator(System.Threading.CancellationToken token);
    }

    public interface IAsyncEnumerator<out T> : System.IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask<bool> MoveNextAsync();
        T Current { get; }
    }
}
namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyEmitDiagnostics(
                // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "c").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        public void GetAsyncEnumerator_Missing()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C : IAsyncEnumerable<int>
{
    public static async Task M(C c)
    {
        await foreach (var i in c)
        {
        }
    }
}
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
    }

    public interface IAsyncEnumerator<out T> : System.IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask<bool> MoveNextAsync();
        T Current { get; }
    }
}
namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(source);
            comp.VerifyEmitDiagnostics(
                // (8,33): error CS0656: Missing compiler required member 'System.Collections.Generic.IAsyncEnumerable`1.GetAsyncEnumerator'
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "c").WithArguments("System.Collections.Generic.IAsyncEnumerable`1", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        public void GetAsyncEnumerator_WithOptionalParameter()
        {
            string source = @"
using System.Collections.Generic;
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public IAsyncEnumerator<int> GetAsyncEnumerator(int opt = 0)
    {
        throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void GetAsyncEnumerator_WithParams()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator(params int[] x)
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync"");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync");
        }

        [Fact]
        public void GetAsyncEnumerator_WithMultipleValidCandidatesWithOptionalParameters()
        {
            string source = @"
using System.Threading.Tasks;
using System.Collections.Generic;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public IEnumerator<int> GetAsyncEnumerator(int a = 0) => throw null;
    public IEnumerator<int> GetAsyncEnumerator(bool a = true) => throw null;
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                    // (8,33): warning CS0278: 'C' does not implement the 'collection' pattern. 'C.GetAsyncEnumerator(int)' is ambiguous with 'C.GetAsyncEnumerator(bool)'.
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.WRN_PatternIsAmbiguous, "new C()").WithArguments("C", "collection", "C.GetAsyncEnumerator(int)", "C.GetAsyncEnumerator(bool)").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        public void GetAsyncEnumerator_WithMultipleInvalidCandidates()
        {
            string source = @"
using System.Threading.Tasks;
using System.Collections.Generic;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public IEnumerator<int> GetAsyncEnumerator(int a) => throw null;
    public IEnumerator<int> GetAsyncEnumerator(bool a) => throw null;
}";
            var comp = CreateCompilationWithMscorlib46(source);
            comp.VerifyDiagnostics(
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
        public async Task DisposeAsync()
        {
            System.Console.Write(""DisposeAsync "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync DisposeAsync Done");
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_TwoOverloads()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
        public async Task DisposeAsync(int i = 0)
        {
            System.Console.Write(""DisposeAsync "");
            await Task.Yield();
        }
        public Task DisposeAsync(params string[] s)
            => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync DisposeAsync Done");
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_NoExtensions()
        {
            string source = @"
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
    }
}
public static class Extension
{
    public static ValueTask DisposeAsync(this C.Enumerator e) => throw null;
}";
            // extension methods do not contribute to pattern-based disposal
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync Done");
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_NoExtensions_TwoExtensions()
        {
            string source = @"
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
    }
}
public static class Extension1
{
    public static ValueTask DisposeAsync(this C.Enumerator c) => throw null;
}
public static class Extension2
{
    public static ValueTask DisposeAsync(this C.Enumerator c) => throw null;
}
";
            // extension methods do not contribute to pattern-based disposal
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync Done");
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_InstanceMethodPreferredOverInterface()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator : System.IAsyncDisposable
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
        ValueTask System.IAsyncDisposable.DisposeAsync() => throw null;
        public async ValueTask DisposeAsync()
        {
            System.Console.Write(""DisposeAsync "");
            await Task.Yield();
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync DisposeAsync Done");
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_ReturnsVoid()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator()
        => throw null;
    public sealed class Enumerator
    {
        public Task<bool> MoveNextAsync()
            => throw null;
        public int Current
        {
            get => throw null;
        }
        public void DisposeAsync()
            => throw null;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp.VerifyDiagnostics(
                // (7,33): error CS4008: Cannot await 'void'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadAwaitArgVoidCall, "new C()").WithLocation(7, 33)
                );
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_ReturnsInt()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
        public int DisposeAsync()
        {
            throw null;
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable });
            comp.VerifyDiagnostics(
                // (7,33): error CS1061: 'int' does not contain a definition for 'GetAwaiter' and no accessible extension method 'GetAwaiter' accepting a first argument of type 'int' could be found (are you missing a using directive or an assembly reference?)
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "new C()").WithArguments("int", "GetAwaiter").WithLocation(7, 33)
                );
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_ReturnsAwaitable()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
        public Awaitable DisposeAsync()
        {
            System.Console.Write(""DisposeAsync "");
            return new Awaitable();
        }
    }
}

public class Awaitable
{
    public Awaiter GetAwaiter() { return new Awaiter(); }
}
public class Awaiter : System.Runtime.CompilerServices.INotifyCompletion
{
    public bool IsCompleted { get { return true; } }
    public bool GetResult() { return true; }
    public void OnCompleted(System.Action continuation) { }
}
";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync DisposeAsync Done");
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_ReturnsTask()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
        public async Task DisposeAsync()
        {
            System.Console.Write(""DisposeAsync "");
            await Task.Yield();
        }
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync DisposeAsync Done");
        }

        [Fact]
        [WorkItem(32316, "https://github.com/dotnet/roslyn/issues/32316")]
        public void PatternBasedDisposal_ReturnsTaskOfInt()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
        public async Task<int> DisposeAsync()
        {
            System.Console.Write(""DisposeAsync "");
            await Task.Yield();
            return 1;
        }
    }
}
";
            // it's okay to await `Task<int>` even if we don't care about the result
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync DisposeAsync Done");
        }

        [Fact]
        public void PatternBasedDisposal_WithOptionalParameter()
        {
            string source = @"
using System.Threading.Tasks;
class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
        }
        System.Console.Write(""Done"");
    }
    public Enumerator GetAsyncEnumerator()
    {
        return new Enumerator();
    }
    public sealed class Enumerator
    {
        public async Task<bool> MoveNextAsync()
        {
            System.Console.Write(""MoveNextAsync "");
            await Task.Yield();
            return false;
        }
        public int Current
        {
            get => throw null;
        }
        public async Task<int> DisposeAsync(int i = 1)
        {
            System.Console.Write($""DisposeAsync {i} "");
            await Task.Yield();
            return 1;
        }
    }
}
";
            // it's okay to await `Task<int>` even if we don't care about the result
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "MoveNextAsync DisposeAsync 1 Done");
        }

        [Fact]
        [WorkItem(30956, "https://github.com/dotnet/roslyn/issues/30956")]
        public void GetAwaiterBoxingConversion()
        {
            var source =
@"using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

interface I1 { }
interface I2 { }
struct StructAwaitable1 : I1 { }
struct StructAwaitable2 : I2 { }

class Enumerable
{
    public Enumerator GetAsyncEnumerator() => new Enumerator();
    internal class Enumerator
    {
        public object Current => null;
        public StructAwaitable1 MoveNextAsync() => new StructAwaitable1();
        public StructAwaitable2 DisposeAsync() => new StructAwaitable2();
    }
}

static class Extensions
{
    internal static TaskAwaiter<bool> GetAwaiter(this I1 x)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        Console.Write(x);
        return Task.FromResult(false).GetAwaiter();
    }
    internal static TaskAwaiter GetAwaiter(this I2 x)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        Console.Write(x);
        return Task.CompletedTask.GetAwaiter();
    }
}

class Program
{
    static async Task Main()
    {
        await foreach (var o in new Enumerable())
        {
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, expectedOutput: "StructAwaitable1StructAwaitable2");
        }

        [Fact]
        public void TestInvalidForeachOnConstantNullObject()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in (object)null)
        {
            Console.Write(i);
        }
    }
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'object' because 'object' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in (object)null)
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "(object)null").WithArguments("object", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestConstantNullObjectImplementingIEnumerable()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in (IAsyncEnumerable<int>)null)
        {
            Console.Write(i);
        }
    }
}";
            CreateCompilationWithTasksExtensions(new[] { source, AsyncStreamsTypes }, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (9,33): error CS0186: Use of null is not valid in this context
                    //         await foreach (var i in (IAsyncEnumerable<int>)null)
                    Diagnostic(ErrorCode.ERR_NullNotValid, "(IAsyncEnumerable<int>)null").WithLocation(9, 33)
                    );
        }

        [Fact]
        public void TestConstantNullObjectWithGetAsyncEnumeratorPattern()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in (C)null)
        {
            Console.Write(i);
        }
    }

    public IAsyncEnumerator<int> GetAsyncEnumerator() => throw null;
}";
            CreateCompilationWithTasksExtensions(new[] { source, AsyncStreamsTypes }, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (9,33): error CS0186: Use of null is not valid in this context
                    //         await foreach (var i in (C)null)
                    Diagnostic(ErrorCode.ERR_NullNotValid, "(C)null").WithLocation(9, 33)
                    );
        }

        [Fact]
        public void TestConstantNullableImplementingIEnumerable()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
public struct C : IAsyncEnumerable<int>
{
    public static async Task Main()
    {
        await foreach (var i in (C?)null)
        {
            Console.Write(i);
        }
    }

    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken) => throw null;
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, AsyncStreamsTypes }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp);
        }

        [Fact]
        public void TestConstantNullableWithGetAsyncEnumeratorPattern()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
public struct C
{
    public static async Task Main()
    {
        await foreach (var i in (C?)null)
        {
            Console.Write(i);
        }
    }

    public IAsyncEnumerator<int> GetAsyncEnumerator() => throw null;
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, AsyncStreamsTypes }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp);
        }

        [Fact]
        public void TestForeachNullLiteral()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in null)
        {
            Console.Write(i);
        }
    }
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS0186: Use of null is not valid in this context
                    //         await foreach (var i in null)
                    Diagnostic(ErrorCode.ERR_NullNotValid, "null").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestForeachDefaultLiteral()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in default)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this object self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS8716: There is no target type for the default literal.
                    //         await foreach (var i in default)
                    Diagnostic(ErrorCode.ERR_DefaultLiteralNoTargetType, "default").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensions()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");

            var tree = comp.SyntaxTrees.Single();
            var model = (SyntaxTreeSemanticModel)comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("C.Enumerator Extensions.GetAsyncEnumerator(this C self)", info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.Task<System.Boolean> C.Enumerator.MoveNextAsync()", info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 C.Enumerator.Current { get; private set; }", info.CurrentProperty.ToTestDisplayString());
            Assert.Null(info.DisposeMethod);
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
            Assert.Equal(ConversionKind.Identity, info.ElementConversion.Kind);
            Assert.Equal(ConversionKind.Identity, info.CurrentConversion.Kind);
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithUpcast()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this object self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnDefaultObject()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in default(object))
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this object self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithStructEnumerator()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public struct Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithUserDefinedImplicitConversion()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static implicit operator int(C c) => 0;

    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this int self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (10,33): error CS1929: 'C' does not contain a definition for 'GetAsyncEnumerator' and the best extension method overload 'Extensions.GetAsyncEnumerator(int)' requires a receiver of type 'int'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadInstanceArgType, "new C()").WithArguments("C", "GetAsyncEnumerator", "Extensions.GetAsyncEnumerator(int)", "int").WithLocation(10, 33),
                // (10,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(10, 33)
                );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithNullableValueTypeConversion()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in 1)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this int? self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS1929: 'int' does not contain a definition for 'GetAsyncEnumerator' and the best extension method overload 'Extensions.GetAsyncEnumerator(int?)' requires a receiver of type 'int?'
                    //         await foreach (var i in 1)
                    Diagnostic(ErrorCode.ERR_BadInstanceArgType, "1").WithArguments("int", "GetAsyncEnumerator", "Extensions.GetAsyncEnumerator(int?)", "int?").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'int' because 'int' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in 1)
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "1").WithArguments("int", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithUnboxingConversion()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new object())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this int self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS1929: 'object' does not contain a definition for 'GetAsyncEnumerator' and the best extension method overload 'Extensions.GetAsyncEnumerator(int)' requires a receiver of type 'int'
                    //         await foreach (var i in new object())
                    Diagnostic(ErrorCode.ERR_BadInstanceArgType, "new object()").WithArguments("object", "GetAsyncEnumerator", "Extensions.GetAsyncEnumerator(int)", "int").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'object' because 'object' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new object())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new object()").WithArguments("object", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithNullableUnwrapping()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in (int?)1)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this int self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS1929: 'int?' does not contain a definition for 'GetAsyncEnumerator' and the best extension method overload 'Extensions.GetAsyncEnumerator(int)' requires a receiver of type 'int'
                    //         await foreach (var i in (int?)1)
                    Diagnostic(ErrorCode.ERR_BadInstanceArgType, "(int?)1").WithArguments("int?", "GetAsyncEnumerator", "Extensions.GetAsyncEnumerator(int)", "int").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'int?' because 'int?' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in (int?)1)
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "(int?)1").WithArguments("int?", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithZeroToEnumConversion()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public enum E { Default = 0 }
public class C
{
    public static async Task Main()
    {
        await foreach (var i in 0)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this E self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (9,33): error CS1929: 'int' does not contain a definition for 'GetAsyncEnumerator' and the best extension method overload 'Extensions.GetAsyncEnumerator(E)' requires a receiver of type 'E'
                    //         await foreach (var i in 0)
                    Diagnostic(ErrorCode.ERR_BadInstanceArgType, "0").WithArguments("int", "GetAsyncEnumerator", "Extensions.GetAsyncEnumerator(E)", "E").WithLocation(9, 33),
                    // (9,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'int' because 'int' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in 0)
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "0").WithArguments("int", "GetAsyncEnumerator").WithLocation(9, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithUnconstrainedGenericConversion()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await Inner(1);

        async Task Inner<T>(T t)
        {
            await foreach (var i in t)
            {
                Console.Write(i);
            }
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this object self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithConstrainedGenericConversion()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await Inner(1);

        async Task Inner<T>(T t) where T : IConvertible
        {
            await foreach (var i in t)
            {
                Console.Write(i);
            }
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this IConvertible self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithFormattableStringConversion1()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in $"" "")
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this FormattableString self) => throw null;
    public static C.Enumerator GetAsyncEnumerator(this object self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithFormattableStringConversion2()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in $"" "")
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this string self) => new C.Enumerator();
    public static C.Enumerator GetAsyncEnumerator(this FormattableString self) => throw null;
    public static C.Enumerator GetAsyncEnumerator(this object self) => throw null;
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithFormattableStringConversion3()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in $"" "")
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this FormattableString self) => throw null;
}";
            CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS1929: 'string' does not contain a definition for 'GetAsyncEnumerator' and the best extension method overload 'Extensions.GetAsyncEnumerator(FormattableString)' requires a receiver of type 'FormattableString'
                    //         await foreach (var i in $" ")
                    Diagnostic(ErrorCode.ERR_BadInstanceArgType, @"$"" """).WithArguments("string", "GetAsyncEnumerator", "Extensions.GetAsyncEnumerator(System.FormattableString)", "System.FormattableString").WithLocation(8, 33),
                    // (8,33): error CS8415: Asynchronous foreach statement cannot operate on variables of type 'string' because 'string' does not contain a public instance or extension definition for 'GetAsyncEnumerator'. Did you mean 'foreach' rather than 'await foreach'?
                    //         await foreach (var i in $" ")
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMemberWrongAsync, @"$"" """).WithArguments("string", "GetAsyncEnumerator").WithLocation(8, 33));

        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithDelegateConversion()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in () => 42)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this Func<int> self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS0446: Foreach cannot operate on a 'lambda expression'. Did you intend to invoke the 'lambda expression'?
                    //         await foreach (var i in () => 42)
                    Diagnostic(ErrorCode.ERR_AnonMethGrpInForEach, "() => 42").WithArguments("lambda expression").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithBoxing()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public struct C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this object self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnInterface()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public interface I {}
public class C : I
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this I self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnDelegate()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in (Func<int>)(() => 42))
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this Func<int> self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnEnum()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public enum E { Default }
public class C
{
    public static async Task Main()
    {
        await foreach (var i in E.Default)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this E self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnNullable()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in (int?)null)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this int? self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnConstantNullObject()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in (object)null)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this object self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnTypeParameter()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new object())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator<T>(this T self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternOnRange()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in 1..4)
        {
            Console.Write(i);
        }
    }
}
public static class Extensions
{
    public static async IAsyncEnumerator<int> GetAsyncEnumerator(this Range range)
    {
        await Task.Yield();
        for(var i = range.Start.Value; i < range.End.Value; i++)
        {
            yield return i;
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(
                new[] { source, TestSources.Index, TestSources.Range, AsyncStreamsTypes },
                options: TestOptions.DebugExe,
                parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnTuple()
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
public struct C
{
    public static async Task Main()
    {
        await foreach (var i in (1, 2, 3))
        {
            Console.Write(i);
        }
    }
}
public static class Extensions
{
    public static async IAsyncEnumerator<T> GetAsyncEnumerator<T>(this (T first, T second, T third) self)
    {
        await Task.Yield();
        yield return self.first;
        yield return self.second;
        yield return self.third;
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, AsyncStreamsTypes }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsOnTupleWithNestedConversions()
        {
            var source = @"
using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public struct C
{
    public static async Task Main()
    {
        await foreach (var (a, b) in (new[] { 1, 2, 3 }, new List<decimal>{ 0.1m, 0.2m, 0.3m }))
        {
            Console.WriteLine((a + b).ToString(CultureInfo.InvariantCulture));
        }
    }
}
public static class Extensions
{
    public static async IAsyncEnumerator<(T1, T2)> GetAsyncEnumerator<T1, T2>(this (IEnumerable<T1> first, IEnumerable<T2> second) self)
    {
        await Task.Yield();
        foreach(var pair in self.first.Zip(self.second, (a,b) => (a,b)))
        {
            yield return pair;
        }
    }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, AsyncStreamsTypes }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: @"1.1
2.2
3.3");
        }

        [Fact]
        public void TestMoveNextAsyncPatternViaExtensions1()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
    public static bool MoveNext(this C.Enumerator e) => false;
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (8,33): error CS0117: 'C.Enumerator' does not contain a definition for 'MoveNextAsync'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("C.Enumerator", "MoveNextAsync").WithLocation(8, 33),
                // (8,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'Extensions.GetAsyncEnumerator(C)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "Extensions.GetAsyncEnumerator(C)").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestMoveNextAsyncPatternViaExtensions2()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        
    }

    public C.Enumerator GetAsyncEnumerator() => new C.Enumerator();
}
public static class Extensions
{
    public static bool MoveNext(this C.Enumerator e) => false;
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (8,33): error CS0117: 'C.Enumerator' does not contain a definition for 'MoveNextAsync'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("C.Enumerator", "MoveNextAsync").WithLocation(8, 33),
                // (8,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'C.GetAsyncEnumerator()' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "C.GetAsyncEnumerator()").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestPreferAsyncEnumeratorPatternFromInstanceThanViaExtension()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator1
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
    public sealed class Enumerator2
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => throw null;
    }

    public C.Enumerator1 GetAsyncEnumerator() => new C.Enumerator1();
}

public static class Extensions
{
    public static C.Enumerator2 GetAsyncEnumerator(this C self) => throw null;
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestPreferAsyncEnumeratorPatternFromInstanceThanViaExtensionEvenWhenInvalid()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator1
    {
    }
    public sealed class Enumerator2
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => throw null;
    }

    public C.Enumerator1 GetAsyncEnumerator() => throw null;
}

public static class Extensions
{
    public static C.Enumerator2 GetAsyncEnumerator(this C self) => throw null;
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (8,33): error CS0117: 'C.Enumerator1' does not contain a definition for 'Current'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("C.Enumerator1", "Current").WithLocation(8, 33),
                // (8,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator1' of 'C.GetAsyncEnumerator()' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator1", "C.GetAsyncEnumerator()").WithLocation(8, 33));
        }

        [Fact]
        public void TestPreferAsyncEnumeratorPatternFromIAsyncEnumerableInterfaceThanViaExtension()
        {
            string source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class C : IAsyncEnumerable<int>
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    
    public sealed class Enumerator1 : IAsyncEnumerator<int>
    {
        public int Current { get; private set; }

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(Current++ != 3);

        public ValueTask DisposeAsync() => new ValueTask();
    }

    public sealed class Enumerator2
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => throw null;
    }

    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken cancellationToken) => new C.Enumerator1();
}

public static class Extensions
{
    public static C.Enumerator2 GetAsyncEnumerator(this C self) => throw null;
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestCannotUseExtensionGetAsyncEnumeratorOnDynamic()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public class C
{
    public static async Task Main()
    {
        await foreach (var i in (dynamic)new C())
        {
            Console.Write(i);
        }
    }

    public sealed class Enumerator2
    {
        public int Current { get; private set; }
        public bool MoveNext() => throw null;
    }
}

public static class Extensions
{
    public static C.Enumerator2 GetAsyncEnumerator(this C self) => throw null;
}";
            CreateCompilationWithCSharp(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (9,33): error CS8416: Cannot use a collection of dynamic type in an asynchronous foreach
                    //         await foreach (var i in (dynamic)new C())
                    Diagnostic(ErrorCode.ERR_BadDynamicAwaitForEach, "(dynamic)new C()").WithLocation(9, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensions()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}
public static class Extensions2
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (8,33): warning CS0278: 'C' does not implement the 'collection' pattern. 'Extensions1.GetAsyncEnumerator(C)' is ambiguous with 'Extensions2.GetAsyncEnumerator(C)'.
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_PatternIsAmbiguous, "new C()").WithArguments("C", "collection", "Extensions1.GetAsyncEnumerator(C)", "Extensions2.GetAsyncEnumerator(C)").WithLocation(8, 33),
                // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsWhenOneHasCorrectPattern()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static int GetAsyncEnumerator(this C self) => 42;
}
public static class Extensions2
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): warning CS0278: 'C' does not implement the 'collection' pattern. 'Extensions1.GetAsyncEnumerator(C)' is ambiguous with 'Extensions2.GetAsyncEnumerator(C)'.
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.WRN_PatternIsAmbiguous, "new C()").WithArguments("C", "collection", "Extensions1.GetAsyncEnumerator(C)", "Extensions2.GetAsyncEnumerator(C)").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsWhenNeitherHasCorrectPattern()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static int GetAsyncEnumerator(this C self) => 42;
}
public static class Extensions2
{
    public static bool GetAsyncEnumerator(this C self) => true;
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): warning CS0278: 'C' does not implement the 'collection' pattern. 'Extensions1.GetAsyncEnumerator(C)' is ambiguous with 'Extensions2.GetAsyncEnumerator(C)'.
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.WRN_PatternIsAmbiguous, "new C()").WithArguments("C", "collection", "Extensions1.GetAsyncEnumerator(C)", "Extensions2.GetAsyncEnumerator(C)").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsWhenOneHasCorrectNumberOfParameters()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static C.Enumerator GetAsyncEnumerator(this C self, int _) => throw null;
}
public static class Extensions2
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsWhenNeitherHasCorrectNumberOfParameters()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static C.Enumerator GetAsyncEnumerator(this C self, int _) => new C.Enumerator();
}
public static class Extensions2
{
    public static C.Enumerator GetAsyncEnumerator(this C self, bool _) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS1501: No overload for method 'GetAsyncEnumerator' takes 0 arguments
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_BadArgCount, "new C()").WithArguments("GetAsyncEnumerator", "0").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsOnDifferentInterfaces()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public interface I1 {}
public interface I2 {}

public class C : I1, I2
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static C.Enumerator GetAsyncEnumerator(this I1 self) => new C.Enumerator();
}
public static class Extensions2
{
    public static C.Enumerator GetAsyncEnumerator(this I2 self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (12,33): warning CS0278: 'C' does not implement the 'collection' pattern. 'Extensions1.GetAsyncEnumerator(I1)' is ambiguous with 'Extensions2.GetAsyncEnumerator(I2)'.
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.WRN_PatternIsAmbiguous, "new C()").WithArguments("C", "collection", "Extensions1.GetAsyncEnumerator(I1)", "Extensions2.GetAsyncEnumerator(I2)").WithLocation(12, 33),
                    // (12,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(12, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsWithMostSpecificReceiver()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public interface I {}
public class C : I
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static C.Enumerator GetAsyncEnumerator(this I self) => throw null;
}
public static class Extensions2
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsWithMostSpecificReceiverWhenMostSpecificReceiverDoesntImplementPattern()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public interface I {}
public class C : I
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static C.Enumerator GetAsyncEnumerator(this I self) => throw null;
}
public static class Extensions2
{ 
    public static int GetAsyncEnumerator(this C self) => 42;
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (10,33): error CS0117: 'int' does not contain a definition for 'Current'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("int", "Current").WithLocation(10, 33),
                    // (10,33): error CS8412: Asynchronous foreach requires that the return type 'int' of 'Extensions2.GetAsyncEnumerator(C)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("int", "Extensions2.GetAsyncEnumerator(C)").WithLocation(10, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsWhenOneHasOptionalParams()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}
public static class Extensions2
{
    public static C.Enumerator GetAsyncEnumerator(this C self, int a = 0) => throw null;
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAmbiguousExtensionsWhenOneHasFewerOptionalParams()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions1
{
    public static C.Enumerator GetAsyncEnumerator(this C self, int a = 0, int b = 1) => new C.Enumerator();
}
public static class Extensions2
{
    public static C.Enumerator GetAsyncEnumerator(this C self, int a = 0) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): warning CS0278: 'C' does not implement the 'collection' pattern. 'Extensions1.GetAsyncEnumerator(C, int, int)' is ambiguous with 'Extensions2.GetAsyncEnumerator(C, int)'.
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.WRN_PatternIsAmbiguous, "new C()").WithArguments("C", "collection", "Extensions1.GetAsyncEnumerator(C, int, int)", "Extensions2.GetAsyncEnumerator(C, int)").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithOptionalParameter()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public Enumerator(int start) => Current = start;
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self, int x = 1) => new C.Enumerator(x);
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "23");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithArgList()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self, __arglist) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                 .VerifyDiagnostics(
                    // (8,33): error CS7036: There is no argument given that corresponds to the required parameter '__arglist' of 'Extensions.GetAsyncEnumerator(C, __arglist)'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "new C()").WithArguments("__arglist", "Extensions.GetAsyncEnumerator(C, __arglist)").WithLocation(8, 33),
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithParams()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public Enumerator(int[] arr) => Current = arr.Length;
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self, params int[] x) => new C.Enumerator(x);
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaRefExtensionOnNonAssignableVariable()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public struct C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public struct Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this ref C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (8,33): error CS1510: A ref or out value must be an assignable variable
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_RefLvalueExpected, "new C()").WithLocation(8, 33));
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaRefExtensionOnAssignableVariable()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public struct C
{
    public static async Task Main()
    {
        var c = new C();
        await foreach (var i in c)
        {
            Console.Write(i);
        }
    }
    public struct Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this ref C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (9,33): error CS1510: A ref or out value must be an assignable variable
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_RefLvalueExpected, "c").WithLocation(9, 33)
                );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaOutExtension()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public struct C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public struct Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this out C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (8,33): error CS1620: Argument 1 must be passed with the 'out' keyword
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_BadArgRef, "new C()").WithArguments("1", "out").WithLocation(8, 33),
                // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33),
                // (21,56): error CS8328:  The parameter modifier 'out' cannot be used with 'this'
                //     public static C.Enumerator GetAsyncEnumerator(this out C self) => new C.Enumerator();
                Diagnostic(ErrorCode.ERR_BadParameterModifiers, "out").WithArguments("out", "this").WithLocation(21, 56));
        }

        [Theory]
        [InlineData("in", LanguageVersion.CSharp9)]
        [InlineData("ref readonly", LanguageVersion.Preview)]
        public void TestGetAsyncEnumeratorPatternViaInExtensionOnNonAssignableVariable(string modifier, LanguageVersion languageVersion)
        {
            string source = @"
using System;
using System.Threading.Tasks;
public struct C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public struct Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this " + modifier + @" C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular.WithLanguageVersion(languageVersion));
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Theory]
        [InlineData("in", LanguageVersion.CSharp9)]
        [InlineData("ref readonly", LanguageVersion.Preview)]
        public void TestGetAsyncEnumeratorPatternViaInExtensionOnAssignableVariable(string modifier, LanguageVersion languageVersion)
        {
            string source = @"
using System;
using System.Threading.Tasks;
public struct C
{
    public static async Task Main()
    {
        var c = new C();
        await foreach (var i in c)
        {
            Console.Write(i);
        }
    }
    public struct Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this " + modifier + @" C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular.WithLanguageVersion(languageVersion));
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsCSharp8()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (8,33): error CS8400: Feature 'extension GetAsyncEnumerator' is not available in C# 8.0. Please use language version 9.0 or greater.
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion8, "new C()").WithArguments("extension GetAsyncEnumerator", "9.0").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaInternalExtensions()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    internal static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionInInternalClass()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
internal static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithInvalidEnumerator()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
    }
}
internal static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS0117: 'C.Enumerator' does not contain a definition for 'MoveNextAsync'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("C.Enumerator", "MoveNextAsync").WithLocation(8, 33),
                    // (8,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator' of 'Extensions.GetAsyncEnumerator(C)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator", "Extensions.GetAsyncEnumerator(C)").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithInstanceGetAsyncEnumeratorReturningTypeWhichDoesntMatchPattern()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }

    public sealed class Enumerator1
    {
        public int Current { get; private set; }
    }

    public sealed class Enumerator2
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }

    public Enumerator1 GetAsyncEnumerator() => new Enumerator1();
}
internal static class Extensions
{
    public static C.Enumerator2 GetAsyncEnumerator(this C self) => new C.Enumerator2();
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS0117: 'C.Enumerator1' does not contain a definition for 'MoveNextAsync'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("C.Enumerator1", "MoveNextAsync").WithLocation(8, 33),
                    // (8,33): error CS8412: Asynchronous foreach requires that the return type 'C.Enumerator1' of 'C.GetAsyncEnumerator()' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("C.Enumerator1", "C.GetAsyncEnumerator()").WithLocation(8, 33)
                );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithInternalInstanceGetAsyncEnumerator()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }

    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }

    internal Enumerator GetAsyncEnumerator() => throw null;
}
internal static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (8,33): warning CS0279: 'C' does not implement the 'async streams' pattern. 'C.GetAsyncEnumerator()' is not a public instance or extension method.
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_PatternNotPublicOrNotInstance, "new C()").WithArguments("C", "async streams", "C.GetAsyncEnumerator()").WithLocation(8, 33)
                );
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithInstanceGetAsyncEnumeratorWithTooManyParameters()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }

    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }

    internal Enumerator GetAsyncEnumerator(int a) => throw null;
}
internal static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithStaticGetAsyncEnumeratorDeclaredInType()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }

    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }

    public static Enumerator GetAsyncEnumerator() => throw null;
}
internal static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestAwaitForEachViaExtensionImplicitImplementationOfIAsyncDisposableStruct()
        {
            var source = @"
using System;
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var x in new C())
        {
            Console.Write(x);
        }
    }
}

static class Extensions
{
    public static Enumerator GetAsyncEnumerator(this C _) => new Enumerator();
}

struct Enumerator : IAsyncDisposable
{
    public int Current { get; private set; }
    public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    public ValueTask DisposeAsync() { Console.Write(""Disposed""); return new ValueTask(); }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: @"123Disposed");
        }

        [Fact]
        public void TestAwaitForEachViaExtensionExplicitlyDisposableStruct()
        {
            var source = @"
using System;
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var x in new C())
        {
            Console.Write(x);
        }
    }
}

static class Extensions
{
    public static Enumerator GetAsyncEnumerator(this C _) => new Enumerator();
}

struct Enumerator : IAsyncDisposable
{
    public int Current { get; private set; }
    public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    ValueTask IAsyncDisposable.DisposeAsync() { Console.Write(""Disposed""); return new ValueTask(); }
}";

            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: @"123Disposed");
        }

        [Fact]
        public void TestAwaitForEachViaExtensionAsyncDisposeStruct()
        {
            var source = @"
using System;
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await foreach (var x in new C())
        {
            Console.Write(x);
        }
    }
}

static class Extensions
{
    public static Enumerator GetAsyncEnumerator(this C _) => new Enumerator();
}

struct Enumerator
{
    public int Current { get; private set; }
    public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    public ValueTask DisposeAsync() { Console.Write(""Disposed""); return new ValueTask(); }
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: @"123Disposed");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionsWithTaskLikeTypeMoveNext()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestWithObsoletePatternMethodsViaExtension()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        [Obsolete]
        public int Current { get; private set; }
        [Obsolete]
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
        [Obsolete]
        public Task DisposeAsync() { Console.Write(""Disposed""); return Task.CompletedTask; }
    }
}
[Obsolete]
public static class Extensions
{
    [Obsolete]
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (8,15): warning CS0612: 'C.Enumerator.DisposeAsync()' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.Enumerator.DisposeAsync()").WithLocation(8, 15),
                // (8,15): warning CS0612: 'Extensions.GetAsyncEnumerator(C)' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("Extensions.GetAsyncEnumerator(C)").WithLocation(8, 15),
                // (8,15): warning CS0612: 'C.Enumerator.MoveNextAsync()' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.Enumerator.MoveNextAsync()").WithLocation(8, 15),
                // (8,15): warning CS0612: 'C.Enumerator.Current' is obsolete
                //         await foreach (var i in new C())
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "foreach").WithArguments("C.Enumerator.Current").WithLocation(8, 15)
                );
            CompileAndVerify(comp, expectedOutput: "123Disposed");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaImportedExtensions()
        {
            string source = @"
using System;
using System.Threading.Tasks;
using N;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
namespace N
{
    public static class Extensions
    {
        public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
    }
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaUnimportedExtensions()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
namespace N
{
    public static class Extensions
    {
        public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
    }
}";
            CreateCompilationWithMscorlib46(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestWithPatternGetAsyncEnumeratorViaExtensionOnUnassignedCollection()
        {
            string source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        C c;
        await foreach (var i in c)
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (9,33): error CS0165: Use of unassigned local variable 'c'
                //         await foreach (var i in c)
                Diagnostic(ErrorCode.ERR_UseDefViolation, "c").WithArguments("c").WithLocation(9, 33)
                );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaValidExtensionInClosestNamespaceInvalidInFurtherNamespace1()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using N1.N2.N3;

namespace N1
{
    public static class Extensions
    {
        public static int GetAsyncEnumerator(this C self) => throw null;
    }

    namespace N2
    {
        public static class Extensions
        {
            public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
        }

        namespace N3
        {
            using N2;
            public class C
            {
                public static async Task Main()
                {
                    await foreach (var i in new C())
                    {
                        Console.Write(i);
                    }
                }
                public sealed class Enumerator
                {
                    public int Current { get; private set; }
                    public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
                }
            }
        }
    }
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaValidExtensionInClosestNamespaceInvalidInFurtherNamespace2()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using N1;
using N3;

namespace N1
{
    using N2;
    public class C
    {
        public static async Task Main()
        {
            await foreach (var i in new C())
            {
                Console.Write(i);
            }
        }
        public sealed class Enumerator
        {
            public int Current { get; private set; }
            public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
        }
    }
}

namespace N2
{
    public static class Extensions
    {
        public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
    }
}

namespace N3
{
    public static class Extensions
    {
        public static int GetAsyncEnumerator(this C self) => throw null;
    }
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics(
                // (5,1): hidden CS8019: Unnecessary using directive.
                // using N3;
                Diagnostic(ErrorCode.HDN_UnusedUsingDirective, "using N3;").WithLocation(5, 1));
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaInvalidExtensionInClosestNamespaceValidInFurtherNamespace1()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using N1.N2.N3;

namespace N1
{
    public static class Extensions
    {
        public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
    }

    namespace N2
    {
        public static class Extensions
        {
            public static int GetAsyncEnumerator(this C self) => throw null;
        }

        namespace N3
        {
            using N2;
            public class C
            {
                public static async Task Main()
                {
                    await foreach (var i in new C())
                    {
                        Console.Write(i);
                    }
                }
                public sealed class Enumerator
                {
                    public int Current { get; private set; }
                    public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
                }
            }
        }
    }
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (27,45): error CS0117: 'int' does not contain a definition for 'Current'
                    //                     await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("int", "Current").WithLocation(27, 45),
                    // (27,45): error CS8412: Asynchronous foreach requires that the return type 'int' of 'Extensions.GetAsyncEnumerator(C)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                    //                     await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("int", "N1.N2.Extensions.GetAsyncEnumerator(N1.N2.N3.C)").WithLocation(27, 45));
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaInvalidExtensionInClosestNamespaceValidInFurtherNamespace2()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using N1;
using N2;

namespace N1
{
    using N3;
    public class C
    {
        public static async Task Main()
        {
            await foreach (var i in new C())
            {
                Console.Write(i);
            }
        }
        public sealed class Enumerator
        {
            public int Current { get; private set; }
            public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
        }
    }
}

namespace N2
{
    public static class Extensions
    {
        public static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
    }
}

namespace N3
{
    public static class Extensions
    {
        public static int GetAsyncEnumerator(this C self) => throw null;
    }
}";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (5,1): hidden CS8019: Unnecessary using directive.
                    // using N2;
                    Diagnostic(ErrorCode.HDN_UnusedUsingDirective, "using N2;").WithLocation(5, 1),
                    // (14,37): error CS0117: 'int' does not contain a definition for 'Current'
                    //             await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_NoSuchMember, "new C()").WithArguments("int", "Current").WithLocation(14, 37),
                    // (14,37): error CS8412: Asynchronous foreach requires that the return type 'int' of 'Extensions.GetAsyncEnumerator(C)' must have a suitable public 'MoveNextAsync' method and public 'Current' property
                    //             await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_BadGetAsyncEnumerator, "new C()").WithArguments("int", "N3.Extensions.GetAsyncEnumerator(N1.C)").WithLocation(14, 37));
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAccessiblePrivateExtension()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public static class Program
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }

    private static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}

public class C
{
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaAccessiblePrivateExtensionInNestedClass()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public static class Program
{
    public static class Inner
    {
        public static async Task Main()
        {
            await foreach (var i in new C())
            {
                Console.Write(i);
            }
        }
    }

    private static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}

public class C
{
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaInaccessiblePrivateExtension()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public sealed class Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    private static C.Enumerator GetAsyncEnumerator(this C self) => new C.Enumerator();
}
";
            CreateCompilation(source, parseOptions: TestOptions.Regular9)
                .VerifyDiagnostics(
                    // (8,33): error CS8411: Asynchronous foreach statement cannot operate on variables of type 'C' because 'C' does not contain a suitable public instance or extension definition for 'GetAsyncEnumerator'
                    //         await foreach (var i in new C())
                    Diagnostic(ErrorCode.ERR_AwaitForEachMissingMember, "new C()").WithArguments("C", "GetAsyncEnumerator").WithLocation(8, 33)
                    );
        }

        [Fact]
        public void TestGetAsyncEnumeratorPatternViaExtensionWithRefReturn()
        {
            var source = @"
using System;
using System.Threading.Tasks;
public class C
{
    public static async Task Main()
    {
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
        
        await foreach (var i in new C())
        {
            Console.Write(i);
        }
    }
    public struct Enumerator
    {
        public int Current { get; private set; }
        public Task<bool> MoveNextAsync() => Task.FromResult(Current++ != 3);
    }
}
public static class Extensions
{
    public static C.Enumerator Instance = new C.Enumerator();
    public static ref C.Enumerator GetAsyncEnumerator(this C self) => ref Instance;
}";
            var comp = CreateCompilationWithMscorlib46(source, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular9);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "123123");
        }

        [Theory, WorkItem(59955, "https://github.com/dotnet/roslyn/issues/59955")]
        [InlineData(true)]
        [InlineData(false)]
        public void DisposePatternPreferredOverIAsyncDisposable(bool withCSharp8)
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class C
{
    public static async Task Main()
    {
        await foreach (var i in new AsyncEnumerable())
        {
        }
    }
}

struct AsyncEnumerable : IAsyncEnumerable<int>
{
    public AsyncEnumerator GetAsyncEnumerator(CancellationToken token = default) => new AsyncEnumerator();
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken token) => throw null;
}

struct AsyncEnumerator : IAsyncEnumerator<int>
{
    public int Current => 0;
    public async ValueTask<bool> MoveNextAsync()
    {
        await Task.Yield();
        return false;
    }
    public async ValueTask DisposeAsync()
    {
        Console.WriteLine(""RAN"");
        await Task.Yield();
    }

    int IAsyncEnumerator<int>.Current => throw null;
    ValueTask<bool> IAsyncEnumerator<int>.MoveNextAsync() => throw null;
    ValueTask IAsyncDisposable.DisposeAsync() => throw null;
}
";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe,
                parseOptions: withCSharp8 ? TestOptions.Regular8 : TestOptions.Regular7_3);

            if (withCSharp8)
            {
                comp.VerifyDiagnostics();
                CompileAndVerify(comp, expectedOutput: "RAN");
            }
            else
            {
                comp.VerifyDiagnostics(
                    // (11,9): error CS8370: Feature 'async streams' is not available in C# 7.3. Please use language version 8.0 or greater.
                    //         await foreach (var i in new AsyncEnumerable())
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "await").WithArguments("async streams", "8.0").WithLocation(11, 9)
                    );
            }

            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.True(info.IsAsynchronous);
            Assert.Equal("AsyncEnumerator AsyncEnumerable.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> AsyncEnumerator.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 AsyncEnumerator.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask AsyncEnumerator.DisposeAsync()",
                info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
        }

        [Theory, WorkItem(59955, "https://github.com/dotnet/roslyn/issues/59955")]
        [InlineData(true)]
        [InlineData(false)]
        public void DisposePatternPreferredOverIAsyncDisposable_NoIAsyncEnumerable(bool withCSharp8)
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class C
{
    public static async Task Main()
    {
        await foreach (var i in new AsyncEnumerable())
        {
        }
    }
}

struct AsyncEnumerable
{
    public AsyncEnumerator GetAsyncEnumerator(CancellationToken token = default) => new AsyncEnumerator();
}

struct AsyncEnumerator : IAsyncDisposable
{
    public int Current => 0;
    public async ValueTask<bool> MoveNextAsync()
    {
        await Task.Yield();
        return false;
    }
    public async ValueTask DisposeAsync()
    {
        Console.WriteLine(""RAN"");
        await Task.Yield();
    }

    ValueTask IAsyncDisposable.DisposeAsync() => throw null;
}
";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe,
                parseOptions: withCSharp8 ? TestOptions.Regular8 : TestOptions.Regular7_3);

            if (withCSharp8)
            {
                comp.VerifyDiagnostics();
                CompileAndVerify(comp, expectedOutput: "RAN");
            }
            else
            {
                comp.VerifyDiagnostics(
                    // (10,9): error CS8370: Feature 'async streams' is not available in C# 7.3. Please use language version 8.0 or greater.
                    //         await foreach (var i in new AsyncEnumerable())
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "await").WithArguments("async streams", "8.0").WithLocation(10, 9)
                    );
            }

            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.True(info.IsAsynchronous);
            Assert.Equal("AsyncEnumerator AsyncEnumerable.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> AsyncEnumerator.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 AsyncEnumerator.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask AsyncEnumerator.DisposeAsync()",
                info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
        }

        [Theory, WorkItem(59955, "https://github.com/dotnet/roslyn/issues/59955")]
        [InlineData(true)]
        [InlineData(false)]
        public void AsyncEnumerationViaInterfaceUsesIAsyncDisposable(bool withCSharp8)
        {
            // The enumerator type is IAsyncEnumerator<int> so disposal uses IAsyncDisposable.DisposeAsync()
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class C
{
    public static async Task Main()
    {
        await foreach (var i in new AsyncEnumerable())
        {
        }
    }
}

struct AsyncEnumerable : IAsyncEnumerable<int>
{
    IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken token) => new AsyncEnumerator();
}

struct AsyncEnumerator : IAsyncEnumerator<int>
{
    public ValueTask DisposeAsync() => throw null;

    int IAsyncEnumerator<int>.Current => 0;
    async ValueTask<bool> IAsyncEnumerator<int>.MoveNextAsync()
    {
        await Task.Yield();
        return false;
    }
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        Console.WriteLine(""RAN"");
        await Task.Yield();
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe,
                parseOptions: withCSharp8 ? TestOptions.Regular8 : TestOptions.Regular7_3);

            if (withCSharp8)
            {
                comp.VerifyDiagnostics();
                CompileAndVerify(comp, expectedOutput: "RAN");
            }
            else
            {
                comp.VerifyDiagnostics(
                    // (11,9): error CS8370: Feature 'async streams' is not available in C# 7.3. Please use language version 8.0 or greater.
                    //         await foreach (var i in new AsyncEnumerable())
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "await").WithArguments("async streams", "8.0").WithLocation(11, 9)
                    );
            }

            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.True(info.IsAsynchronous);
            Assert.Equal("System.Collections.Generic.IAsyncEnumerator<System.Int32> System.Collections.Generic.IAsyncEnumerable<System.Int32>.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
               info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> System.Collections.Generic.IAsyncEnumerator<System.Int32>.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 System.Collections.Generic.IAsyncEnumerator<System.Int32>.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()",
                info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("System.Int32", info.ElementType.ToTestDisplayString());
        }

        [Fact, WorkItem(59955, "https://github.com/dotnet/roslyn/issues/59955")]
        public void EnumerationViaInterfaceUsesIDisposable()
        {
            var source = @"
using System;
using System.Collections;
using System.Collections.Generic;

class C
{
    public static void Main()
    {
        foreach (var i in new Enumerable())
        {
        }
    }
}

struct Enumerable : IEnumerable<int>
{
    IEnumerator IEnumerable.GetEnumerator() => throw null;
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => new Enumerator();
}

struct Enumerator : IEnumerator<int>
{
    public void Dispose() => throw null;

    int IEnumerator<int>.Current => 0;
    object IEnumerator.Current => throw null;
    void IEnumerator.Reset() => throw null;
    bool IEnumerator.MoveNext() => false;

    void IDisposable.Dispose()
    {
        Console.WriteLine(""RAN"");
    }
}
";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics();
            CompileAndVerify(comp, expectedOutput: "RAN");

            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.Equal("void System.IDisposable.Dispose()", info.DisposeMethod.ToTestDisplayString());
        }

        [Theory, WorkItem(59955, "https://github.com/dotnet/roslyn/issues/59955")]
        [InlineData(true)]
        [InlineData(false)]
        public void DisposePatternPreferredOverIAsyncDisposable_Deconstruction(bool withCSharp8)
        {
            var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class C
{
    public static async Task Main()
    {
        await foreach (var (i, j) in new AsyncEnumerable())
        {
        }
    }
}

struct AsyncEnumerable : IAsyncEnumerable<(int, int)>
{
    public AsyncEnumerator GetAsyncEnumerator(CancellationToken token = default) => new AsyncEnumerator();
    IAsyncEnumerator<(int, int)> IAsyncEnumerable<(int, int)>.GetAsyncEnumerator(CancellationToken token) => throw null;
}

struct AsyncEnumerator : IAsyncEnumerator<(int, int)>
{
    public (int, int) Current => (0, 0);
    public async ValueTask<bool> MoveNextAsync()
    {
        await Task.Yield();
        return false;
    }
    public async ValueTask DisposeAsync()
    {
        Console.WriteLine(""RAN"");
        await Task.Yield();
    }

    (int, int) IAsyncEnumerator<(int, int)>.Current => throw null;
    ValueTask<bool> IAsyncEnumerator<(int, int)>.MoveNextAsync() => throw null;
    ValueTask IAsyncDisposable.DisposeAsync() => throw null;
}
";
            var comp = CreateCompilationWithTasksExtensions(new[] { source, s_IAsyncEnumerable }, options: TestOptions.DebugExe,
                parseOptions: withCSharp8 ? TestOptions.Regular8 : TestOptions.Regular7_3);

            if (withCSharp8)
            {
                comp.VerifyDiagnostics();
                CompileAndVerify(comp, expectedOutput: "RAN");
            }
            else
            {
                comp.VerifyDiagnostics(
                    // (11,9): error CS8370: Feature 'async streams' is not available in C# 7.3. Please use language version 8.0 or greater.
                    //         await foreach (var i in new AsyncEnumerable())
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "await").WithArguments("async streams", "8.0").WithLocation(11, 9)
                    );
            }

            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree, ignoreAccessibility: false);
            var foreachSyntax = tree.GetRoot().DescendantNodes().OfType<ForEachVariableStatementSyntax>().Single();
            var info = model.GetForEachStatementInfo(foreachSyntax);

            Assert.True(info.IsAsynchronous);
            Assert.Equal("AsyncEnumerator AsyncEnumerable.GetAsyncEnumerator([System.Threading.CancellationToken token = default(System.Threading.CancellationToken)])",
                info.GetEnumeratorMethod.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask<System.Boolean> AsyncEnumerator.MoveNextAsync()",
                info.MoveNextMethod.ToTestDisplayString());
            Assert.Equal("(System.Int32, System.Int32) AsyncEnumerator.Current { get; }",
                info.CurrentProperty.ToTestDisplayString());
            Assert.Equal("System.Threading.Tasks.ValueTask AsyncEnumerator.DisposeAsync()",
                info.DisposeMethod.ToTestDisplayString());
            Assert.Equal("(System.Int32, System.Int32)", info.ElementType.ToTestDisplayString());
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/72819")]
        public void PatternBasedFails_AwaitForeach()
        {
            var src = """
using System;
using System.Threading;
using System.Threading.Tasks;

interface ICustomEnumerator
{
    public int Current {get;}

    public ValueTask<bool> MoveNextAsync();
}

interface IGetEnumerator<TEnumerator> where TEnumerator : ICustomEnumerator
{
    TEnumerator GetAsyncEnumerator(CancellationToken token = default);
}

struct S1 : IGetEnumerator<S2>
{
    public S2 GetAsyncEnumerator(CancellationToken token = default)
    {
        return new S2();
    }
}

interface IMyAsyncDisposable1
{
    ValueTask DisposeAsync();
}

interface IMyAsyncDisposable2
{
    ValueTask DisposeAsync();
}

struct S2 : ICustomEnumerator, IMyAsyncDisposable1, IMyAsyncDisposable2, IAsyncDisposable
{
    ValueTask IMyAsyncDisposable1.DisposeAsync() => throw null;
    ValueTask IMyAsyncDisposable2.DisposeAsync() => throw null;
    public ValueTask DisposeAsync()
    { 
        System.Console.Write("D");
        return ValueTask.CompletedTask;
    }

    public int Current => throw null;
    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(false);
    }
}

class C
{
    static async Task Main()
    {
        await Test<S1, S2>();
    }

    static async Task Test<TEnumerable, TEnumerator>()
        where TEnumerable : IGetEnumerator<TEnumerator>
        where TEnumerator : ICustomEnumerator, IMyAsyncDisposable1, IMyAsyncDisposable2, IAsyncDisposable
    {
        await foreach (var i in default(TEnumerable))
        {
            System.Console.Write(i);
        }
    }
}
""";
            var comp = CreateCompilation(src, targetFramework: TargetFramework.Net80, options: TestOptions.ReleaseExe);
            CompileAndVerify(comp,
                expectedOutput: ExecutionConditionUtil.IsMonoOrCoreClr ? "D" : null,
                verify: ExecutionConditionUtil.IsMonoOrCoreClr ? Verification.Passes : Verification.Skipped).VerifyDiagnostics();
        }
    }
}

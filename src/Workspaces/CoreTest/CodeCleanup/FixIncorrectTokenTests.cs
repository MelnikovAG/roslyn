﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeCleanup;
using Microsoft.CodeAnalysis.CodeCleanup.Providers;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests.CodeCleanup;

[UseExportProvider]
[Trait(Traits.Feature, Traits.Features.FixIncorrectTokens)]
public sealed class FixIncorrectTokensTests
{
    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithMatchingIf()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    [|If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                    endif|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                    End If
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithMatchingIf_Directive()
        => VerifyAsync("""
            [|
            #If c = 0 Then
            #Endif|]
            """, """

            #If c = 0 Then
            #End If
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithoutMatchingIf()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    [|EndIf|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    End If
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithoutMatchingIf_Directive()
        => VerifyAsync("""
            [|
            Class X
            End Class

            #Endif|]
            """, """

            Class X
            End Class

            #End If
            """);

    [Fact(Skip = "889521")]
    [WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_SameLineAsIf()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then [|EndIf|]        
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then
                    End If
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_SameLineAsIf_Invalid()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing [|EndIf|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing EndIf
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_SameLineAsIf_Directive()
        => VerifyAsync("""
            [|
            #If c = 0 Then #Endif|]
            """, """

            #If c = 0 Then #Endif
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithLeadingTrivia()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    [|If args IsNot Nothing Then
                        System.Console.WriteLine(args)
            ' Dummy Endif
                    EndIf|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                        ' Dummy Endif
                    End If
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithLeadingTrivia_Directive()
        => VerifyAsync("""
            [|
            #If c = 0 Then
            '#Endif
            #Endif
            |]
            """, """

            #If c = 0 Then
            '#Endif
            #End If

            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_InvocationExpressionArgument()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    [|If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                    InvocationExpression EndIf|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                        InvocationExpression EndIf
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_InvalidDirectiveCases()
        => VerifyAsync("""
            [|
            ' BadDirective cases
            #If c = 0 Then
            #InvocationExpression #Endif

            #If c = 0 Then
            InvocationExpression# #Endif

            #If c = 0 Then
            InvocationExpression #Endif


            ' Missing EndIfDirective cases
            #If c = 0 Then
            #InvocationExpression
            #Endif

            #If c = 0 Then
            InvocationExpression#
            #Endif

            #If c = 0 Then
            InvocationExpression
            #Endif
            |]
            """, """

            ' BadDirective cases
            #If c = 0 Then
            #InvocationExpression #Endif

            #If c = 0 Then
            InvocationExpression# #Endif

            #If c = 0 Then
            InvocationExpression #Endif


            ' Missing EndIfDirective cases
            #If c = 0 Then
            #InvocationExpression
            #End If

            #If c = 0 Then
            InvocationExpression#
            #End If

            #If c = 0 Then
            InvocationExpression
            #End If

            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithTrailingTrivia()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    [|If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                    EndIf ' Dummy EndIf|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                    End If ' Dummy EndIf
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithTrailingTrivia_Directive()
        => VerifyAsync("""
            [|
            #If c = 0 Then
            #Endif '#Endif
            |]
            """, """

            #If c = 0 Then
            #End If '#Endif

            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithIdentifierTokenTrailingTrivia()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    [|If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                    EndIf IdentifierToken|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                    End If IdentifierToken
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_InvalidDirectiveCases_02()
        => VerifyAsync("""
            [|
            ' BadDirective cases
            #If c = 0 Then
            #Endif #IdentifierToken

            #If c = 0 Then
            #Endif IdentifierToken#

            #If c = 0 Then
            #Endif IdentifierToken


            ' Missing EndIfDirective cases
            #If c = 0 Then
            #Endif
            #IdentifierToken

            #If c = 0 Then
            #Endif
            IdentifierToken#

            #If c = 0 Then
            #Endif
            IdentifierToken
            |]
            """, """

            ' BadDirective cases
            #If c = 0 Then
            #End If #IdentifierToken

            #If c = 0 Then
            #End If IdentifierToken#

            #If c = 0 Then
            #End If IdentifierToken


            ' Missing EndIfDirective cases
            #If c = 0 Then
            #End If
            #IdentifierToken

            #If c = 0 Then
            #End If
            IdentifierToken#

            #If c = 0 Then
            #End If
            IdentifierToken

            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithLeadingAndTrailingTrivia()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    [|If args IsNot Nothing Then
                        System.Console.WriteLine(args)
            ' Dummy EndIf
            EndIf
            ' Dummy EndIf|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                        ' Dummy EndIf
                    End If
                    ' Dummy EndIf
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithLeadingAndTrailingTrivia_Directive()
        => VerifyAsync("""
            [|
            #If c = 0 Then
            '#Endif
            #Endif '#Endif
            |]
            """, """

            #If c = 0 Then
            '#Endif
            #End If '#Endif

            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithLeadingAndTrailingInvocationExpressions()
        => VerifyAsync("""

            Module Program
                Sub Main(args As String())
                    [|If args IsNot Nothing Then
                        System.Console.WriteLine(args)
            IdentifierToken
            EndIf
            IdentifierToken|]
                End Sub
            End Module
            """, """

            Module Program
                Sub Main(args As String())
                    If args IsNot Nothing Then
                        System.Console.WriteLine(args)
                        IdentifierToken
                    End If
                    IdentifierToken
                End Sub
            End Module
            """);

    [Fact, WorkItem(17313, "DevDiv_Projects/Roslyn")]
    public Task FixEndIfKeyword_WithLeadingAndTrailingInvocationExpressions_Directive()
        => VerifyAsync("""
            [|
            ' BadDirective cases
            #If c = 0 Then
            #InvalidTrivia #Endif #InvalidTrivia

            #If c = 0 Then
            InvalidTrivia #Endif InvalidTrivia

            #If c = 0 Then
            InvalidTrivia# #Endif InvalidTrivia#


            ' Missing EndIfDirective cases
            #If c = 0 Then
            #InvalidTrivia
            #Endif #InvalidTrivia

            #If c = 0 Then
            InvalidTrivia
            #Endif InvalidTrivia

            #If c = 0 Then
            InvalidTrivia#
            #Endif InvalidTrivia#
            |]
            """, """

            ' BadDirective cases
            #If c = 0 Then
            #InvalidTrivia #Endif #InvalidTrivia

            #If c = 0 Then
            InvalidTrivia #Endif InvalidTrivia

            #If c = 0 Then
            InvalidTrivia# #Endif InvalidTrivia#


            ' Missing EndIfDirective cases
            #If c = 0 Then
            #InvalidTrivia
            #End If #InvalidTrivia

            #If c = 0 Then
            InvalidTrivia
            #End If InvalidTrivia

            #If c = 0 Then
            InvalidTrivia#
            #End If InvalidTrivia#

            """);

    [Fact, WorkItem(5722, "DevDiv_Projects/Roslyn")]
    public Task FixPrimitiveTypeKeywords_ValidCases()
        => VerifyAsync("""
            [|
            Imports SystemAlias = System
            Imports SystemInt16Alias = System.Short
            Imports SystemUInt16Alias = System.ushort
            Imports SystemInt32Alias = System.INTEGER
            Imports SystemUInt32Alias = System.UInteger
            Imports SystemInt64Alias = System.Long
            Imports SystemUInt64Alias = System.uLong
            Imports SystemDateTimeAlias = System.Date

            Module Program
                Sub Main(args As String())
                    Dim a1 As System.Short = 0
                    Dim b1 As SystemAlias.SHORT = a1
                    Dim c1 As SystemInt16Alias = b1

                    Dim a2 As System.UShort = 0
                    Dim b2 As SystemAlias.USHORT = a2
                    Dim c2 As SystemUInt16Alias = b2

                    Dim a3 As System.Integer = 0
                    Dim b3 As SystemAlias.INTEGER = a3
                    Dim c3 As SystemInt32Alias = b3

                    Dim a4 As System.UInteger = 0
                    Dim b4 As SystemAlias.UINTEGER = a4
                    Dim c4 As SystemUInt32Alias = b4

                    Dim a5 As System.Long = 0
                    Dim b5 As SystemAlias.LONG = a5
                    Dim c5 As SystemInt64Alias = b5

                    Dim a6 As System.ULong = 0
                    Dim b6 As SystemAlias.ULONG = 0
                    Dim c6 As SystemUInt64Alias = 0

                    Dim a7 As System.Date = Nothing
                    Dim b7 As SystemAlias.DATE = Nothing
                    Dim c7 As SystemDateTimeAlias = Nothing
                End Sub
            End Module
            |]
            """, """

            Imports SystemAlias = System
            Imports SystemInt16Alias = System.Int16
            Imports SystemUInt16Alias = System.UInt16
            Imports SystemInt32Alias = System.Int32
            Imports SystemUInt32Alias = System.UInt32
            Imports SystemInt64Alias = System.Int64
            Imports SystemUInt64Alias = System.UInt64
            Imports SystemDateTimeAlias = System.DateTime

            Module Program
                Sub Main(args As String())
                    Dim a1 As System.Int16 = 0
                    Dim b1 As SystemAlias.Int16 = a1
                    Dim c1 As SystemInt16Alias = b1

                    Dim a2 As System.UInt16 = 0
                    Dim b2 As SystemAlias.UInt16 = a2
                    Dim c2 As SystemUInt16Alias = b2

                    Dim a3 As System.Int32 = 0
                    Dim b3 As SystemAlias.Int32 = a3
                    Dim c3 As SystemInt32Alias = b3

                    Dim a4 As System.UInt32 = 0
                    Dim b4 As SystemAlias.UInt32 = a4
                    Dim c4 As SystemUInt32Alias = b4

                    Dim a5 As System.Int64 = 0
                    Dim b5 As SystemAlias.Int64 = a5
                    Dim c5 As SystemInt64Alias = b5

                    Dim a6 As System.UInt64 = 0
                    Dim b6 As SystemAlias.UInt64 = 0
                    Dim c6 As SystemUInt64Alias = 0

                    Dim a7 As System.DateTime = Nothing
                    Dim b7 As SystemAlias.DateTime = Nothing
                    Dim c7 As SystemDateTimeAlias = Nothing
                End Sub
            End Module

            """);

    [Fact, WorkItem(5722, "DevDiv_Projects/Roslyn")]
    public async Task FixPrimitiveTypeKeywords_InvalidCases()
    {
        // With a user defined type named System
        // No fixups as System binds to type not a namespace.
        var code = """

            Imports SystemAlias = System
            Imports SystemInt16Alias = System.Short
            Imports SystemUInt16Alias = System.ushort
            Imports SystemInt32Alias = System.INTEGER
            Imports SystemUInt32Alias = System.UInteger
            Imports SystemInt64Alias = System.Long
            Imports SystemUInt64Alias = System.uLong
            Imports SystemDateTimeAlias = System.Date

            Class System
            End Class

            Module Program
                Sub Main(args As String())
                    Dim a1 As System.Short = 0
                    Dim b1 As SystemAlias.SHORT = a1
                    Dim c1 As SystemInt16Alias = b1
                    Dim d1 As System.System.Short = 0
                    Dim e1 As Short = 0

                    Dim a2 As System.UShort = 0
                    Dim b2 As SystemAlias.USHORT = a2
                    Dim c2 As SystemUInt16Alias = b2
                    Dim d2 As System.System.UShort = 0
                    Dim e2 As UShort = 0

                    Dim a3 As System.Integer = 0
                    Dim b3 As SystemAlias.INTEGER = a3
                    Dim c3 As SystemInt32Alias = b3
                    Dim d3 As System.System.Integer = 0
                    Dim e3 As Integer = 0

                    Dim a4 As System.UInteger = 0
                    Dim b4 As SystemAlias.UINTEGER = a4
                    Dim c4 As SystemUInt32Alias = b4
                    Dim d4 As System.System.UInteger = 0
                    Dim e4 As UInteger = 0

                    Dim a5 As System.Long = 0
                    Dim b5 As SystemAlias.LONG = a5
                    Dim c5 As SystemInt64Alias = b5
                    Dim d5 As System.System.Long = 0
                    Dim e5 As Long = 0

                    Dim a6 As System.ULong = 0
                    Dim b6 As SystemAlias.ULONG = 0
                    Dim c6 As SystemUInt64Alias = 0
                    Dim d6 As System.System.ULong = 0
                    Dim e6 As ULong = 0

                    Dim a7 As System.Date = Nothing
                    Dim b7 As SystemAlias.DATE = Nothing
                    Dim c7 As SystemDateTimeAlias = Nothing
                    Dim d7 As System.System.Date = 0
                    Dim e7 As Date = 0
                End Sub
            End Module

            """;

        await VerifyAsync(@"[|" + code + @"|]", expectedResult: code);

        // No Fixes in trivia
        code = """

            Imports SystemAlias = System
            'Imports SystemInt16Alias = System.Short

            Module Program
                Sub Main(args As String())
                    ' Dim a1 As System.Short = 0
                    ' Dim b1 As SystemAlias.SHORT = a1
                End Sub
            End Module

            """;

        await VerifyAsync(@"[|" + code + @"|]", expectedResult: code);
    }

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/606015")]
    public Task FixFullWidthSingleQuotes()
        => VerifyAsync("""
            [|
            ‘ｆｕｌｌｗｉｄｔｈ 1　
            ’ｆｕｌｌｗｉｄｔｈ 2
            ‘‘ｆｕｌｌｗｉｄｔｈ 3
            ’'ｆｕｌｌｗｉｄｔｈ 4
            '‘ｆｕｌｌｗｉｄｔｈ 5
            ‘’ｆｕｌｌｗｉｄｔｈ 6
            ‘’‘’ｆｕｌｌｗｉｄｔｈ 7
            '‘’‘’ｆｕｌｌｗｉｄｔｈ 8|]
            """, """

            'ｆｕｌｌｗｉｄｔｈ 1　
            'ｆｕｌｌｗｉｄｔｈ 2
            '‘ｆｕｌｌｗｉｄｔｈ 3
            ''ｆｕｌｌｗｉｄｔｈ 4
            '‘ｆｕｌｌｗｉｄｔｈ 5
            '’ｆｕｌｌｗｉｄｔｈ 6
            '’‘’ｆｕｌｌｗｉｄｔｈ 7
            '‘’‘’ｆｕｌｌｗｉｄｔｈ 8
            """);

    [Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/707135")]
    public async Task FixFullWidthSingleQuotes2()
    {
        var savedCulture = System.Threading.Thread.CurrentThread.CurrentCulture;

        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                System.Globalization.CultureInfo.CreateSpecificCulture("zh-CN");
            await VerifyAsync(@"[|‘’ｆｕｌｌｗｉｄｔｈ 1|]", @"'’ｆｕｌｌｗｉｄｔｈ 1");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = savedCulture;
        }
    }

    private static string FixLineEndings(string text)
        => text.Replace("\r\n", "\n").Replace("\n", "\r\n");

    private static async Task VerifyAsync(string codeWithMarker, string expectedResult)
    {
        codeWithMarker = FixLineEndings(codeWithMarker);
        expectedResult = FixLineEndings(expectedResult);

        MarkupTestFile.GetSpans(codeWithMarker, out var codeWithoutMarker, out var textSpans);

        var document = CreateDocument(codeWithoutMarker, LanguageNames.VisualBasic);
        var codeCleanups = CodeCleaner.GetDefaultProviders(document).WhereAsArray(p => p.Name is PredefinedCodeCleanupProviderNames.FixIncorrectTokens or PredefinedCodeCleanupProviderNames.Format);

        var cleanDocument = await CodeCleaner.CleanupAsync(document, textSpans[0], await document.GetCodeCleanupOptionsAsync(CancellationToken.None), codeCleanups);

        Assert.Equal(expectedResult, (await cleanDocument.GetSyntaxRootAsync()).ToFullString());
    }

    private static Document CreateDocument(string code, string language)
    {
        var solution = new AdhocWorkspace().CurrentSolution;
        var projectId = ProjectId.CreateNewId();
        var project = solution.AddProject(projectId, "Project", "Project.dll", language).GetProject(projectId);

        return project.AddMetadataReference(NetFramework.mscorlib)
                      .AddDocument("Document", SourceText.From(code));
    }
}

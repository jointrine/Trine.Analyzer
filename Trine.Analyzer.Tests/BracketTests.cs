using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trine.Analyzer.Tests.TestHelper;
using Trine.Analyzer;
using System;

namespace Trine.Analyzer.Tests
{
    [TestClass]
    public class BracketTests : CodeFixVerifier
    {
        [DataTestMethod]
        [DataRow("class X { void Test() { if (true) { return; } } }")]
        [DataRow("class X { void Test() { if (true) { return; } else if { return; } } }")]
        public void NoDiagnosticsWhenCorrect(string source)
        {
            VerifyCSharpDiagnostic(source);
        }

        [TestMethod]
        public void DiagnosticsWhenMissingBrackets()
        {
            var source = @"class X { void Test() { if (true) return; } }";

            VerifyCSharpDiagnostic(source, new DiagnosticResult
            {
                Id = "TRINE03",
                Message = "Missing brackets",
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", 1, 35) }
            });
        }

        [DataTestMethod]
        [DataRow(
"if (true) return;",
@"if (true)
{
    return;
}")]

        [DataRow(
"if (true) { return 1; } else return 2;",
@"if (true) { return 1; } else
{
    return 2;
}")]

        [DataRow(
"if (true) return 1; else return 2;",
@"if (true)
{
    return 1;
}
else
{
    return 2;
}")]
        [DataRow(
@"if (true) 
    return 1;",
@"if (true)
{
    return 1;
}")]
        public void VerifyFixer(string source, string fixedSource)
        {
            VerifyCSharpFix(WrapStatementsInMethod(source), WrapStatementsInMethod(fixedSource));
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new BracketCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new BracketAnalyzer();
        }

        private string WrapStatementsInMethod(string code)
        {
            return $@"class X 
{{ 
    void Test() 
    {{ 
        {code.Replace(Environment.NewLine, Environment.NewLine + new string(' ', 8))}
    }} 
}}";
        }
    }
}

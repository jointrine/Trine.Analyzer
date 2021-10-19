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
    public class TestStructureTests : DiagnosticVerifier
    {
        [DataTestMethod]
        [DataRow("class X { void NoTest() { } } }")]
        [DataRow("class X { [Test] void Test() { Tester.Run(s => s); } }")]
        public void NoDiagnosticsWhenCorrect(string source)
        {
            VerifyCSharpDiagnostic(source);
        }

        [DataTestMethod]
        [DataRow("class X { [Test] void Test() { } } }")]
        [DataRow("class X { [TestCase()] void Test() { } } }")]
        [DataRow("class X { [TestCaseSource()] void Test() { } } }")]
        public void DiagnosticsWhenIncorrect(string source)
        {
            VerifyCSharpDiagnostic(source, new DiagnosticResult
            {
                Id = "TRINE06",
                Message = "Should use Tester.Run(...)",
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", 1, 11) }
            });
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new TestStructureAnalyzer();
        }
    }
}

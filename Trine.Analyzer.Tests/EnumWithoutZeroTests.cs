using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trine.Analyzer.Tests.TestHelper;

namespace Trine.Analyzer.Tests
{
    [TestClass]
    public class EnumWithoutZeroTests : DiagnosticVerifier
    {
        [TestMethod]
        public void NoDiagnosticsWhenEmpty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoDiagnosticsWhenHasNonZeroValues()
        {
            var test = @"enum Enum { A = 1 }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DiagnosticsWhenHasZeroValue()
        {
            var source = @"enum Enum { A = 0 }";

            VerifyCSharpDiagnostic(source, new DiagnosticResult
            {
                Id = "TRINE05",
                Message = "Enum value must not be zero",
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] {new DiagnosticResultLocation("Test0.cs", 1, 1)}
            });
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EnumWithoutZeroAnalyzer();
        }
    }
}
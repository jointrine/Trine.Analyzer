using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trine.Analyzer.Tests.TestHelper;
using Trine.Analyzer;

namespace Trine.Analyzer.Tests
{
    [TestClass]
    public class EnumValueTests : CodeFixVerifier
    {

        [TestMethod]
        public void NoDiagnosticsWhenEmpty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NoDiagnosticsWhenHasValues()
        {
            var test = @"enum Enum { A = 0 }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DiagnosticsWhenMissingValues()
        {
            var source = @"enum Enum { A = 0, B, C = 4, D, E }";

            VerifyCSharpDiagnostic(source, new DiagnosticResult
            {
                Id = "TRINE02",
                Message = "Missing enum value",
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] {new DiagnosticResultLocation("Test0.cs", 1, 1)}
            });

            var fixedSource = @"enum Enum
{
    A = 0,
    B = 1,
    C = 4,
    D = 5,
    E = 6
}";
            VerifyCSharpFix(source, fixedSource);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new EnumValueCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EnumValueAnalyzer();
        }
    }
}

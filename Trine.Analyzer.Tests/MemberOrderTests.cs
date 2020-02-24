using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trine.Analyzer.Tests.TestHelper;
using Trine.Analyzer;

namespace Trine.Analyzer.Tests
{
    [TestClass]
    public class MemberOrderTests : CodeFixVerifier
    {

        [TestMethod]
        public void NoDiagnosticsWhenEmpty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void InvalidOrderWithFixer()
        {
            var test = @"
namespace Trine
{
    public class TestClass
    {
        public class SubClass {}

        private string PrivateProperty { get; }
        internal string InternalProperty { get; }

        int nonConstField;
        int anotherNonConstField;
        const int constField = 1;

        public static bool operator ==(TestClass p1, TestClass p2) {
            return true;
        }

        // Keep comment
        protected TestClass() {}

        public TestClass(string title, string details) {}

        public void Method() {}
        public static void StaticMethod() {}
        public static void AnotherStaticMethod() {}
    }
}
";
            VerifyCSharpDiagnostic(test, new[]{
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Property should be declared before Class",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] {new DiagnosticResultLocation("Test0.cs", 8, 9)}
                },
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Internal should be declared before Private",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] {new DiagnosticResultLocation("Test0.cs", 9, 9)}
                },
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Field should be declared before Property",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] {new DiagnosticResultLocation("Test0.cs", 11, 9)}
                },
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Constant should be declared before Field",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] {new DiagnosticResultLocation("Test0.cs", 13, 9)}
                },
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Public should be declared before Protected",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] {new DiagnosticResultLocation("Test0.cs", 22, 9)}
                },
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Static should be declared before NonStatic",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] {new DiagnosticResultLocation("Test0.cs", 25, 9)}
                },
            });

            var fixtest = @"
namespace Trine
{
    public class TestClass
    {
        const int constField = 1;

        int nonConstField;
        int anotherNonConstField;

        public TestClass(string title, string details) {}

        // Keep comment
        protected TestClass() {}

        internal string InternalProperty { get; }

        private string PrivateProperty { get; }

        public static void StaticMethod() {}

        public static void AnotherStaticMethod() {}

        public void Method() {}

        public class SubClass {}

        public static bool operator ==(TestClass p1, TestClass p2) {
            return true;
        }
    }
}
";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void WhenImplementingInterface()
        {
            var source = @"
                interface ITest 
                {
                    void A();
                    void B();
                }

                interface ITest2
                {
                    void C();
                }

                class Test : ITest, ITest2
                {
                    public void C() {}
                    public void B() {}
                    public void A() {}
                }
            ";
            var @fixed = @"
                interface ITest 
                {
                    void A();
                    void B();
                }

                interface ITest2
                {
                    void C();
                }

                class Test : ITest, ITest2
                {
                    public void A() {}

                    public void B() {}

                    public void C() {}
                }
            ";
            VerifyCSharpFix(source, @fixed);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new MemberOrderCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new MemberOrderAnalyzer();
        }
    }
}

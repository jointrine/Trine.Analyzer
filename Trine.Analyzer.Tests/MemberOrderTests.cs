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
using System;
using System.Runtime.Serialization;

namespace Trine.Core.Error
{
    [Serializable]
    public class UserFriendlyException : Exception
    {
        private string Title { get; }
        internal string Details { get; }

        protected UserFriendlyException(SerializationInfo info, StreamingContext context): base(info, context)
        {
            Title = info.GetString(""pTitle"");
            Details = info.GetString(""pDetails"");
        }

        public UserFriendlyException(string title, string details)
            : base(title + "": "" + details)
        {
            Title = title;
            Details = details;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(""pDetails"", Details);
            info.AddValue(""pTitle"", Title);
        }
    }
}
";
            VerifyCSharpDiagnostic(test, new[]{
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Internal should be declared before Private",
                    Severity = DiagnosticSeverity.Warning,
                    Locations =
                        new[] {
                                new DiagnosticResultLocation("Test0.cs", 11, 9)
                            }
                },
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Constructor should be declared before Property",
                    Severity = DiagnosticSeverity.Warning,
                    Locations =
                        new[] {
                                new DiagnosticResultLocation("Test0.cs", 13, 9)
                            }
                },
                new DiagnosticResult
                {
                    Id = "TRINE01",
                    Message = "Public should be declared before Protected",
                    Severity = DiagnosticSeverity.Warning,
                    Locations =
                        new[] {
                                new DiagnosticResultLocation("Test0.cs", 19, 9)
                            }
                }
            });

            var fixtest = @"
using System;
using System.Runtime.Serialization;

namespace Trine.Core.Error
{
    [Serializable]
    public class UserFriendlyException : Exception
    {
        public UserFriendlyException(string title, string details)
            : base(title + "": "" + details)
        {
            Title = title;
            Details = details;
        }
        protected UserFriendlyException(SerializationInfo info, StreamingContext context): base(info, context)
        {
            Title = info.GetString(""pTitle"");
            Details = info.GetString(""pDetails"");
        }

        internal string Details { get; }

        private string Title { get; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(""pDetails"", Details);
            info.AddValue(""pTitle"", Title);
        }
    }
}
";
            VerifyCSharpFix(test, fixtest);
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

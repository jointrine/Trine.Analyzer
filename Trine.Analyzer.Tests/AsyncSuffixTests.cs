using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trine.Analyzer.Tests.TestHelper;

namespace Trine.Analyzer.Tests
{
    [TestClass]
    public class AsyncSuffixTests : CodeFixVerifier
    {
        [DataTestMethod]
        [DataRow("class X { void Test() { } }")]
        [DataRow("class X { Task TestAsync() { return Task.CompletedTask; } }")]
        [DataRow("class X { System.Task TestAsync() { return System.Task.CompletedTask; } }")]
        [DataRow("class X { System.Task<int> TestAsync() { return System.Task.FromResult(1); } }")]
        [DataRow("class X { IAsyncEnumerable<int> TestAsync() {  } }")]
        [DataRow("class X { ValueTask<int> TestAsync() {  } }")]
        [DataRow("class Program { static async Task Main() { return Task.CompletedTask; } }")]
        public void NoDiagnosticsWhenCorrect(string source)
        {
            VerifyCSharpDiagnostic(source);
        }

        [TestMethod]
        public void DiagnosticsWhenMissingAsync()
        {
            var source = @"class X { System.Task Test() { return System.Task.CompletedTask; } }";

            VerifyCSharpDiagnostic(source, new DiagnosticResult
            {
                Id = "TRINE04",
                Message = "Invalid Async suffix",
                Severity = DiagnosticSeverity.Warning,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", 1, 23) }
            });
        }

        [DataTestMethod]
        [DataRow(
            "System.Task Test() { return System.Task.CompletedTask; }", 
            "System.Task TestAsync() { return System.Task.CompletedTask; }")]
        [DataRow(
            "Task Test() { return Task.CompletedTask; }", 
            "Task TestAsync() { return Task.CompletedTask; }")]
        [DataRow(
            "void TestAsync() { }", 
            "void Test() { }")]
        [DataRow(
            "void TestAsync() { } void Go() { TestAsync(); }", 
            "void Test() { } void Go() { Test(); }")]
        public void VerifyFixer(string source, string fixedSource)
        {
            VerifyCSharpFix(WrapStatementsInClass(source), WrapStatementsInClass(fixedSource));
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new AsyncSuffixCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AsyncSuffixAnalyzer();
        }

        private string WrapStatementsInClass(string code)
        {
            return $@"class X 
{{ 
    {code.Replace(Environment.NewLine, Environment.NewLine + new string(' ', 8))}
}}";
        }
    }
}
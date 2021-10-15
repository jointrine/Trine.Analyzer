using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trine.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TestStructureAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = DiagnosticIds.TestStructure;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Invalid test structure",
            "Should use Tester.Run(...)",
            "Category",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            if (HasTestAttribute(methodDeclaration))
            {
                if (!methodDeclaration.Body.Statements.Any(IsTesterRunStatement))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.GetLocation()));
                }
            }
        }

        private static bool IsTesterRunStatement(StatementSyntax arg)
        {
            var testerRun = ((arg as ExpressionStatementSyntax)?.Expression as InvocationExpressionSyntax)
                ?.Expression
                as MemberAccessExpressionSyntax;
            return testerRun?.Name.Identifier.Text == "Run" && testerRun?.Expression.ToString() == "Tester";
        }

        private static bool HasTestAttribute(MethodDeclarationSyntax methodDeclaration)
        {
            return methodDeclaration.AttributeLists.Any(list => list.Attributes.Any(IsTestAttribute));
        }

        private static bool IsTestAttribute(AttributeSyntax a)
        {
            var attributeName = a.Name.GetText().ToString();
            return attributeName == "Test" || attributeName == "TestCase" || attributeName == "TestCaseSource";
        }
    }
}
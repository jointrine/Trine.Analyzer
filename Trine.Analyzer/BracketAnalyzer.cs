using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trine.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BracketAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TRINE03";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Missing brackets",
            "Missing brackets",
            "Category",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
        }

        private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
        {
            var ifStatementSyntax = (IfStatementSyntax)context.Node;
            AnalyzeStatement(context, ifStatementSyntax.Statement);
            if (ifStatementSyntax.Else != null &&
                !ifStatementSyntax.Else.Statement.IsKind(SyntaxKind.IfStatement))
            {
                AnalyzeStatement(context, ifStatementSyntax.Else.Statement);
            }
        }

        private static void AnalyzeStatement(SyntaxNodeAnalysisContext context, StatementSyntax statement)
        {
            var missingBrackets = !statement.IsKind(SyntaxKind.Block);
            if (missingBrackets)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, statement.GetLocation()));
            }
        }
    }
}
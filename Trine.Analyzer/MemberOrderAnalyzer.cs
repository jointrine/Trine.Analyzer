using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trine.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MemberOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TRINE01";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Incorrect order", "{0} should be declared before {1}", "Category", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
        {
            var cls = (ClassDeclarationSyntax)context.Node;
            SortOrder? prevSortOrder = null;
            foreach (var member in cls.Members)
            {
                var sortOrder = new SortOrder(member, context.SemanticModel);
                if (prevSortOrder != null
                    && prevSortOrder.IsKnown
                    && sortOrder.IsKnown
                    && sortOrder.CompareTo(prevSortOrder) < 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, member.GetLocation(), SortOrder.FormatOrderDifference(sortOrder, prevSortOrder)));
                }

                prevSortOrder = sortOrder;
            }
        }
    }
}
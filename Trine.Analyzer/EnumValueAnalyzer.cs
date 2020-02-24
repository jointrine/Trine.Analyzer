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
    public class EnumValueAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TRINE02";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Missing enum value", "Missing enum value", "Category", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.EnumDeclaration);
        }

        private static void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
        {
            var enumSyntax = (EnumDeclarationSyntax)context.Node;
            var hasEnumMembersWithoutValues = enumSyntax.Members
                .Any((EnumMemberDeclarationSyntax m) => m.EqualsValue == null);
            if (hasEnumMembersWithoutValues)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, enumSyntax.GetLocation()));
            }
        }
    }
}
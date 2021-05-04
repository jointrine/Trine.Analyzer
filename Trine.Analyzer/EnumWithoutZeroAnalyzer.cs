using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trine.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnumWithoutZeroAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = DiagnosticIds.EnumWithoutZero;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Enum value must not be zero", "Enum value must not be zero", "Category", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.EnumDeclaration);
        }

        private static void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            var zeroConstant = SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(0)
            );
            
            var enumSyntax = (EnumDeclarationSyntax)context.Node;
            var hasEnumWithZeroValue = enumSyntax.Members
                .Any(member =>
                {
                    if (member.EqualsValue == null) return false;
                    
                    var constantValue = semanticModel.GetConstantValue(member.EqualsValue.Value);
                    if (!constantValue.HasValue) return false;
                    
                    return (int) constantValue.Value == 0;

                });
            if (hasEnumWithZeroValue)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, enumSyntax.GetLocation()));
            }
        }
    }
}
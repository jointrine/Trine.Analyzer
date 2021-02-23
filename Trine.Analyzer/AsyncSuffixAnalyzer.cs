using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trine.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AsyncSuffixAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TRINE04";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Invalid Async suffix",
            "Invalid Async suffix",
            "Category",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        internal static bool IsTask(TypeSyntax type)
        {
            return type switch
            {
                QualifiedNameSyntax qualifiedNameSyntax => IsTask(qualifiedNameSyntax.Right),
                SimpleNameSyntax simpleNameSyntax => simpleNameSyntax.Identifier.Text == "Task",
                _ => false
            };
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodSyntax = (MethodDeclarationSyntax)context.Node;
            var returnsTask = IsTask(methodSyntax.ReturnType);
            var hasAsyncSuffix = methodSyntax.Identifier.Text.EndsWith("Async");
            if (returnsTask != hasAsyncSuffix)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, methodSyntax.GetLocation()));
            }
        }
    }
}
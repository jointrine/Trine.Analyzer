using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trine.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AsyncSuffixAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = DiagnosticIds.AsyncSuffix;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Invalid Async suffix",
            "Invalid Async suffix",
            "Category",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        internal static bool IsTask(TypeSyntax type)
        {
            var taskTypes = new[] {"Task", "IAsyncEnumerable", "ValueTask"};
            return type switch
            {
                QualifiedNameSyntax qualifiedNameSyntax => IsTask(qualifiedNameSyntax.Right),
                SimpleNameSyntax simpleNameSyntax => taskTypes.Contains(simpleNameSyntax.Identifier.Text),
                _ => false
            };
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodSyntax = (MethodDeclarationSyntax) context.Node;
            var returnsTask = IsTask(methodSyntax.ReturnType);
            var hasAsyncSuffix = methodSyntax.Identifier.Text.EndsWith("Async");
            if (returnsTask != hasAsyncSuffix && !IsProgramMain(methodSyntax))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, methodSyntax.Identifier.GetLocation()));
            }
        }

        private static bool IsProgramMain(MethodDeclarationSyntax methodSyntax)
        {
            return methodSyntax.Identifier.Text == "Main"
                   && methodSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                   && methodSyntax.Parent is ClassDeclarationSyntax
                       classDeclarationSyntax
                   && classDeclarationSyntax.Identifier.Text == "Program";
        }
    }
}
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Rename;

namespace Trine.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncSuffixCodeFixProvider)), Shared]
    public class AsyncSuffixCodeFixProvider : CodeFixProvider
    {
        private const string title = "Update Async suffix";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(AsyncSuffixAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: async ct =>
                    {
                        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                        var semanticModel = await context.Document.GetSemanticModelAsync();
                        var solution = context.Document.Project.Solution;

                        foreach (var diagnostic in context.Diagnostics)
                        {
                            var node = root.FindNode(diagnostic.Location.SourceSpan);
                            var method = node as MethodDeclarationSyntax;
                            if (method == null)
                            {
                                continue;
                            }

                            var isTask = AsyncSuffixAnalyzer.IsTask(method.ReturnType);
                            var methodName = method.Identifier.Text;
                            var newName = isTask 
                                ? methodName + "Async" 
                                : methodName.Substring(0, methodName.Length - "Async".Length);
                            var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                            solution = await Renamer.RenameSymbolAsync(solution, methodSymbol, newName,
                                solution.Options, ct);
                        }
                        return solution;
                    },
                    equivalenceKey: title),
                context.Diagnostics);
            return Task.CompletedTask;
        }
    }
}
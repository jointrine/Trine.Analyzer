using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Trine.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestStructureCodeFixProvider)), Shared]
    public class TestStructureCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add Tester.Run(...)";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(TestStructureAnalyzer.DiagnosticId); }
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
                    createChangedDocument: async ct =>
                    {
                        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
                            .ConfigureAwait(false);
                        var semanticModel = await context.Document.GetSemanticModelAsync();

                        var testMethodsToFix = context.Diagnostics
                            .Select(d => root.FindNode(d.Location.SourceSpan) as MethodDeclarationSyntax)
                            .Distinct();
                        foreach (var testMethod in testMethodsToFix)
                        {
                            if (testMethod == null)
                            {
                                continue;
                            }

                            root = root.ReplaceNode(testMethod,
                                testMethod.AddBodyStatements(SyntaxFactory.ParseStatement("Tester.Run(s => s);")));
                        }

                        return context.Document.WithSyntaxRoot(root);
                    },
                    equivalenceKey: title),
                context.Diagnostics);
            return Task.CompletedTask;
        }
    }
}
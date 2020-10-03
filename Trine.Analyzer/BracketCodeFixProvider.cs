using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;

namespace Trine.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BracketCodeFixProvider)), Shared]
    public class BracketCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add brackets";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(BracketAnalyzer.DiagnosticId); }
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
                        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                        var semanticModel = await context.Document.GetSemanticModelAsync();

                        var statementsToFix = context.Diagnostics
                            .Select(d => root.FindNode(d.Location.SourceSpan) as StatementSyntax)
                            .Distinct();
                        foreach (var statement in statementsToFix)
                        {
                            if (statement == null) continue;

                            var updatedStatement = SyntaxFactory.Block(statement);
                            var updatedParent = statement.Parent.ReplaceNode(statement, updatedStatement);

                            root = root.ReplaceNode(statement, updatedStatement);
                        }
                        return context.Document.WithSyntaxRoot(root);
                    },
                    equivalenceKey: title),
                context.Diagnostics);
            return Task.CompletedTask;
        }
    }
}

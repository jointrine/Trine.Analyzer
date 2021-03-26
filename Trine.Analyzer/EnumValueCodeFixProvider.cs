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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnumValueCodeFixProvider)), Shared]
    public class EnumValueCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add enum values";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(EnumValueAnalyzer.DiagnosticId); }
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

                        var enumSyntaxesToFix = context.Diagnostics
                            .Select(d => root.FindNode(d.Location.SourceSpan) as EnumDeclarationSyntax)
                            .Distinct();
                        foreach(var enumSyntax in enumSyntaxesToFix)
                        {
                            if (enumSyntax == null) continue;
                            
                            var nextValue = 1;

                            var updatedEnumSyntax = enumSyntax.WithMembers(
                                SyntaxFactory.SeparatedList(
                                enumSyntax.Members.Select(member =>
                                {
                                    if (member.EqualsValue == null)
                                    {
                                        return member.WithEqualsValue(
                                            SyntaxFactory.EqualsValueClause(
                                                SyntaxFactory.LiteralExpression(
                                                    SyntaxKind.NumericLiteralExpression,
                                                    SyntaxFactory.Literal(nextValue++)
                                                )
                                            ));
                                    }
                                    else
                                    {
                                        var constantValue = semanticModel.GetConstantValue(member.EqualsValue.Value, ct);
                                        if (constantValue.HasValue)
                                        {
                                            nextValue = (int)constantValue.Value + 1;
                                        }
                                        return member;
                                    }
                                })));

                            updatedEnumSyntax = updatedEnumSyntax.NormalizeWhitespace(elasticTrivia: true)
                                .WithLeadingTrivia(updatedEnumSyntax.GetLeadingTrivia())
                                .WithTrailingTrivia(updatedEnumSyntax.GetTrailingTrivia());

                            root = root.ReplaceNode(enumSyntax, updatedEnumSyntax);
                        }
                        return context.Document.WithSyntaxRoot(root);
                    },
                    equivalenceKey: title),
                context.Diagnostics);
            return Task.CompletedTask;
        }
    }
}

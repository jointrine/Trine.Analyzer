using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Trine.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberOrderCodeFixProvider)), Shared]
    public class MemberOrderCodeFixProvider : CodeFixProvider
    {
        private const string title = "Fix order";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MemberOrderAnalyzer.DiagnosticId); }
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

                        var classes = context.Diagnostics
                            .Select(d => root.FindNode(d.Location.SourceSpan).Parent as ClassDeclarationSyntax)
                            .Distinct();
                        foreach(var cls in classes)
                        {
                            if (cls == null) continue;
                            var updatedClass = ReorderClass(semanticModel, cls);
                            root = root.ReplaceNode(cls, updatedClass);
                        }
                        return context.Document.WithSyntaxRoot(root);
                    },
                    equivalenceKey: title),
                context.Diagnostics);
            return Task.CompletedTask;
        }

        internal static ClassDeclarationSyntax ReorderClass(SemanticModel semanticModel, ClassDeclarationSyntax cls)
        {
            SyntaxList<MemberDeclarationSyntax> originalMembers = cls.Members;

            SortOrder? prevSortOrder = null;
            var sortedMembers = originalMembers
                .Select(member => new
                {
                    Member = member,
                    SortOrder = new SortOrder(member, semanticModel)
                })
                .OrderBy(member => member.SortOrder)
                .Select(member =>
                {
                    var newTrivia = FormatNewlines(
                        member.Member.GetLeadingTrivia(), 
                        member.SortOrder, 
                        prevSortOrder);
                    var updated = member.Member.WithLeadingTrivia(newTrivia);
                    prevSortOrder = member.SortOrder;
                    return updated;
                });
            ;

            return cls.WithMembers(new SyntaxList<MemberDeclarationSyntax>(sortedMembers));
        }

        private static SyntaxTriviaList FormatNewlines(SyntaxTriviaList currentTrivia, SortOrder sortOrder, SortOrder? prevSortOrder)
        {
            var addNewLine = ShouldAddNewLine(sortOrder, prevSortOrder);
            var list = currentTrivia.SkipWhile(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
            if (addNewLine)
            {
                list = new[]{ SyntaxFactory.CarriageReturnLineFeed }.Concat(list);
            }
            return new SyntaxTriviaList(list);
        }

        private static bool ShouldAddNewLine(SortOrder sortOrder, SortOrder? prevSortOrder)
        {
            if (prevSortOrder == null) return false;
            if (sortOrder.Declaration != SortOrder.DeclarationOrder.Constant &&
                sortOrder.Declaration != SortOrder.DeclarationOrder.Field &&
                sortOrder.Declaration != SortOrder.DeclarationOrder.Property)
                {
                    return true;
                }

            if (sortOrder.CompareTo(prevSortOrder) == 0) return false;
            return true;
        }
    }
}

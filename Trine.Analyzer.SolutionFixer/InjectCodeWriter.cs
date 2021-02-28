using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;

namespace Trine.Analyzer.SolutionFixer
{
    public class InjectCodeWriter : CSharpSyntaxRewriter
    {
        private readonly string _className;
        private readonly string _fieldName;

        public InjectCodeWriter(string className, string fieldName)
        {
            _className = className;
            _fieldName = fieldName;
        }

        public async Task<Solution> Inject(MSBuildWorkspace workspace, Solution solution)
        {
            var documentIds = solution.Projects.SelectMany(p => p.DocumentIds).ToArray();
            foreach (var documentId in documentIds)
            {
                var document = solution.GetDocument(documentId)!;
                WriteProgress(documentId, documentIds, document.Name);
                solution = (await UpdateDocument(document, workspace)).Project.Solution;
            }

            return solution;
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var updatedNode = (ClassDeclarationSyntax) base.VisitClassDeclaration(node)!;
            if (updatedNode != node)
            {
                updatedNode = Inject(updatedNode);
                Console.WriteLine("Fixed " + node.Identifier.Text);
            }

            return updatedNode;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == _className && node.Parent is MemberAccessExpressionSyntax)
            {
                return SyntaxFactory.IdentifierName(_fieldName).WithTriviaFrom(node);
            }

            return base.VisitIdentifierName(node);
        }

        private static void WriteProgress(DocumentId documentId, DocumentId[] documentIds, string documentName)
        {
            var percentage = Array.IndexOf(documentIds, documentId) * 100 / documentIds.Length;
            Console.Write($"\r{percentage}% {documentName}".PadRight(100));
        }

        private static ClassDeclarationSyntax Inject(ClassDeclarationSyntax node)
        {
            return InjectInstanceCodeFixProvider.InjectToConstructor(SyntaxFactory.IdentifierName("EventBus"), node,
                SyntaxFactory.Identifier(("_eventBus")));
        }

        private async Task<Document> UpdateDocument(Document document, Workspace workspace)
        {
            var originalSyntaxRoot = await document.GetSyntaxRootAsync();
            var syntaxNode = Visit(originalSyntaxRoot);
            return originalSyntaxRoot != syntaxNode
                ? document.WithSyntaxRoot(Formatter.Format(syntaxNode, workspace))
                : document;
        }
    }
}
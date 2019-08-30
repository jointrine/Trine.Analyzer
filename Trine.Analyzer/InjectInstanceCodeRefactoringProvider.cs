using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Trine.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectInstanceCodeFixProvider)), Shared]
    public class InjectInstanceCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create("CS0120", "CS0119"); }
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var textSpan = context.Span;
            var cancellationToken = context.CancellationToken;

            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SyntaxToken token = root.FindToken(textSpan.Start);

            if (!token.IsKind(SyntaxKind.IdentifierToken))
            {
                return;
            }

            var classNode = FindClass(token.Parent);
            if (classNode == null)
            {
                return;
            }

            context.RegisterCodeFix(
                new InjectCodeAction("Inject instance",
                    (c) => InjectInstance(document, classNode, token.Text)),
                    context.Diagnostics);
        }

        private ClassDeclarationSyntax FindClass(SyntaxNode node)
        {
            if (node == null) return null;
            if (node is ClassDeclarationSyntax classNode) return classNode;
            return FindClass(node.Parent);
        }

        private async Task<Document> InjectInstance(Document document, ClassDeclarationSyntax classNode, string typeName)
        {
            var fieldName = "_" + ToVariableName(typeName);
            var fieldIdentifier = SyntaxFactory.Identifier(fieldName);

            ClassDeclarationSyntax updatedClass = classNode;
            updatedClass = (ClassDeclarationSyntax)new IdentifierRewrtier(typeName, fieldName).Visit(updatedClass);
            updatedClass = AddField(updatedClass, typeName, fieldIdentifier);
            updatedClass = InjectInConstructor(updatedClass, typeName, fieldIdentifier);

            var root = await document.GetSyntaxRootAsync();
            root = root.ReplaceNode(classNode, updatedClass);
            return document.WithSyntaxRoot(root);
        }

        private static ClassDeclarationSyntax AddField(ClassDeclarationSyntax classNode, string serviceName, SyntaxToken fieldIdentifier)
        {
            var serviceType = SyntaxFactory.ParseTypeName(serviceName);
            var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(serviceType,
                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(fieldIdentifier))))
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
                .WithLeadingTrivia(SyntaxFactory.ElasticEndOfLine("\r\n"));
            return classNode.AddSortedMembers(field);
        }

        private ClassDeclarationSyntax InjectInConstructor(ClassDeclarationSyntax classNode, string symbolName, SyntaxToken fieldName)
        {
            string paramName = ToVariableName(symbolName);
            var oldConstructor = (ConstructorDeclarationSyntax)classNode.Members.FirstOrDefault(m => m.IsKind(SyntaxKind.ConstructorDeclaration));
            var constructor = oldConstructor;
            if (constructor == null)
            {
                constructor = SyntaxFactory.ConstructorDeclaration(classNode.Identifier)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .NormalizeWhitespace();
            }
            constructor = constructor.AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                    .WithType(SyntaxFactory.ParseTypeName(symbolName)))
                .AddBodyStatements(SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(fieldName),
                    SyntaxFactory.IdentifierName(paramName))));

            if (oldConstructor != null)
            {
                classNode = classNode.ReplaceNode(oldConstructor, constructor);
            }
            else
            {
                classNode = classNode.AddSortedMembers(constructor);
            }

            return classNode;
        }

        private static string ToVariableName(string symbolName)
        {
            var paramName = symbolName;
            if (paramName.StartsWith("I"))
            {
                paramName = paramName.Substring(1);
            }
            paramName = paramName.Substring(0, 1).ToLower() + paramName.Substring(1);
            return paramName;
        }

        class IdentifierRewrtier : CSharpSyntaxRewriter
        {
            private readonly string _from;
            private readonly string _to;
            public IdentifierRewrtier(string from, string to)
            {
                _from = from;
                _to = to;

            }
            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                if (node.Identifier.Text == _from)
                {
                    return node.WithIdentifier(SyntaxFactory.Identifier(_to));
                }
                return node;
            }
        }

        private class InjectCodeAction : CodeAction
        {
            private readonly Func<CancellationToken, Task<Document>> generateDocument;
            private readonly string title;

            public InjectCodeAction(string title, Func<CancellationToken, Task<Document>> generateDocument)
            {
                this.title = title;
                this.generateDocument = generateDocument;
            }

            public override string Title { get { return title; } }

            protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
            {
                return generateDocument(cancellationToken);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Trine.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectInstanceCodeFixProvider)), Shared]
    public class InjectInstanceCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create("CS0120", "CS0119"); }
        }

        public static ClassDeclarationSyntax InjectToConstructor(SimpleNameSyntax typeName,
            ClassDeclarationSyntax updatedClass, SyntaxToken fieldIdentifier)
        {
            updatedClass = AddField(updatedClass, typeName, fieldIdentifier);
            updatedClass = InjectInConstructor(updatedClass, typeName.WithoutTrivia(), fieldIdentifier);
            return updatedClass;
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var textSpan = context.Span;
            var cancellationToken = context.CancellationToken;

            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var token = root.FindToken(textSpan.Start);

            if (!token.IsKind(SyntaxKind.IdentifierToken))
            {
                return;
            }

            var syntax = token.Parent as SimpleNameSyntax;
            if (syntax == null) return;

            var classNode = FindClass(syntax);
            if (classNode == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create("Inject instance",
                    (c) => InjectInstance(document, classNode, syntax),
                    equivalenceKey: "Inject instance"),
                    context.Diagnostics);
        }

        private static ClassDeclarationSyntax AddField(ClassDeclarationSyntax classNode, SimpleNameSyntax serviceType, SyntaxToken fieldIdentifier)
        {
            var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(serviceType,
                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(fieldIdentifier))))
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
                .WithLeadingTrivia(SyntaxFactory.ElasticEndOfLine("\r\n"));
            return classNode.AddSortedMembers(field);
        }

        private static string ToVariableName(SimpleNameSyntax symbolName)
        {
            var paramName = symbolName.Identifier.Text;
            if (new Regex("^I[A-Z]").IsMatch(paramName))
            {
                paramName = paramName.Substring(1);
            }
            paramName = paramName.Substring(0, 1).ToLower() + paramName.Substring(1);
            return paramName;
        }

        private static ClassDeclarationSyntax InjectInConstructor(ClassDeclarationSyntax classNode, SimpleNameSyntax symbolName, SyntaxToken fieldName)
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
                    .WithType(symbolName))
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

        private ClassDeclarationSyntax? FindClass(SyntaxNode? node)
        {
            if (node == null) return null;
            if (node is ClassDeclarationSyntax classNode) return classNode;
            return FindClass(node.Parent);
        }

        private async Task<Document> InjectInstance(Document document, ClassDeclarationSyntax classNode, SimpleNameSyntax typeName)
        {
            var fieldName = "_" + ToVariableName(typeName);
            var fieldIdentifier = SyntaxFactory.Identifier(fieldName);

            ClassDeclarationSyntax updatedClass = classNode;
            var typeNameStr = typeName.GetText().ToString();
            updatedClass = updatedClass.ReplaceNode(typeName, SyntaxFactory.IdentifierName(fieldName));
            updatedClass = InjectToConstructor(typeName, updatedClass, fieldIdentifier);

            var root = await document.GetSyntaxRootAsync();
            root = root.ReplaceNode(classNode, updatedClass);
            return document.WithSyntaxRoot(root);
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
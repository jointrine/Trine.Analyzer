using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Trine.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MemberOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TRINE01";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Incorrect order", "{0} should be declared before {1}", "Category", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
        {
            var cls = (ClassDeclarationSyntax)context.Node;
            MemberDeclarationSyntax prevMember = null;
            foreach(var member in cls.Members)
            {
                if (prevMember != null) 
                {
                    DeclarationOrder? order = GetDeclarationOrder(member);
                    if (order != null)
                    {
                        var prevOrder = GetDeclarationOrder(prevMember);
                        if (prevOrder != null && order < prevOrder)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Rule, member.GetLocation(), order, prevOrder));
                        }
                        else if (order == prevOrder)
                        {
                            var visibility = GetVisibilityOrder(member, context.SemanticModel);
                            if (visibility != null)
                            {
                                var prevVisibility = GetVisibilityOrder(prevMember, context.SemanticModel);
                                if (prevVisibility != null && visibility < prevVisibility)
                                {
                                    context.ReportDiagnostic(Diagnostic.Create(Rule, member.GetLocation(), visibility, prevVisibility));
                                }
                            }
                        }
                    }
                }
                prevMember = member;
            }
        }

        internal static (DeclarationOrder?, VisibilityOrder?) GetSortOrder(MemberDeclarationSyntax member, SemanticModel semanticModel)
        {
            return (GetDeclarationOrder(member), GetVisibilityOrder(member, semanticModel));
        }

        private static DeclarationOrder? GetDeclarationOrder(MemberDeclarationSyntax member)
        {
            DeclarationOrder? order = null;
            switch (member.Kind())
            {
                case SyntaxKind.MethodDeclaration: order = DeclarationOrder.Method; break;
                case SyntaxKind.FieldDeclaration: order = DeclarationOrder.Field; break;
                case SyntaxKind.ConstructorDeclaration: order = DeclarationOrder.Constructor; break;
                case SyntaxKind.PropertyDeclaration: order = DeclarationOrder.Property; break;
            }

            return order;
        }

        private static VisibilityOrder? GetVisibilityOrder(MemberDeclarationSyntax member, SemanticModel semanticModel)
        {
            var acessibility = semanticModel.GetDeclaredSymbol(member)?.DeclaredAccessibility;
            switch(acessibility)
            {
                case Accessibility.Public: return VisibilityOrder.Public;
                case Accessibility.Protected: return VisibilityOrder.Protected;
                case Accessibility.Internal: return VisibilityOrder.Internal;
                case Accessibility.Private: return VisibilityOrder.Private;   
            }
            return null;
        }

        internal enum VisibilityOrder
        {
            Public,
            Protected,
            Internal,
            Private
        }

        internal enum DeclarationOrder
        {
            Field,
            Constructor,
            Property,
            Method
        }
    }
}

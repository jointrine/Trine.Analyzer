using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Trine.Analyzer
{
    internal class SortOrder : IComparable<SortOrder>
    {
        private readonly DeclarationOrder? _declarationOrder;
        private readonly VisibilityOrder? _visibilityOrder;
        private readonly StaticOrder? _staticOrder;

        public SortOrder(MemberDeclarationSyntax member, SemanticModel semanticModel)
        {
            _declarationOrder = GetDeclarationOrder(member);
            _visibilityOrder = GetVisibilityOrder(member, semanticModel);
            _staticOrder = GetStaticOrder(member, semanticModel);
        }

        public static string[] FormatOrderDifference(SortOrder sortOrder1, SortOrder sortOrder2)
        {
            if (sortOrder1._declarationOrder != sortOrder2._declarationOrder) return FormatItems(sortOrder1._declarationOrder, sortOrder2._declarationOrder);
            if (sortOrder1._visibilityOrder != sortOrder2._visibilityOrder) return FormatItems(sortOrder1._visibilityOrder, sortOrder2._visibilityOrder);
            if (sortOrder1._staticOrder != sortOrder2._staticOrder) return FormatItems(sortOrder1._staticOrder, sortOrder2._staticOrder);
            return new string[0];
        }

        int IComparable<SortOrder>.CompareTo(SortOrder other)
        {
            return CompareOrder(_declarationOrder, other._declarationOrder)
                ?? CompareOrder(_visibilityOrder, other._visibilityOrder)
                ?? CompareOrder(_staticOrder, other._staticOrder)
                ?? 0;
        }

        internal int CompareTo(SortOrder prevSortOrder)
        {
            return ((IComparable<SortOrder>)this).CompareTo(prevSortOrder);
        }

        private static string[] FormatItems<T>(T? item11, T? item12) where T : struct
        {
            return new string[] { item11.ToString(), item12.ToString() };
        }

        private static int? CompareOrder<T>(T? order1, T? order2) where T : struct, IComparable
        {
            if (!order1.HasValue || !order2.HasValue) return null;
            var diff = order1.Value.CompareTo(order2.Value);
            if (diff == 0) return null;
            return diff;
        }

        private static DeclarationOrder? GetDeclarationOrder(MemberDeclarationSyntax member)
        {
            DeclarationOrder? order = null;
            switch (member.Kind())
            {
                case SyntaxKind.MethodDeclaration:
                    order = DeclarationOrder.Method;
                    break;
                case SyntaxKind.FieldDeclaration:
                    if ((member as FieldDeclarationSyntax).Modifiers.Any(SyntaxKind.ConstKeyword))
                        order = DeclarationOrder.Constant;
                    else
                        order = DeclarationOrder.Field;
                    break;
                case SyntaxKind.ConstructorDeclaration:
                    order = DeclarationOrder.Constructor;
                    break;
                case SyntaxKind.DestructorDeclaration:
                    order = DeclarationOrder.Destructor;
                    break;
                case SyntaxKind.PropertyDeclaration:
                    order = DeclarationOrder.Property;
                    break;
                case SyntaxKind.DelegateDeclaration:
                    order = DeclarationOrder.Delegate;
                    break;
                case SyntaxKind.EventDeclaration:
                    order = DeclarationOrder.Event;
                    break;
                case SyntaxKind.IndexerDeclaration:
                    order = DeclarationOrder.Indexer;
                    break;
                case SyntaxKind.InterfaceDeclaration:
                    order = DeclarationOrder.Interface;
                    break;
                case SyntaxKind.StructDeclaration:
                    order = DeclarationOrder.Struct;
                    break;
                case SyntaxKind.EnumDeclaration:
                    order = DeclarationOrder.Enum;
                    break;
                case SyntaxKind.ClassDeclaration:
                    order = DeclarationOrder.Class;
                    break;
            }

            return order;
        }

        private static VisibilityOrder? GetVisibilityOrder(MemberDeclarationSyntax member, SemanticModel semanticModel)
        {
            var acessibility = semanticModel.GetDeclaredSymbol(member)?.DeclaredAccessibility;
            switch (acessibility)
            {
                case Accessibility.Public:
                    return VisibilityOrder.Public;
                case Accessibility.Protected:
                    return VisibilityOrder.Protected;
                case Accessibility.ProtectedAndInternal:
                    return VisibilityOrder.ProtectedInternal;
                case Accessibility.Internal:
                    return VisibilityOrder.Internal;
                case Accessibility.Private:
                    return VisibilityOrder.Private;
            }
            return null;
        }

        private StaticOrder? GetStaticOrder(MemberDeclarationSyntax member, SemanticModel semanticModel)
        {
            var isStatic = semanticModel.GetDeclaredSymbol(member)?.IsStatic;
            if (isStatic == null) return null;
            return isStatic.Value ? StaticOrder.Static : StaticOrder.NonStatic;
        }

        // CG: For details on ordering: https://stackoverflow.com/a/310967/382040
        internal enum DeclarationOrder
        {
            Constant,
            Field,
            Constructor,
            Destructor,
            Delegate,
            Event,
            Enum,
            Interface,
            Property,
            Indexer,
            Method,
            Struct,
            Class
        }

        internal enum VisibilityOrder
        {
            Public,
            Internal,
            ProtectedInternal,
            Protected,
            Private
        }

        internal enum StaticOrder
        {
            Static,
            NonStatic,
        }

    }
}
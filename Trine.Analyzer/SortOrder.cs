using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Trine.Analyzer
{
    internal class SortOrder : IComparable<SortOrder>
    {
        private readonly Lazy<DeclarationOrder?> _declarationOrder;
        private readonly Lazy<VisibilityOrder?> _visibilityOrder;
        private readonly Lazy<StaticOrder?> _staticOrder;
        private readonly Lazy<int?> _interfaceOrder;
        private readonly Lazy<string?> _interfaceOrderName;

        public SortOrder(MemberDeclarationSyntax member, SemanticModel? semanticModel)
        {
            _declarationOrder = new Lazy<DeclarationOrder?>(() => GetDeclarationOrder(member));
            _visibilityOrder = new Lazy<VisibilityOrder?>(() => GetVisibilityOrder(member));
            _staticOrder = new Lazy<StaticOrder?>(() => GetStaticOrder(member));
            _interfaceOrder = new Lazy<int?>(() => semanticModel != null ?
                GetInterfaceOrder(member, semanticModel) : null);
            _interfaceOrderName = new Lazy<string?>(() => GetInterfaceOrderName(member, semanticModel));
        }

        // CG: For details on ordering: https://stackoverflow.com/a/310967/382040
        internal enum DeclarationOrder
        {
            Constant = 0,
            Field = 1,
            Constructor = 2,
            Destructor = 3,
            Delegate = 4,
            Event = 5,
            Enum = 6,
            Interface = 7,
            Property = 8,
            Indexer = 9,
            Method = 10,
            Struct = 11,
            Class = 12
        }

        internal enum VisibilityOrder
        {
            Public = 0,
            Internal = 1,
            Protected = 2,
            Private = 3
        }

        internal enum StaticOrder
        {
            Static = 0,
            NonStatic = 1
        }

        public DeclarationOrder? Declaration => _declarationOrder.Value;
        public bool IsKnown => _declarationOrder.Value != null
                               && _visibilityOrder.Value != null
                               && _staticOrder.Value != null;

        public static string?[] FormatOrderDifference(SortOrder sortOrder1, SortOrder sortOrder2)
        {
            if (sortOrder1._declarationOrder.Value != sortOrder2._declarationOrder.Value)
                return FormatItems(sortOrder1._declarationOrder, sortOrder2._declarationOrder);
            if (sortOrder1._visibilityOrder.Value != sortOrder2._visibilityOrder.Value)
                return FormatItems(sortOrder1._visibilityOrder, sortOrder2._visibilityOrder);
            if (sortOrder1._staticOrder.Value != sortOrder2._staticOrder.Value)
                return FormatItems(sortOrder1._staticOrder, sortOrder2._staticOrder);
            if (sortOrder1._interfaceOrder.Value != sortOrder2._interfaceOrder.Value)
                return new[] { sortOrder1._interfaceOrderName.Value, sortOrder2._interfaceOrderName.Value };
            return new string[0];
        }

        internal int CompareTo(SortOrder prevSortOrder)
        {
            return ((IComparable<SortOrder>)this).CompareTo(prevSortOrder);
        }

        private static string?[] FormatItems<T>(Lazy<T?> item1, Lazy<T?> item2) where T : struct
        {
            return new string?[] { item1.Value.ToString(), item2.Value.ToString() };
        }

        private static int? CompareOrder<T>(T? order1, T? order2) where T : struct, IComparable
        {
            if (!order1.HasValue || !order2.HasValue)
            {
                if (order2.HasValue) return 1;
                if (order1.HasValue) return -1;
                return 0;
            }

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
                    if ((member as FieldDeclarationSyntax)!.Modifiers.Any(SyntaxKind.ConstKeyword))
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

        private static VisibilityOrder? GetVisibilityOrder(MemberDeclarationSyntax member)
        {
            var modifiers = GetModifiers(member);
            foreach (var modifier in modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PublicKeyword)) return VisibilityOrder.Public;
                if (modifier.IsKind(SyntaxKind.InternalKeyword)) return VisibilityOrder.Internal;
                if (modifier.IsKind(SyntaxKind.ProtectedKeyword)) return VisibilityOrder.Protected;
                if (modifier.IsKind(SyntaxKind.PrivateKeyword)) return VisibilityOrder.Private;
            }

            return VisibilityOrder.Private;
        }

        private static SyntaxTokenList GetModifiers(MemberDeclarationSyntax member)
        {
            if (member is BaseMethodDeclarationSyntax method) return method.Modifiers;
            if (member is BaseFieldDeclarationSyntax field) return field.Modifiers;
            if (member is BasePropertyDeclarationSyntax property) return property.Modifiers;
            if (member is ClassDeclarationSyntax @class) return @class.Modifiers;
            if (member is EnumDeclarationSyntax @enum) return @enum.Modifiers;
            return new SyntaxTokenList();
        }

        private static string? GetInterfaceOrderName(MemberDeclarationSyntax member, SemanticModel? semanticModel)
        {
            if (!member.IsKind(SyntaxKind.MethodDeclaration) || semanticModel == null) return null;

            var methodSymbol = semanticModel.GetDeclaredSymbol(member);
            if (methodSymbol == null) return null;
            var classSymbol = methodSymbol.ContainingType;

            var @interface = classSymbol
                .AllInterfaces
                .FirstOrDefault(i =>
                    i.GetMembers().Any(m => classSymbol.FindImplementationForInterfaceMember(m) == methodSymbol)
                );

            // CG: For some reason the interface couldn't be found in all cases.
            // Couldn't figure out why, but for now just ignoring.
            if (@interface == null) return methodSymbol.Name;
            return @interface.Name + "." + methodSymbol.Name;
        }

        int IComparable<SortOrder>.CompareTo(SortOrder other)
        {
            return CompareOrder(_declarationOrder.Value, other._declarationOrder.Value)
                ?? CompareOrder(_visibilityOrder.Value, other._visibilityOrder.Value)
                ?? CompareOrder(_staticOrder.Value, other._staticOrder.Value)
                ?? CompareOrder(_interfaceOrder.Value, other._interfaceOrder.Value)
                ?? 0;
        }

        private StaticOrder? GetStaticOrder(MemberDeclarationSyntax member)
        {
            var isStatic = GetModifiers(member).Any(m => m.IsKind(SyntaxKind.StaticKeyword));
            return isStatic ? StaticOrder.Static : StaticOrder.NonStatic;
        }

        private int? GetInterfaceOrder(MemberDeclarationSyntax member, SemanticModel semanticModel)
        {
            if (!member.IsKind(SyntaxKind.MethodDeclaration)) return null;

            var methodSymbol = semanticModel.GetDeclaredSymbol(member);
            if (methodSymbol == null) return null;
            var classSymbol = methodSymbol.ContainingType;
            var allInterfaceMembers = classSymbol
                .AllInterfaces
                .SelectMany(i => i.GetMembers())
                .Select(i => classSymbol.FindImplementationForInterfaceMember(i));

            var interfaceOrder = allInterfaceMembers
                .TakeWhile(m => m != methodSymbol)
                .Count();

            return interfaceOrder;
        }

    }
}
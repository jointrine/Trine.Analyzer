using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Trine.Analyzer
{
    public static class SyntaxExtensions
    {
        public static ClassDeclarationSyntax AddSortedMembers(this ClassDeclarationSyntax classNode, MemberDeclarationSyntax member)
        {
            var updatedMembers = classNode.Members;
            var currentSortOrder = new SortOrder(member, null);
            var insertIndex = updatedMembers.IndexOf(otherMember => new SortOrder(otherMember, null).CompareTo(currentSortOrder) > 0);
            if (insertIndex == -1) insertIndex = updatedMembers.Count;
            updatedMembers = updatedMembers.Insert(insertIndex, member);
            return classNode.WithMembers(updatedMembers);
        }
    }
}
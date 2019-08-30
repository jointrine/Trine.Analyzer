using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Trine.Analyzer
{
    public static class SyntaxExtensions
    {
        public static ClassDeclarationSyntax AddSortedMembers(this ClassDeclarationSyntax classNode, MemberDeclarationSyntax member)
        {
            var updatedMembers = classNode.Members;
            var currentSortOrder = new SortOrder(member);
            var insertIndex = updatedMembers.IndexOf(otherMember => new SortOrder(otherMember).CompareTo(currentSortOrder) >= 0);
            if (insertIndex == -1) insertIndex = updatedMembers.Count;
            updatedMembers = updatedMembers.Insert(insertIndex, member);
            return classNode.WithMembers(updatedMembers);
        }
    }
}
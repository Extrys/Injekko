using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Injekko.Codegen
{
	public class InjekMethodReceiver : ISyntaxReceiver
	{
		public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

		public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
		{
			if (syntaxNode is not MethodDeclarationSyntax methodDeclaration)
				return;

			foreach (var attributeList in methodDeclaration.AttributeLists)
			{
				foreach (var attribute in attributeList.Attributes)
				{
					if (LooksLikeInjekAttribute(attribute.Name.ToString()))
					{
						CandidateMethods.Add(methodDeclaration);
						return;
					}
				}
			}
		}

		private static bool LooksLikeInjekAttribute(string attributeName)
			=> attributeName == "Injek"
			|| attributeName == "Injekko.Injek"
			|| attributeName == "InjekAttribute"
			|| attributeName == "Injekko.InjekAttribute";
	}
}

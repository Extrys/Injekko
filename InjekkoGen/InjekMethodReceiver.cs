using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Injekko.Codegen
{
	public class InjekMethodReceiver : ISyntaxReceiver
	{
		public List<MethodDeclarationSyntax> MethodsWithInjectAttribute { get; } = new List<MethodDeclarationSyntax>();

		public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
		{
			if (syntaxNode is MethodDeclarationSyntax methodDeclaration)
				foreach (var attributeList in methodDeclaration.AttributeLists)
					foreach (var attribute in attributeList.Attributes)
						if (IsInjekAttribute(attribute.Name.ToString()))
						{
							MethodsWithInjectAttribute.Add(methodDeclaration);
							break;
						}
		}
		private bool IsInjekAttribute(string attributeName) => attributeName == "Injek" || attributeName == "Injekko.Injek" || attributeName == "InjekAttribute" || attributeName == "Injekko.InjekAttribute";
	}
}
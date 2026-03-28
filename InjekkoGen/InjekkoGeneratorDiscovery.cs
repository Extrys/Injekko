using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Injekko.Codegen
{
	internal static class InjekkoGeneratorDiscovery
	{
		internal static bool IsCandidateInjekMethod(SyntaxNode node)
		{
			if (node is not MethodDeclarationSyntax methodDeclaration)
				return false; //Is not a method so earlty exit

			foreach (var attributeList in methodDeclaration.AttributeLists)
				foreach (var attribute in attributeList.Attributes)
					if (LooksLikeInjekAttribute(attribute.Name.ToString()))
						return true;

			return false;
		}

		internal static bool IsCandidateFucktoryTarget(SyntaxNode node)
		{
			if (node is not ClassDeclarationSyntax classDeclaration)
				return false; //Is not a class declaration so earlty exit

			foreach (var attributeList in classDeclaration.AttributeLists)
				foreach (var attribute in attributeList.Attributes)
					if (LooksLikeCreateFucktoryAttribute(attribute.Name.ToString()))
						return true;
			return false;
		}

		internal static IMethodSymbol GetAnnotatedMethod(GeneratorSyntaxContext context)
		{
			if (context.Node is not MethodDeclarationSyntax methodNode)
				return null;

			if (context.SemanticModel.GetDeclaredSymbol(methodNode) is not IMethodSymbol methodSymbol)
				return null;

			foreach (var attribute in methodSymbol.GetAttributes())
				if (attribute.AttributeClass?.ToDisplayString() == "Injekko.InjekAttribute")
					return methodSymbol;

			return null;
		}

		internal static FucktoryTargetModel GetFucktoryTarget(GeneratorSyntaxContext context)
		{
			if (context.Node is not ClassDeclarationSyntax classNode)
				return null;

			if (context.SemanticModel.GetDeclaredSymbol(classNode) is not INamedTypeSymbol targetType)
				return null;

			foreach (var attribute in targetType.GetAttributes())
			{
				if (attribute.AttributeClass?.ToDisplayString() != "Injekko.CreateFucktoryAttribute")
					continue;

				return new FucktoryTargetModel(targetType, ExtractRuntimeArgumentTypes(attribute));
			}

			return null;
		}

		internal static ImmutableArray<ITypeSymbol> ExtractRuntimeArgumentTypes(AttributeData attribute)
		{
			if (attribute.ConstructorArguments.IsDefaultOrEmpty)
				return ImmutableArray<ITypeSymbol>.Empty;

			var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();
			foreach (var argument in attribute.ConstructorArguments)
			{
				if (argument.Kind == TypedConstantKind.Array)
				{
					foreach (var value in argument.Values)
					{
						if (value.Value is ITypeSymbol typeValue)
							builder.Add(typeValue);
					}

					continue;
				}

				if (argument.Value is ITypeSymbol typeArgument)
					builder.Add(typeArgument);
			}

			return builder.ToImmutable();
		}

		internal static List<IMethodSymbol> DistinctMethods(ImmutableArray<IMethodSymbol> methods)
		{
			var result = new List<IMethodSymbol>(methods.Length);
			foreach (var method in methods)
			{
				if (method == null)
					continue;

				if (result.Any(existing => SymbolEqualityComparer.Default.Equals(existing, method)))
					continue;

				result.Add(method);
			}

			return result;
		}

		internal static List<FucktoryTargetModel> DistinctFucktories(ImmutableArray<FucktoryTargetModel> fucktories)
		{
			var result = new List<FucktoryTargetModel>(fucktories.Length);
			foreach (var fucktory in fucktories)
			{
				if (fucktory == null)
					continue;

				if (result.Any(existing => SymbolEqualityComparer.Default.Equals(existing.TargetType, fucktory.TargetType)))
					continue;

				result.Add(fucktory);
			}

			return result;
		}

		static bool LooksLikeInjekAttribute(string attributeName)
			=> attributeName == "Injek"
			|| attributeName == "Injekko.Injek"
			|| attributeName == "InjekAttribute"
			|| attributeName == "Injekko.InjekAttribute";

		static bool LooksLikeCreateFucktoryAttribute(string attributeName)
			=> attributeName == "CreateFucktory"
			|| attributeName == "Injekko.CreateFucktory"
			|| attributeName == "CreateFucktoryAttribute"
			|| attributeName == "Injekko.CreateFucktoryAttribute";
	}
}

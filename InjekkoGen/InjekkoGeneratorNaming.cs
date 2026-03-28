using Microsoft.CodeAnalysis;

namespace Injekko.Codegen
{
	internal static class InjekkoGeneratorNaming
	{
		internal static string GetFucktoryName(INamedTypeSymbol targetType) => targetType.Name + "_Fucktory";
		internal static string GetResolverName(INamedTypeSymbol targetType) => targetType.Name + "_Rizolver";

		internal static string GetFullyQualifiedFucktoryName(INamedTypeSymbol targetType)
		{
			var namespaceName = targetType.ContainingNamespace.ToDisplayString();
			var fucktoryName = GetFucktoryName(targetType);
			return targetType.ContainingNamespace.IsGlobalNamespace
				? "global::" + fucktoryName
				: "global::" + namespaceName + "." + fucktoryName;
		}

		internal static string GetFullyQualifiedResolverName(INamedTypeSymbol targetType)
		{
			var namespaceName = targetType.ContainingNamespace.ToDisplayString();
			var resolverName = GetResolverName(targetType);
			return targetType.ContainingNamespace.IsGlobalNamespace
				? "global::" + resolverName
				: "global::" + namespaceName + "." + resolverName;
		}

		internal static string TrimGlobal(string typeName) => typeName.StartsWith("global::") ? typeName.Substring("global::".Length) : typeName;
	}
}

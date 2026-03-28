using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Injekko.Codegen
{
	internal static class InjekkoGraphMetadataGeneration
	{
		internal static void AppendGraphMetadata(
			StringBuilder sourceBuilder,
			IEnumerable<IMethodSymbol> methods,
			IEnumerable<FucktoryTargetModel> fucktories)
		{
			var methodList = methods.ToList();
			var fucktoryList = fucktories.ToList();
			var runtimeArgumentMap = fucktoryList.ToDictionary(
				fucktory => fucktory.TargetType,
				fucktory => fucktory.RuntimeArgumentTypes,
				SymbolEqualityComparer.Default);

			sourceBuilder.AppendLine("public static class Injekko_GraphMetadata");
			sourceBuilder.AppendLine("{");
			sourceBuilder.AppendLine("\tpublic static global::Injekko.InjekGraphMetadata Create()");
			sourceBuilder.AppendLine("\t{");
			sourceBuilder.AppendLine("\t\treturn new global::Injekko.InjekGraphMetadata(");
			sourceBuilder.AppendLine("\t\t\tnew global::Injekko.InjekGraphTypeInfo[]");
			sourceBuilder.AppendLine("\t\t\t{");

			var allTypes = methodList.Select(method => method.ContainingType)
				.Concat(fucktoryList.Select(fucktory => fucktory.TargetType))
				.Distinct(SymbolEqualityComparer.Default)
				.ToList();

			foreach (var type in allTypes)
			{
				var hasInjek = methodList.Any(method => SymbolEqualityComparer.Default.Equals(method.ContainingType, type));
				var hasFucktory = fucktoryList.Any(fucktory => SymbolEqualityComparer.Default.Equals(fucktory.TargetType, type));
				sourceBuilder.AppendLine($"\t\t\t\tnew global::Injekko.InjekGraphTypeInfo(\"{Escape(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}\", {hasInjek.ToString().ToLowerInvariant()}, {hasFucktory.ToString().ToLowerInvariant()}),");
			}

			sourceBuilder.AppendLine("\t\t\t},");
			sourceBuilder.AppendLine("\t\t\tnew global::Injekko.InjekGraphDependencyInfo[]");
			sourceBuilder.AppendLine("\t\t\t{");

			foreach (var method in methodList)
			{
				runtimeArgumentMap.TryGetValue(method.ContainingType, out var runtimeArgumentTypes);
				runtimeArgumentTypes = runtimeArgumentTypes.IsDefault ? ImmutableArray<ITypeSymbol>.Empty : runtimeArgumentTypes;
				var runtimeArgumentStartIndex = method.Parameters.Length - runtimeArgumentTypes.Length;

				for (var parameterIndex = 0; parameterIndex < method.Parameters.Length; parameterIndex++)
				{
					var parameter = method.Parameters[parameterIndex];
					var isRuntimeArgument = parameterIndex >= runtimeArgumentStartIndex && runtimeArgumentTypes.Length > 0;
					var isFucktory = fucktoryList.Any(fucktory =>
						fucktory.FactoryName == parameter.Type.Name
						|| fucktory.FullyQualifiedFactoryName == parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

					sourceBuilder.AppendLine($"\t\t\t\tnew global::Injekko.InjekGraphDependencyInfo(\"{Escape(method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}\", \"{Escape(parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}\", \"{Escape(parameter.Name)}\", {isFucktory.ToString().ToLowerInvariant()}, {isRuntimeArgument.ToString().ToLowerInvariant()}),");
				}
			}

			sourceBuilder.AppendLine("\t\t\t},");
			sourceBuilder.AppendLine("\t\t\tnew global::Injekko.InjekGraphFucktoryInfo[]");
			sourceBuilder.AppendLine("\t\t\t{");

			foreach (var fucktory in fucktoryList)
			{
				sourceBuilder.AppendLine($"\t\t\t\tnew global::Injekko.InjekGraphFucktoryInfo(\"{Escape(fucktory.FullyQualifiedFactoryName)}\", \"{Escape(fucktory.TargetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}\", {fucktory.RuntimeArgumentTypes.Length}),");
			}

			sourceBuilder.AppendLine("\t\t\t});");
			sourceBuilder.AppendLine("\t}");
			sourceBuilder.AppendLine("}");
		}

		static string Escape(string value)
			=> value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}
}

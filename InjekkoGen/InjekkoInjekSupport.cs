using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Injekko.Codegen
{
	internal static class InjekkoInjekSupport
	{
		internal static void ReportDuplicateInjekMethods(SourceProductionContext context, IEnumerable<IMethodSymbol> methods)
		{
			var groupedByType = methods.GroupBy(m => m.ContainingType, SymbolEqualityComparer.Default);
			foreach (var group in groupedByType)
			{
				if (group.Count() <= 1)
					continue;

				foreach (var method in group)
				{
					context.ReportDiagnostic(Diagnostic.Create(
						InjekkoGeneratorDiagnostics.MultipleInjekMethodsRule,
						method.Locations.FirstOrDefault(),
						method.ContainingType.ToDisplayString()));
				}
			}
		}

		internal static bool TryValidateInjekMethod(SourceProductionContext context, IMethodSymbol methodSymbol)
		{
			if (methodSymbol.MethodKind != MethodKind.Ordinary)
				return ReportInvalid(context, methodSymbol, "it must be an ordinary instance method");

			if (methodSymbol.IsStatic)
				return ReportInvalid(context, methodSymbol, "static methods cannot act as pseudo-constructors");

			if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
				return ReportInvalid(context, methodSymbol, "it must be public so generated code can call it");

			if (methodSymbol.IsGenericMethod)
				return ReportInvalid(context, methodSymbol, "generic [Injek] methods are not supported");

			if (!methodSymbol.ReturnsVoid)
				return ReportInvalid(context, methodSymbol, "it must return void");

			if (methodSymbol.ContainingType == null)
				return ReportInvalid(context, methodSymbol, "it must belong to a named type");

			return true;
		}

		internal static IMethodSymbol FindInjectMethod(IEnumerable<IMethodSymbol> methods, INamedTypeSymbol targetType)
			=> methods.FirstOrDefault(method => SymbolEqualityComparer.Default.Equals(method.ContainingType, targetType));

		internal static bool HasMultipleInjekMethods(IEnumerable<IMethodSymbol> methods, INamedTypeSymbol targetType)
			=> methods.Count(method => SymbolEqualityComparer.Default.Equals(method.ContainingType, targetType)) > 1;

		internal static bool HasInjectableMethod(ITypeSymbol typeSymbol, INamedTypeSymbol injekAttributeSymbol)
		{
			var currentType = typeSymbol;
			while (currentType != null)
			{
				foreach (var method in currentType.GetMembers().OfType<IMethodSymbol>())
				{
					if (method.DeclaredAccessibility != Accessibility.Public || method.IsStatic)
						continue;

					if (method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, injekAttributeSymbol)))
						return true;
				}

				currentType = currentType.BaseType;
			}

			return false;
		}

		internal static void AppendResolver(
			StringBuilder sourceBuilder,
			Compilation compilation,
			INamedTypeSymbol injekAttributeSymbol,
			IMethodSymbol methodSymbol,
			List<FucktoryTargetModel> fucktories)
		{
			var associatedFucktory = InjekkoFucktoryGeneration.FindFucktoryForTarget(fucktories, methodSymbol.ContainingType);
			if (associatedFucktory != null
				&& associatedFucktory.RuntimeArgumentTypes.Length > 0
				&& InjekkoFucktoryGeneration.MatchesTrailingRuntimeArguments(methodSymbol, associatedFucktory.RuntimeArgumentTypes))
			{
				AppendResolverOverload(sourceBuilder, compilation, injekAttributeSymbol, methodSymbol, fucktories, associatedFucktory.RuntimeArgumentTypes);
				return;
			}

			AppendResolverOverload(sourceBuilder, compilation, injekAttributeSymbol, methodSymbol, fucktories, ImmutableArray<ITypeSymbol>.Empty);
		}

		static void AppendResolverOverload(
			StringBuilder sourceBuilder,
			Compilation compilation,
			INamedTypeSymbol injekAttributeSymbol,
			IMethodSymbol methodSymbol,
			List<FucktoryTargetModel> fucktories,
			ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
		{
			var classSymbol = methodSymbol.ContainingType;
			var className = classSymbol.Name;
			var classNamespace = classSymbol.ContainingNamespace.ToDisplayString();
			var globalNs = classSymbol.ContainingNamespace.IsGlobalNamespace;
			var parameters = methodSymbol.Parameters;
			var instanceTypeName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			var runtimeArgumentStartIndex = parameters.Length - runtimeArgumentTypes.Length;
			var extraSignature = BuildRuntimeArgumentSignature(runtimeArgumentTypes);
			var extraInvocation = BuildRuntimeArgumentInvocation(runtimeArgumentTypes.Length);

			if (!globalNs)
			{
				sourceBuilder.AppendLine($"namespace {classNamespace}");
				sourceBuilder.AppendLine("{");
			}

			sourceBuilder.AppendLine($"	public static class {className}_Rizolver");
			sourceBuilder.AppendLine("	{");
			if (runtimeArgumentTypes.Length == 0)
			{
				sourceBuilder.AppendLine($"		public static {instanceTypeName} Resolve(global::Injekko.IInjekScope scope)");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("			if(scope == null)");
				sourceBuilder.AppendLine("				throw new global::System.ArgumentNullException(nameof(scope));");
				sourceBuilder.AppendLine($"			var instance = scope.Resolve<{instanceTypeName}>();");
				sourceBuilder.AppendLine("			Activate(instance, scope);");
				sourceBuilder.AppendLine("			return instance;");
				sourceBuilder.AppendLine("		}");
				sourceBuilder.AppendLine();
			}

			sourceBuilder.AppendLine($"		public static void Injek(this {instanceTypeName} instance, global::Injekko.IInjekScope scope{extraSignature})");
			sourceBuilder.AppendLine("		{");
			sourceBuilder.AppendLine($"			Activate(instance, scope{extraInvocation});");
			sourceBuilder.AppendLine("		}");
			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine($"		public static void Activate(this {instanceTypeName} instance, global::Injekko.IInjekScope scope{extraSignature})");
			sourceBuilder.AppendLine("		{");
			sourceBuilder.AppendLine("			if(instance == null)");
			sourceBuilder.AppendLine("				throw new global::System.ArgumentNullException(nameof(instance));");
			sourceBuilder.AppendLine("			if(scope == null)");
			sourceBuilder.AppendLine("				throw new global::System.ArgumentNullException(nameof(scope));");
			sourceBuilder.AppendLine("			if(scope.IsActivated(instance))");
			sourceBuilder.AppendLine("				return;");
			sourceBuilder.AppendLine("			if(!scope.TryBeginActivation(instance))");
			sourceBuilder.AppendLine("				return;");
			sourceBuilder.AppendLine("			try");
			sourceBuilder.AppendLine("			{");

			for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
			{
				var parameter = parameters[parameterIndex];
				var parameterName = parameter.Name;

				if (parameterIndex >= runtimeArgumentStartIndex && runtimeArgumentTypes.Length > 0)
				{
					var runtimeArgumentIndex = parameterIndex - runtimeArgumentStartIndex + 1;
					sourceBuilder.AppendLine($"				var {parameterName} = arg{runtimeArgumentIndex};");
					continue;
				}

				if (TryAppendFucktoryParameterCreation(sourceBuilder, compilation, classSymbol, parameter, fucktories))
					continue;

				var paramType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				if (HasInjectableMethod(parameter.Type, injekAttributeSymbol))
					sourceBuilder.AppendLine($"				var {parameterName} = {paramType}_Rizolver.Resolve(scope);");
				else
					sourceBuilder.AppendLine($"				var {parameterName} = scope.Resolve<{paramType}>();");
			}

			sourceBuilder.AppendLine($"				instance.{methodSymbol.Name}({string.Join(", ", parameters.Select(p => p.Name))});");
			sourceBuilder.AppendLine("				scope.CompleteActivation(instance);");
			sourceBuilder.AppendLine("			}");
			sourceBuilder.AppendLine("			catch");
			sourceBuilder.AppendLine("			{");
			sourceBuilder.AppendLine("				scope.CancelActivation(instance);");
			sourceBuilder.AppendLine("				throw;");
			sourceBuilder.AppendLine("			}");
			sourceBuilder.AppendLine("		}");
			sourceBuilder.AppendLine("	}");

			if (!globalNs)
				sourceBuilder.AppendLine("}");
		}

		static bool TryAppendFucktoryParameterCreation(
			StringBuilder sourceBuilder,
			Compilation compilation,
			INamedTypeSymbol ownerType,
			IParameterSymbol parameter,
			List<FucktoryTargetModel> fucktories)
		{
			var fucktory = InjekkoFucktoryGeneration.FindFucktoryForParameter(fucktories, parameter.Type);
			if (fucktory == null)
				return false;

			var factoryTypeName = fucktory.FullyQualifiedFactoryName;
			if (InjekkoFucktoryGeneration.IsComponentType(compilation, fucktory.TargetType)
				&& InjekkoFucktoryGeneration.TryGetGameObjectType(ownerType, out _))
			{
				sourceBuilder.AppendLine($"				var {parameter.Name} = new {factoryTypeName}(instance.gameObject, scope);");
				return true;
			}

			sourceBuilder.AppendLine($"				var {parameter.Name} = new {factoryTypeName}(scope);");
			return true;
		}

		static string BuildRuntimeArgumentSignature(ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
		{
			if (runtimeArgumentTypes.Length == 0)
				return string.Empty;

			var builder = new StringBuilder();
			for (var index = 0; index < runtimeArgumentTypes.Length; index++)
			{
				builder.Append(", ");
				builder.Append(runtimeArgumentTypes[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
				builder.Append(' ');
				builder.Append("arg");
				builder.Append(index + 1);
			}

			return builder.ToString();
		}

		static string BuildRuntimeArgumentInvocation(int count)
		{
			if (count == 0)
				return string.Empty;

			return ", " + string.Join(", ", Enumerable.Range(1, count).Select(index => $"arg{index}"));
		}

		static bool ReportInvalid(SourceProductionContext context, IMethodSymbol methodSymbol, string reason)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				InjekkoGeneratorDiagnostics.InvalidInjekMethodRule,
				methodSymbol.Locations.FirstOrDefault(),
				methodSymbol.ToDisplayString(),
				reason));
			return false;
		}
	}
}

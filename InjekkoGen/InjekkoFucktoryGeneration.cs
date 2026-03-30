using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Injekko.Codegen
{
	internal static class InjekkoFucktoryGeneration
	{
		internal static bool TryValidateFucktory(SourceProductionContext context, Compilation compilation, List<IMethodSymbol> methods, FucktoryTargetModel fucktory, out FucktoryPlan plan)
		{
			plan = null;

			if (fucktory.TargetType.IsAbstract)
				return ReportInvalid(context, fucktory, "abstract types cannot generate a Fucktory");

			if (fucktory.RuntimeArgumentTypes.Length > 2)
				return ReportInvalid(context, fucktory, "v1 only supports Fucktories with up to 2 runtime arguments");

			if (InjekkoInjekSupport.HasMultipleInjekMethods(methods, fucktory.TargetType))
				return ReportInvalid(context, fucktory, "target type declares multiple [Injek] methods");

			var isComponentTarget = IsComponentType(compilation, fucktory.TargetType);
			var componentType = compilation.GetTypeByMetadataName("UnityEngine.Component") ?? compilation.GetTypeByMetadataName("Component");
			var gameObjectType = default(ITypeSymbol);

			if (isComponentTarget)
			{
				if (!TryGetGameObjectType(fucktory.TargetType, out gameObjectType))
					return ReportInvalid(context, fucktory, "component Fucktories require an accessible gameObject field or property");
			}
			else if (!HasAccessibleParameterlessConstructor(fucktory.TargetType))
			{
				return ReportInvalid(context, fucktory, "non-component Fucktories need an accessible parameterless constructor");
			}

			var injectMethod = InjekkoInjekSupport.FindInjectMethod(methods, fucktory.TargetType);
			if (fucktory.RuntimeArgumentTypes.Length > 0)
			{
				if (injectMethod == null || !MatchesTrailingRuntimeArguments(injectMethod, fucktory.RuntimeArgumentTypes))
				{
					context.ReportDiagnostic(Diagnostic.Create(InjekkoGeneratorDiagnostics.InvalidFucktoryArgumentsRule, fucktory.TargetType.Locations.FirstOrDefault(), fucktory.TargetType.ToDisplayString()));
					return false;
				}
			}

			plan = new FucktoryPlan(fucktory.TargetType, fucktory.RuntimeArgumentTypes, injectMethod, isComponentTarget, gameObjectType, componentType);
			return true;
		}

		internal static void AppendFucktory(StringBuilder sourceBuilder, FucktoryPlan plan)
		{
			var targetType = plan.TargetType;
			var namespaceName = targetType.ContainingNamespace.ToDisplayString();
			var globalNs = targetType.ContainingNamespace.IsGlobalNamespace;
			var targetTypeName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			var fucktoryName = InjekkoGeneratorNaming.GetFucktoryName(targetType);
			var interfaceType = BuildFucktoryInterfaceType(targetTypeName, plan.RuntimeArgumentTypes);
			var createSignature = BuildCreateMethodSignature(plan.RuntimeArgumentTypes);
			var createInvocation = BuildCreateArgumentInvocation(plan.RuntimeArgumentTypes.Length);

			if (!globalNs)
			{
				sourceBuilder.AppendLine($"namespace {namespaceName}");
				sourceBuilder.AppendLine("{");
			}

			sourceBuilder.AppendLine($"	public sealed partial class {fucktoryName} : {interfaceType}");
			sourceBuilder.AppendLine("	{");

			if (plan.IsComponentTarget)
			{
				var gameObjectTypeName = plan.GameObjectType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sourceBuilder.AppendLine($"		readonly {gameObjectTypeName} gameObject;");
				sourceBuilder.AppendLine($"		readonly {targetTypeName} prefab;");
				sourceBuilder.AppendLine("		readonly global::Injekko.IInjekScope scope;");
				sourceBuilder.AppendLine();
				sourceBuilder.AppendLine($"		public {fucktoryName}(global::Injekko.IInjekScope scope)");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("			this.scope = scope;");
				sourceBuilder.AppendLine("		}");
				sourceBuilder.AppendLine();
				sourceBuilder.AppendLine($"		public {fucktoryName}({gameObjectTypeName} gameObject)");
				sourceBuilder.AppendLine($"			: this(gameObject, global::Injekko.Unity.InjekScopeRegistry.GetScope(gameObject))");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("		}");
				sourceBuilder.AppendLine();
				sourceBuilder.AppendLine($"		public {fucktoryName}({gameObjectTypeName} gameObject, global::Injekko.IInjekScope scope)");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("			this.gameObject = gameObject;");
				sourceBuilder.AppendLine("			this.scope = scope;");
				sourceBuilder.AppendLine("		}");
				sourceBuilder.AppendLine();
				sourceBuilder.AppendLine($"		public {fucktoryName}({targetTypeName} prefab, global::Injekko.IInjekScope scope)");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("			this.prefab = prefab;");
				sourceBuilder.AppendLine("			this.scope = scope;");
				sourceBuilder.AppendLine("		}");
			}
			else
			{
				sourceBuilder.AppendLine("		readonly global::Injekko.IInjekScope scope;");
				sourceBuilder.AppendLine();
				sourceBuilder.AppendLine($"		public {fucktoryName}()");
				sourceBuilder.AppendLine("			: this(global::Injekko.Unity.InjekScopeRegistry.GetProjectScope())");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("		}");
				sourceBuilder.AppendLine();
				sourceBuilder.AppendLine($"		public {fucktoryName}(global::Injekko.IInjekScope scope)");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("			this.scope = scope;");
				sourceBuilder.AppendLine("		}");
			}

			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine($"		public {targetTypeName} Create({createSignature})");
			sourceBuilder.AppendLine("		{");
			sourceBuilder.AppendLine("			try");
			sourceBuilder.AppendLine("			{");
			sourceBuilder.AppendLine("				OnBeforeCreate();");

			if (plan.IsComponentTarget)
			{
				AppendComponentCreateBody(sourceBuilder, plan, targetTypeName, createInvocation);
			}
			else
			{
				sourceBuilder.AppendLine($"				var instance = new {targetTypeName}();");
				sourceBuilder.AppendLine("				OnAfterCreate(instance);");
				if (plan.InjekMethod != null)
				{
					var resolverTypeName = InjekkoGeneratorNaming.GetFullyQualifiedResolverName(targetType);
					sourceBuilder.AppendLine($"				{resolverTypeName}.Activate(instance, scope{createInvocation});");
				}
				sourceBuilder.AppendLine("				OnAfterActivate(instance);");
				sourceBuilder.AppendLine("				return instance;");
			}

			sourceBuilder.AppendLine("			}");
			sourceBuilder.AppendLine("			catch");
			sourceBuilder.AppendLine("			{");
			sourceBuilder.AppendLine("				throw;");
			sourceBuilder.AppendLine("			}");
			sourceBuilder.AppendLine("		}");
			sourceBuilder.AppendLine();

			if (plan.IsComponentTarget)
			{
				sourceBuilder.AppendLine($"		public static void BindPrefab(global::Injekko.IInjekBindingBuilder builder, {targetTypeName} prefab)");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("			if(builder == null)");
				sourceBuilder.AppendLine("				throw new global::System.ArgumentNullException(nameof(builder));");
				sourceBuilder.AppendLine("			builder.BindPrefab(prefab);");
				sourceBuilder.AppendLine("		}");
				sourceBuilder.AppendLine();
			}

			sourceBuilder.AppendLine("		partial void OnBeforeCreate();");
			sourceBuilder.AppendLine($"		partial void OnAfterCreate({targetTypeName} instance);");
			sourceBuilder.AppendLine($"		partial void OnAfterActivate({targetTypeName} instance);");
			sourceBuilder.AppendLine("	}");

			if (!globalNs)
				sourceBuilder.AppendLine("}");
		}

		static void AppendComponentCreateBody(StringBuilder sourceBuilder, FucktoryPlan plan, string targetTypeName, string createInvocation)
		{
			sourceBuilder.AppendLine($"				var prefabSource = prefab ?? (scope != null && scope.TryResolvePrefab<{targetTypeName}>(out var boundPrefab) ? boundPrefab : null);");
			sourceBuilder.AppendLine("				if (prefabSource != null)");
			sourceBuilder.AppendLine("				{");
			sourceBuilder.AppendLine("					var instance = global::Injekko.Unity.InjekHierarchyActivator.InstantiatePrefab(prefabSource, scope, global::Injekko.Unity.InjekGeneratedRuntimeRegistry.ActivateHierarchy);");
			if (plan.InjekMethod != null)
			{
				var resolverTypeName = InjekkoGeneratorNaming.GetFullyQualifiedResolverName(plan.TargetType);
				sourceBuilder.AppendLine($"					{resolverTypeName}.Activate(instance, scope{createInvocation});");
			}
			sourceBuilder.AppendLine("					OnAfterCreate(instance);");
			sourceBuilder.AppendLine("					OnAfterActivate(instance);");
			sourceBuilder.AppendLine("					return instance;");
			sourceBuilder.AppendLine("				}");
			sourceBuilder.AppendLine("				if (gameObject == null)");
			sourceBuilder.AppendLine($"					throw new global::Injekko.InjekException(\"No prefab binding found for {targetTypeName} and no host GameObject was provided for AddComponent creation.\");");
			sourceBuilder.AppendLine("				var deferredLifecycle = global::Injekko.Unity.InjekDeferredLifecycle.Begin(gameObject);");
			sourceBuilder.AppendLine("				try");
			sourceBuilder.AppendLine("				{");
			sourceBuilder.AppendLine($"					var instance = gameObject.AddComponent<{targetTypeName}>();");
			sourceBuilder.AppendLine("					OnAfterCreate(instance);");
			if (plan.InjekMethod != null)
			{
				var resolverTypeName = InjekkoGeneratorNaming.GetFullyQualifiedResolverName(plan.TargetType);
				sourceBuilder.AppendLine($"					{resolverTypeName}.Activate(instance, scope{createInvocation});");
			}
			sourceBuilder.AppendLine("					OnAfterActivate(instance);");
			sourceBuilder.AppendLine("					deferredLifecycle.Complete(instance);");
			sourceBuilder.AppendLine("					return instance;");
			sourceBuilder.AppendLine("				}");
			sourceBuilder.AppendLine("				catch");
			sourceBuilder.AppendLine("				{");
			sourceBuilder.AppendLine("					deferredLifecycle.Dispose();");
			sourceBuilder.AppendLine("					throw;");
			sourceBuilder.AppendLine("				}");
		}

		internal static FucktoryTargetModel FindFucktoryForTarget(IEnumerable<FucktoryTargetModel> fucktories, INamedTypeSymbol targetType)
			=> fucktories.FirstOrDefault(fucktory => SymbolEqualityComparer.Default.Equals(fucktory.TargetType, targetType));

		internal static FucktoryTargetModel FindFucktoryForParameter(IEnumerable<FucktoryTargetModel> fucktories, ITypeSymbol parameterType)
		{
			var parameterSimpleName = parameterType.Name;
			var parameterDisplayName = InjekkoGeneratorNaming.TrimGlobal(parameterType.ToDisplayString());
			var parameterFullName = InjekkoGeneratorNaming.TrimGlobal(parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

			foreach (var fucktory in fucktories)
			{
				if (fucktory.FactoryName == parameterSimpleName)
					return fucktory;

				if (fucktory.FullyQualifiedFactoryName == parameterDisplayName || fucktory.FullyQualifiedFactoryName == parameterFullName)
					return fucktory;
			}

			return null;
		}

		internal static bool MatchesTrailingRuntimeArguments(IMethodSymbol injectMethod, ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
		{
			if (injectMethod.Parameters.Length < runtimeArgumentTypes.Length)
				return false;

			var firstRuntimeParameterIndex = injectMethod.Parameters.Length - runtimeArgumentTypes.Length;
			for (var index = 0; index < runtimeArgumentTypes.Length; index++)
			{
				var injectParameterType = injectMethod.Parameters[firstRuntimeParameterIndex + index].Type;
				var runtimeArgumentType = runtimeArgumentTypes[index];
				if (!SymbolEqualityComparer.Default.Equals(injectParameterType, runtimeArgumentType))
					return false;
			}

			return true;
		}

		internal static bool IsComponentType(Compilation compilation, INamedTypeSymbol targetType)
		{
			var componentType =
				compilation.GetTypeByMetadataName("UnityEngine.Component")
				?? compilation.GetTypeByMetadataName("Component");
			if (componentType == null)
				return false;

			var currentType = targetType;
			while (currentType != null)
			{
				if (SymbolEqualityComparer.Default.Equals(currentType, componentType))
					return true;

				currentType = currentType.BaseType;
			}

			return false;
		}

		internal static bool TryGetGameObjectType(INamedTypeSymbol targetType, out ITypeSymbol gameObjectType)
		{
			var currentType = targetType;
			while (currentType != null)
			{
				foreach (var member in currentType.GetMembers("gameObject"))
				{
					if (member is IFieldSymbol fieldSymbol)
					{
						gameObjectType = fieldSymbol.Type;
						return true;
					}

					if (member is IPropertySymbol propertySymbol)
					{
						gameObjectType = propertySymbol.Type;
						return true;
					}
				}

				currentType = currentType.BaseType;
			}

			gameObjectType = null;
			return false;
		}

		static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol targetType)
		{
			if (targetType.TypeKind != TypeKind.Class)
				return false;

			if (targetType.InstanceConstructors.Length == 0)
				return true;

			foreach (var constructor in targetType.InstanceConstructors)
			{
				if (constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public)
					return true;
			}

			return false;
		}

		static string BuildCreateMethodSignature(ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
		{
			if (runtimeArgumentTypes.Length == 0)
				return string.Empty;

			return string.Join(", ", runtimeArgumentTypes.Select((typeSymbol, index) =>
				$"{typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} arg{index + 1}"));
		}

		static string BuildCreateArgumentInvocation(int count)
		{
			if (count == 0)
				return string.Empty;

			return ", " + string.Join(", ", Enumerable.Range(1, count).Select(index => $"arg{index}"));
		}

		static string BuildFucktoryInterfaceType(string targetTypeName, ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
		{
			if (runtimeArgumentTypes.Length == 0)
				return $"global::Injekko.IFucktory<{targetTypeName}>";

			if (runtimeArgumentTypes.Length == 1)
				return $"global::Injekko.IFucktory<{targetTypeName}, {runtimeArgumentTypes[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";

			return $"global::Injekko.IFucktory<{targetTypeName}, {runtimeArgumentTypes[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}, {runtimeArgumentTypes[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";
		}

		static bool ReportInvalid(SourceProductionContext context, FucktoryTargetModel fucktory, string reason)
		{
			context.ReportDiagnostic(Diagnostic.Create(InjekkoGeneratorDiagnostics.InvalidFucktoryRule, fucktory.TargetType.Locations.FirstOrDefault(), fucktory.TargetType.ToDisplayString(), reason));
			return false;
		}
	}
}

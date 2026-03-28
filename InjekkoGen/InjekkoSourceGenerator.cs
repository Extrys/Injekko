using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Injekko.Codegen
{
	[Generator]
	public sealed class InjekkoSourceGenerator : IIncrementalGenerator
	{
		static readonly DiagnosticDescriptor MissingAttributeRule = new(
			id: "INJEK001",
			title: "Missing Injek attribute definition",
			messageFormat: "Could not find Injekko.InjekAttribute in the compilation",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		static readonly DiagnosticDescriptor MultipleInjekMethodsRule = new(
			id: "INJEK002",
			title: "Only one [Injek] method is supported per type",
			messageFormat: "Type '{0}' declares multiple [Injek] methods, Keep a single pseudo-constructor method per type",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		static readonly DiagnosticDescriptor InvalidInjekMethodRule = new(
			id: "INJEK003",
			title: "Invalid [Injek] method",
			messageFormat: "Method '{0}' is not a supported [Injek] method: {1}",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		static readonly DiagnosticDescriptor MissingScopeRule = new(
			id: "INJEK004",
			title: "Missing IInjekScope definition",
			messageFormat: "Could not find Injekko.IInjekScope in the compilation",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		static readonly DiagnosticDescriptor InvalidFucktoryRule = new(
			id: "INJEK005",
			title: "Invalid [CreateFucktory] target",
			messageFormat: "Type '{0}' cannot generate a Fucktory: {1}",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		static readonly DiagnosticDescriptor InvalidFucktoryArgumentsRule = new(
			id: "INJEK006",
			title: "Invalid [CreateFucktory] runtime arguments",
			messageFormat: "Type '{0}' declares [CreateFucktory] runtime arguments that do not match the trailing [Injek] parameters",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var injekMethods = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => IsCandidateMethod(node),
					static (generatorContext, _) => GetAnnotatedMethod(generatorContext))
				.Where(static methodSymbol => methodSymbol != null)
				.Select(static (methodSymbol, _) => methodSymbol!);

			var fucktoryTargets = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => IsCandidateFucktoryTarget(node),
					static (generatorContext, _) => GetFucktoryTarget(generatorContext))
				.Where(static target => target != null)
				.Select(static (target, _) => target!);

			var compilationAndInjekMethods = context.CompilationProvider.Combine(injekMethods.Collect());
			var fullInput = compilationAndInjekMethods.Combine(fucktoryTargets.Collect());

			context.RegisterSourceOutput(fullInput, static (productionContext, source) =>
			{
				Execute(productionContext, source.Left.Left, source.Left.Right, source.Right);
			});
		}

		static bool IsCandidateMethod(SyntaxNode node)
		{
			if (node is not MethodDeclarationSyntax methodDeclaration)
				return false;

			foreach (var attributeList in methodDeclaration.AttributeLists)
			{
				foreach (var attribute in attributeList.Attributes)
				{
					if (LooksLikeInjekAttribute(attribute.Name.ToString()))
						return true;
				}
			}

			return false;
		}

		static bool IsCandidateFucktoryTarget(SyntaxNode node)
		{
			if (node is not ClassDeclarationSyntax classDeclaration)
				return false;

			foreach (var attributeList in classDeclaration.AttributeLists)
			{
				foreach (var attribute in attributeList.Attributes)
				{
					if (LooksLikeCreateFucktoryAttribute(attribute.Name.ToString()))
						return true;
				}
			}

			return false;
		}

		static IMethodSymbol? GetAnnotatedMethod(GeneratorSyntaxContext context)
		{
			if (context.Node is not MethodDeclarationSyntax methodNode)
				return null;

			if (context.SemanticModel.GetDeclaredSymbol(methodNode) is not IMethodSymbol methodSymbol)
				return null;

			foreach (var attribute in methodSymbol.GetAttributes())
			{
				if (attribute.AttributeClass?.ToDisplayString() == "Injekko.InjekAttribute")
					return methodSymbol;
			}

			return null;
		}

		static FucktoryTargetModel? GetFucktoryTarget(GeneratorSyntaxContext context)
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

		static ImmutableArray<ITypeSymbol> ExtractRuntimeArgumentTypes(AttributeData attribute)
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

		static void Execute(
			SourceProductionContext context,
			Compilation compilation,
			ImmutableArray<IMethodSymbol> candidateMethods,
			ImmutableArray<FucktoryTargetModel> candidateFucktories)
		{
			var injekAttributeSymbol = compilation.GetTypeByMetadataName("Injekko.InjekAttribute");
			if (injekAttributeSymbol == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(MissingAttributeRule, Location.None));
				return;
			}

			var injekScopeSymbol = compilation.GetTypeByMetadataName("Injekko.IInjekScope");
			if (injekScopeSymbol == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(MissingScopeRule, Location.None));
				return;
			}

			var methods = DistinctMethods(candidateMethods);
			var fucktories = DistinctFucktories(candidateFucktories);

			ReportDuplicateInjekMethods(context, methods);

			var sourceBuilder = new StringBuilder();
			foreach (var methodSymbol in methods)
			{
				if (HasMultipleInjekMethods(methods, methodSymbol.ContainingType))
					continue;

				if (!TryValidateInjekMethod(context, methodSymbol))
					continue;

				AppendResolver(sourceBuilder, compilation, injekAttributeSymbol, methodSymbol, fucktories);
			}

			foreach (var fucktory in fucktories)
			{
				if (!TryValidateFucktory(context, compilation, methods, fucktory, out var plan))
					continue;

				AppendFucktory(sourceBuilder, plan);
			}

			if (sourceBuilder.Length > 0)
				context.AddSource("InjekkoGenerated.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
		}

		static List<IMethodSymbol> DistinctMethods(ImmutableArray<IMethodSymbol> methods)
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

		static List<FucktoryTargetModel> DistinctFucktories(ImmutableArray<FucktoryTargetModel> fucktories)
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

		static void ReportDuplicateInjekMethods(SourceProductionContext context, IEnumerable<IMethodSymbol> methods)
		{
			var groupedByType = methods.GroupBy(m => m.ContainingType, SymbolEqualityComparer.Default);
			foreach (var group in groupedByType)
			{
				if (group.Count() <= 1)
					continue;

				foreach (var method in group)
				{
					context.ReportDiagnostic(Diagnostic.Create(
						MultipleInjekMethodsRule,
						method.Locations.FirstOrDefault(),
						method.ContainingType.ToDisplayString()));
				}
			}
		}

		static bool TryValidateInjekMethod(SourceProductionContext context, IMethodSymbol methodSymbol)
		{
			if (methodSymbol.MethodKind != MethodKind.Ordinary)
				return ReportInvalid(context, methodSymbol, "it must be an ordinary instance method");

			if (methodSymbol.IsStatic)
				return ReportInvalid(context, methodSymbol, "static methods cannot act as pseudo-constructors");

			if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
				return ReportInvalid(context, methodSymbol, "it must be public so generated code can call it");

			if (methodSymbol.IsGenericMethod)
				return ReportInvalid(context, methodSymbol, "generic [Injek] methods are not supported");

			if (methodSymbol.ReturnsVoid == false)
				return ReportInvalid(context, methodSymbol, "it must return void");

			if (methodSymbol.ContainingType == null)
				return ReportInvalid(context, methodSymbol, "it must belong to a named type");

			return true;

			static bool ReportInvalid(SourceProductionContext context, IMethodSymbol methodSymbol, string reason)
			{
				context.ReportDiagnostic(Diagnostic.Create(InvalidInjekMethodRule, methodSymbol.Locations.FirstOrDefault(), methodSymbol.ToDisplayString(), reason));
				return false;
			}
		}

		static bool TryValidateFucktory(
			SourceProductionContext context,
			Compilation compilation,
			List<IMethodSymbol> methods,
			FucktoryTargetModel fucktory,
			out FucktoryPlan plan)
		{
			plan = null;

			if (fucktory.TargetType.IsAbstract)
				return ReportInvalid("abstract types cannot generate a Fucktory");

			if (fucktory.RuntimeArgumentTypes.Length > 2)
				return ReportInvalid("v1 only supports Fucktories with up to 2 runtime arguments");

			if (HasMultipleInjekMethods(methods, fucktory.TargetType))
				return ReportInvalid("target type declares multiple [Injek] methods");

			var isComponentTarget = IsComponentType(compilation, fucktory.TargetType);
			var gameObjectType = default(ITypeSymbol);

			if (isComponentTarget)
			{
				if (!TryGetGameObjectType(fucktory.TargetType, out gameObjectType))
					return ReportInvalid("component Fucktories require an accessible gameObject field or property");
			}
			else if (!HasAccessibleParameterlessConstructor(fucktory.TargetType))
			{
				return ReportInvalid("non-component Fucktories need an accessible parameterless constructor");
			}

			var injectMethod = FindInjectMethod(methods, fucktory.TargetType);
			if (fucktory.RuntimeArgumentTypes.Length > 0)
			{
				if (injectMethod == null || !MatchesTrailingRuntimeArguments(injectMethod, fucktory.RuntimeArgumentTypes))
				{
					context.ReportDiagnostic(Diagnostic.Create(
						InvalidFucktoryArgumentsRule,
						fucktory.TargetType.Locations.FirstOrDefault(),
						fucktory.TargetType.ToDisplayString()));
					return false;
				}
			}

			plan = new FucktoryPlan(
				fucktory.TargetType,
				fucktory.RuntimeArgumentTypes,
				injectMethod,
				isComponentTarget,
				gameObjectType);
			return true;

			bool ReportInvalid(string reason)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					InvalidFucktoryRule,
					fucktory.TargetType.Locations.FirstOrDefault(),
					fucktory.TargetType.ToDisplayString(),
					reason));
				return false;
			}
		}

		static void AppendResolver(
			StringBuilder sourceBuilder,
			Compilation compilation,
			INamedTypeSymbol injekAttributeSymbol,
			IMethodSymbol methodSymbol,
			List<FucktoryTargetModel> fucktories)
		{
			var associatedFucktory = FindFucktoryForTarget(fucktories, methodSymbol.ContainingType);
			if (associatedFucktory != null && associatedFucktory.RuntimeArgumentTypes.Length > 0)
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

			if (!globalNs)
			{
				sourceBuilder.AppendLine($"namespace {classNamespace}");
				sourceBuilder.AppendLine("{");
			}

			sourceBuilder.AppendLine($"	public static class {className}_Rizolver");
			sourceBuilder.AppendLine("	{");

			var extraSignature = BuildRuntimeArgumentSignature(runtimeArgumentTypes);
			sourceBuilder.AppendLine($"		public static void Injek(this {instanceTypeName} instance, global::Injekko.IInjekScope scope{extraSignature})");
			sourceBuilder.AppendLine("		{");
			sourceBuilder.AppendLine("			if(instance == null)");
			sourceBuilder.AppendLine("				throw new global::System.ArgumentNullException(nameof(instance));");
			sourceBuilder.AppendLine("			if(scope == null)");
			sourceBuilder.AppendLine("				throw new global::System.ArgumentNullException(nameof(scope));");

			for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
			{
				var parameter = parameters[parameterIndex];
				var parameterName = parameter.Name;

				if (parameterIndex >= runtimeArgumentStartIndex && runtimeArgumentTypes.Length > 0)
				{
					var runtimeArgumentIndex = parameterIndex - runtimeArgumentStartIndex + 1;
					sourceBuilder.AppendLine($"			var {parameterName} = arg{runtimeArgumentIndex};");
					continue;
				}

				if (TryAppendFucktoryParameterCreation(sourceBuilder, compilation, classSymbol, parameter, fucktories))
					continue;

				var paramType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sourceBuilder.AppendLine($"			var {parameterName} = scope.Resolve<{paramType}>();");
				if (HasInjectableMethod(parameter.Type, injekAttributeSymbol))
					sourceBuilder.AppendLine($"			{paramType}_Rizolver.Injek({parameterName}, scope);");
			}

			sourceBuilder.AppendLine($"			instance.{methodSymbol.Name}({string.Join(", ", parameters.Select(p => p.Name))});");
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
			var fucktory = FindFucktoryForParameter(fucktories, parameter.Type);
			if (fucktory == null)
				return false;

			var factoryTypeName = fucktory.FullyQualifiedFactoryName;
			if (IsComponentType(compilation, fucktory.TargetType) && TryGetGameObjectType(ownerType, out _))
			{
				sourceBuilder.AppendLine($"			var {parameter.Name} = new {factoryTypeName}(instance.gameObject, scope);");
				return true;
			}

			if (!IsComponentType(compilation, fucktory.TargetType))
			{
				sourceBuilder.AppendLine($"			var {parameter.Name} = new {factoryTypeName}(scope);");
				return true;
			}

			return false;
		}

		static void AppendFucktory(StringBuilder sourceBuilder, FucktoryPlan plan)
		{
			var targetType = plan.TargetType;
			var namespaceName = targetType.ContainingNamespace.ToDisplayString();
			var globalNs = targetType.ContainingNamespace.IsGlobalNamespace;
			var targetTypeName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			var fucktoryName = GetFucktoryName(targetType);
			var interfaceType = BuildFucktoryInterfaceType(targetTypeName, plan.RuntimeArgumentTypes);
			var createSignature = BuildCreateMethodSignature(plan.RuntimeArgumentTypes);

			if (!globalNs)
			{
				sourceBuilder.AppendLine($"namespace {namespaceName}");
				sourceBuilder.AppendLine("{");
			}

			sourceBuilder.AppendLine($"	public sealed class {fucktoryName} : {interfaceType}");
			sourceBuilder.AppendLine("	{");

			if (plan.IsComponentTarget)
			{
				var gameObjectTypeName = plan.GameObjectType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sourceBuilder.AppendLine($"		readonly {gameObjectTypeName} gameObject;");
				sourceBuilder.AppendLine("		readonly global::Injekko.IInjekScope scope;");
				sourceBuilder.AppendLine();
				sourceBuilder.AppendLine($"		public {fucktoryName}({gameObjectTypeName} gameObject, global::Injekko.IInjekScope scope)");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("			this.gameObject = gameObject;");
				sourceBuilder.AppendLine("			this.scope = scope;");
				sourceBuilder.AppendLine("		}");
			}
			else
			{
				sourceBuilder.AppendLine("		readonly global::Injekko.IInjekScope scope;");
				sourceBuilder.AppendLine();
				sourceBuilder.AppendLine($"		public {fucktoryName}(global::Injekko.IInjekScope scope)");
				sourceBuilder.AppendLine("		{");
				sourceBuilder.AppendLine("			this.scope = scope;");
				sourceBuilder.AppendLine("		}");
			}

			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine($"		public {targetTypeName} Create({createSignature})");
			sourceBuilder.AppendLine("		{");

			if (plan.IsComponentTarget)
				sourceBuilder.AppendLine($"			var instance = gameObject.AddComponent<{targetTypeName}>();");
			else
				sourceBuilder.AppendLine($"			var instance = new {targetTypeName}();");

			if (plan.InjekMethod != null)
			{
				var resolverTypeName = GetFullyQualifiedResolverName(targetType);
				if (plan.RuntimeArgumentTypes.Length == 0)
					sourceBuilder.AppendLine($"			{resolverTypeName}.Injek(instance, scope);");
				else
					sourceBuilder.AppendLine($"			{resolverTypeName}.Injek(instance, scope, {BuildCreateArgumentInvocation(plan.RuntimeArgumentTypes.Length)});");
			}

			sourceBuilder.AppendLine("			return instance;");
			sourceBuilder.AppendLine("		}");
			sourceBuilder.AppendLine("	}");

			if (!globalNs)
				sourceBuilder.AppendLine("}");
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

		static string BuildCreateMethodSignature(ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
		{
			if (runtimeArgumentTypes.Length == 0)
				return string.Empty;

			return string.Join(", ", runtimeArgumentTypes.Select((typeSymbol, index) =>
				$"{typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} arg{index + 1}"));
		}

		static string BuildCreateArgumentInvocation(int count)
			=> string.Join(", ", Enumerable.Range(1, count).Select(index => $"arg{index}"));

		static string BuildFucktoryInterfaceType(string targetTypeName, ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
		{
			if (runtimeArgumentTypes.Length == 0)
				return $"global::Injekko.IFucktory<{targetTypeName}>";

			if (runtimeArgumentTypes.Length == 1)
				return $"global::Injekko.IFucktory<{targetTypeName}, {runtimeArgumentTypes[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";

			return $"global::Injekko.IFucktory<{targetTypeName}, {runtimeArgumentTypes[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}, {runtimeArgumentTypes[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>";
		}

		static bool HasInjectableMethod(ITypeSymbol typeSymbol, INamedTypeSymbol injekAttributeSymbol)
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

		static IMethodSymbol FindInjectMethod(IEnumerable<IMethodSymbol> methods, INamedTypeSymbol targetType)
			=> methods.FirstOrDefault(method => SymbolEqualityComparer.Default.Equals(method.ContainingType, targetType));

		static bool HasMultipleInjekMethods(IEnumerable<IMethodSymbol> methods, INamedTypeSymbol targetType)
			=> methods.Count(method => SymbolEqualityComparer.Default.Equals(method.ContainingType, targetType)) > 1;

		static FucktoryTargetModel FindFucktoryForTarget(IEnumerable<FucktoryTargetModel> fucktories, INamedTypeSymbol targetType)
			=> fucktories.FirstOrDefault(fucktory => SymbolEqualityComparer.Default.Equals(fucktory.TargetType, targetType));

		static FucktoryTargetModel FindFucktoryForParameter(IEnumerable<FucktoryTargetModel> fucktories, ITypeSymbol parameterType)
		{
			var parameterSimpleName = parameterType.Name;
			var parameterDisplayName = TrimGlobal(parameterType.ToDisplayString());
			var parameterFullName = TrimGlobal(parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

			foreach (var fucktory in fucktories)
			{
				if (fucktory.FactoryName == parameterSimpleName)
					return fucktory;

				if (fucktory.FullyQualifiedFactoryName == parameterDisplayName || fucktory.FullyQualifiedFactoryName == parameterFullName)
					return fucktory;
			}

			return null;
		}

		static bool MatchesTrailingRuntimeArguments(IMethodSymbol injectMethod, ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
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

		static bool IsComponentType(Compilation compilation, INamedTypeSymbol targetType)
		{
			var componentType = compilation.GetTypeByMetadataName("Component");
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

		static bool TryGetGameObjectType(INamedTypeSymbol targetType, out ITypeSymbol gameObjectType)
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

		static string GetFucktoryName(INamedTypeSymbol targetType) => targetType.Name + "_Fucktory";

		static string GetResolverName(INamedTypeSymbol targetType) => targetType.Name + "_Rizolver";

		static string GetFullyQualifiedFucktoryName(INamedTypeSymbol targetType)
		{
			var namespaceName = targetType.ContainingNamespace.ToDisplayString();
			var fucktoryName = GetFucktoryName(targetType);
			return targetType.ContainingNamespace.IsGlobalNamespace
				? "global::" + fucktoryName
				: "global::" + namespaceName + "." + fucktoryName;
		}

		static string GetFullyQualifiedResolverName(INamedTypeSymbol targetType)
		{
			var namespaceName = targetType.ContainingNamespace.ToDisplayString();
			var resolverName = GetResolverName(targetType);
			return targetType.ContainingNamespace.IsGlobalNamespace
				? "global::" + resolverName
				: "global::" + namespaceName + "." + resolverName;
		}

		static string TrimGlobal(string typeName)
			=> typeName.StartsWith("global::") ? typeName.Substring("global::".Length) : typeName;

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

		sealed class FucktoryTargetModel
		{
			public FucktoryTargetModel(INamedTypeSymbol targetType, ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
			{
				TargetType = targetType;
				RuntimeArgumentTypes = runtimeArgumentTypes;
			}

			public INamedTypeSymbol TargetType { get; }
			public ImmutableArray<ITypeSymbol> RuntimeArgumentTypes { get; }
			public string FactoryName => GetFucktoryName(TargetType);
			public string FullyQualifiedFactoryName => GetFullyQualifiedFucktoryName(TargetType);
		}

		sealed class FucktoryPlan
		{
			public FucktoryPlan(
				INamedTypeSymbol targetType,
				ImmutableArray<ITypeSymbol> runtimeArgumentTypes,
				IMethodSymbol injekMethod,
				bool isComponentTarget,
				ITypeSymbol gameObjectType)
			{
				TargetType = targetType;
				RuntimeArgumentTypes = runtimeArgumentTypes;
				InjekMethod = injekMethod;
				IsComponentTarget = isComponentTarget;
				GameObjectType = gameObjectType;
			}

			public INamedTypeSymbol TargetType { get; }
			public ImmutableArray<ITypeSymbol> RuntimeArgumentTypes { get; }
			public IMethodSymbol InjekMethod { get; }
			public bool IsComponentTarget { get; }
			public ITypeSymbol GameObjectType { get; }
		}
	}
}

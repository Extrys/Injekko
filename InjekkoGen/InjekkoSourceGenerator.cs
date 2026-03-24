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

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var injekMethods = context.SyntaxProvider
				.CreateSyntaxProvider(static (node, _) => IsCandidateMethod(node), static (generatorContext, _) => GetAnnotatedMethod(generatorContext))
				.Where(static methodSymbol => methodSymbol != null)
				.Select(static (methodSymbol, _) => methodSymbol);

			var compilationAndMethods = context.CompilationProvider.Combine(injekMethods.Collect());
			context.RegisterSourceOutput(compilationAndMethods, static (productionContext, source) => Execute(productionContext, source.Left, source.Right));
		}

		static bool IsCandidateMethod(SyntaxNode node)
		{
			if (node is not MethodDeclarationSyntax methodDeclaration)
				return false;

			foreach (var attributeList in methodDeclaration.AttributeLists)
				foreach (var attribute in attributeList.Attributes)
					if (LooksLikeInjekAttribute(attribute.Name.ToString()))
						return true;
			return false;
		}

		static IMethodSymbol GetAnnotatedMethod(GeneratorSyntaxContext context)
		{
			if (context.Node is not MethodDeclarationSyntax methodNode)
				return null;

			if (context.SemanticModel.GetDeclaredSymbol(methodNode) is not IMethodSymbol methodSymbol)
				return null;

			foreach (var attribute in methodSymbol.GetAttributes())
			{
				var attributeType = attribute.AttributeClass?.ToDisplayString();
				if (attributeType == "Injekko.InjekAttribute")
					return methodSymbol;
			}

			return null;
		}

		static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<IMethodSymbol> candidateMethods)
		{
			var injekAttributeSymbol = compilation.GetTypeByMetadataName("Injekko.InjekAttribute");
			if (injekAttributeSymbol == null) // checks that the attribute exists in the compilation
			{
				context.ReportDiagnostic(Diagnostic.Create(MissingAttributeRule, Location.None));
				return;
			}

			var injekScopeSymbol = compilation.GetTypeByMetadataName("Injekko.IInjekScope");
			if (injekScopeSymbol == null) // checks that the attribute exists in the compilation
			{
				context.ReportDiagnostic(Diagnostic.Create(MissingScopeRule, Location.None));
				return;
			}

			var methods = DistinctMethods(candidateMethods);
			ReportDuplicateInjekMethods(context, methods);

			var sourceBuilder = new StringBuilder();
			foreach (var methodSymbol in methods)
			{
				if (!TryValidateInjekMethod(context, methodSymbol))
					continue;

				AppendResolver(sourceBuilder, injekAttributeSymbol, methodSymbol);
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

				var alreadyAdded = result.Any(existing => SymbolEqualityComparer.Default.Equals(existing, method));
				if (!alreadyAdded)
					result.Add(method);
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
			if (methodSymbol.MethodKind != MethodKind.Ordinary) // a normal method, not a constructor, or whatever
				return ReportInvalid(context, methodSymbol, "it must be an ordinary instance method");

			if (methodSymbol.IsStatic)
				return ReportInvalid(context, methodSymbol, "static methods cannot act as pseudo-constructors");

			if (methodSymbol.DeclaredAccessibility != Accessibility.Public) //TODO: maybe allow internal methods if the generated code is in the same assembly? or do something to only be calleable from the generated code?
				return ReportInvalid(context, methodSymbol, "it must be public so generated code can call it");

			if (methodSymbol.IsGenericMethod)
				return ReportInvalid(context, methodSymbol, "generic [Injek] methods are not supported");

			if (methodSymbol.ReturnsVoid == false)
				return ReportInvalid(context, methodSymbol, "it must return void");

			if (methodSymbol.ContainingType == null) // for example free functions in a script, or something else weird
				return ReportInvalid(context, methodSymbol, "it must belong to a named type");

			return true;

			static bool ReportInvalid(SourceProductionContext context, IMethodSymbol methodSymbol, string reason)	 // shortener for reporting invalid methods easier
			{
				context.ReportDiagnostic(Diagnostic.Create(InvalidInjekMethodRule, methodSymbol.Locations.FirstOrDefault(), methodSymbol.ToDisplayString(), reason));
				return false;
			}
		}

		static void AppendResolver(StringBuilder sourceBuilder, INamedTypeSymbol injekAttributeSymbol, IMethodSymbol methodSymbol)
		{
			var classSymbol = methodSymbol.ContainingType;
			var className = classSymbol.Name;
			var classNamespace = classSymbol.ContainingNamespace.ToDisplayString();
			var globalNs = classSymbol.ContainingNamespace.IsGlobalNamespace; // to check if the namespace is needed in the generated code or not
			var parameters = methodSymbol.Parameters;
			var instanceTypeName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); // to avoid naming errors

			//also using global:: to avoid conflicts with user code that might have the same names as system types

			if (!globalNs) // generate namespace oppening 
			{
				sourceBuilder.AppendLine($"namespace {classNamespace}");
				sourceBuilder.AppendLine("{");
			}

			sourceBuilder.AppendLine($"	public static class {className}_Rizolver");
			sourceBuilder.AppendLine("	{");
			sourceBuilder.AppendLine($"		public static void Injek(this {instanceTypeName} instance, global::Injekko.IInjekScope scope)"); // the scope is passed as parameter now
			sourceBuilder.AppendLine("		{");
			sourceBuilder.AppendLine("			if(instance == null)");
			sourceBuilder.AppendLine("				throw new global::System.ArgumentNullException(nameof(instance));");
			sourceBuilder.AppendLine("			if(scope == null)");
			sourceBuilder.AppendLine("				throw new global::System.ArgumentNullException(nameof(scope));");

			//TODO: try using the new ``` ``` string interpolation syntax from C# 11 so its more readable

			foreach (var parameter in parameters)
			{
				var paramType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var paramName = parameter.Name;

				sourceBuilder.AppendLine($"			var {paramName} = scope.Resolve<{paramType}>();");
				if (HasInjectableMethod(parameter.Type, injekAttributeSymbol))
					sourceBuilder.AppendLine($"			{paramType}_Rizolver.Injek({paramName}, scope);");
			}

			sourceBuilder.AppendLine($"			instance.{methodSymbol.Name}({string.Join(", ", parameters.Select(p => p.Name))});");
			sourceBuilder.AppendLine("		}");
			sourceBuilder.AppendLine("	}");

			if (!globalNs)
				sourceBuilder.AppendLine("}");
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

		static bool LooksLikeInjekAttribute(string attributeName)
			=> attributeName == "Injek"
			|| attributeName == "Injekko.Injek"
			|| attributeName == "InjekAttribute"
			|| attributeName == "Injekko.InjekAttribute";
	}
}

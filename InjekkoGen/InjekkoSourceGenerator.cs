using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Injekko.Codegen
{
	[Generator]
	public class InjekkoSourceGenerator : ISourceGenerator
	{
		private static readonly DiagnosticDescriptor MissingAttributeRule = new(
			id: "INJEK001",
			title: "Missing Injek attribute definition",
			messageFormat: "Could not find Injekko.InjekAttribute in the compilation.",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		private static readonly DiagnosticDescriptor MultipleInjekMethodsRule = new(
			id: "INJEK002",
			title: "Only one [Injek] method is supported per type",
			messageFormat: "Type '{0}' declares multiple [Injek] methods. Keep a single pseudo-constructor method per type.",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		private static readonly DiagnosticDescriptor InvalidInjekMethodRule = new(
			id: "INJEK003",
			title: "Invalid [Injek] method",
			messageFormat: "Method '{0}' is not a supported [Injek] method: {1}",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		public void Initialize(GeneratorInitializationContext context)
		{
			context.RegisterForSyntaxNotifications(() => new InjekMethodReceiver());
		}

		public void Execute(GeneratorExecutionContext context)
		{
			if (context.SyntaxReceiver is not InjekMethodReceiver receiver)
				return;

			var compilation = context.Compilation;
			var injekAttributeSymbol = compilation.GetTypeByMetadataName("Injekko.InjekAttribute");
			if (injekAttributeSymbol == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(MissingAttributeRule, Location.None));
				return;
			}

			var methods = GetAnnotatedMethods(compilation, receiver.CandidateMethods, injekAttributeSymbol)
				.ToList();
			ReportDuplicateInjekMethods(context, methods);

			var sourceBuilder = new StringBuilder();
			foreach (var methodSymbol in methods)
			{
				if (!TryValidateInjekMethod(context, methodSymbol))
					continue;

				AppendResolver(context, sourceBuilder, injekAttributeSymbol, methodSymbol);
			}

			context.AddSource("InjekkoGenerated", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
		}

		private static IEnumerable<IMethodSymbol> GetAnnotatedMethods(
			Compilation compilation,
			IEnumerable<MethodDeclarationSyntax> candidateMethods,
			INamedTypeSymbol injekAttributeSymbol)
		{
			foreach (var method in candidateMethods)
			{
				var model = compilation.GetSemanticModel(method.SyntaxTree);
				if (model.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol)
					continue;

				if (methodSymbol.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, injekAttributeSymbol)))
					yield return methodSymbol;
			}
		}

		private static void ReportDuplicateInjekMethods(GeneratorExecutionContext context, List<IMethodSymbol> methods)
		{
			foreach (var group in methods.GroupBy(method => method.ContainingType, SymbolEqualityComparer.Default))
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

		private static bool TryValidateInjekMethod(GeneratorExecutionContext context, IMethodSymbol methodSymbol)
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
		}

		private static bool ReportInvalid(GeneratorExecutionContext context, IMethodSymbol methodSymbol, string reason)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidInjekMethodRule,
				methodSymbol.Locations.FirstOrDefault(),
				methodSymbol.ToDisplayString(),
				reason));
			return false;
		}

		private static void AppendResolver(
			GeneratorExecutionContext context,
			StringBuilder sourceBuilder,
			INamedTypeSymbol injekAttributeSymbol,
			IMethodSymbol methodSymbol)
		{
			var classSymbol = methodSymbol.ContainingType;
			var className = classSymbol.Name;
			var classNamespace = classSymbol.ContainingNamespace.ToDisplayString();
			var globalNs = classSymbol.ContainingNamespace.IsGlobalNamespace;
			var parameters = methodSymbol.Parameters;

			if (!globalNs)
			{
				sourceBuilder.AppendLine($"namespace {classNamespace}");
				sourceBuilder.AppendLine("{");
			}

			sourceBuilder.AppendLine($"    public static class {className}_Rizolver");
			sourceBuilder.AppendLine("    {");
			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine($"        public static void Injek(this {className} instance)");
			sourceBuilder.AppendLine("        {");

			AppendContainerLookup(context, sourceBuilder, classSymbol);
			sourceBuilder.AppendLine("                if(container == null)");
			sourceBuilder.AppendLine("                    throw new System.Exception(\"No SceneContext found in scene, please add one\");");

			foreach (var parameter in parameters)
			{
				var paramType = parameter.Type.ToDisplayString();
				var paramName = parameter.Name;

				sourceBuilder.AppendLine($"                var {paramName} = container.Resolve<{paramType}>();");
				if (HasInjectableMethod(parameter.Type, injekAttributeSymbol))
					sourceBuilder.AppendLine($"                {paramType}_Rizolver.Injek({paramName});");
			}

			sourceBuilder.AppendLine($"            instance.{methodSymbol.Name}({string.Join(", ", parameters.Select(p => p.Name))});");
			sourceBuilder.AppendLine("        }");
			sourceBuilder.AppendLine("    }");

			if (!globalNs)
				sourceBuilder.AppendLine("}");
		}

		private static void AppendContainerLookup(
			GeneratorExecutionContext context,
			StringBuilder sourceBuilder,
			INamedTypeSymbol classSymbol)
		{
			if (IsComponent(context, classSymbol))
			{
				sourceBuilder.AppendLine("                Context container = instance.GetComponent<Context>();");
				sourceBuilder.AppendLine("                if(container == null)");
				sourceBuilder.AppendLine("                    container = Project.CurrentScene.FindObjectOfType<SceneContext>();");
				return;
			}

			sourceBuilder.AppendLine("                Context container = Project.CurrentScene.FindObjectOfType<SceneContext>();");
		}

		private static bool HasInjectableMethod(ITypeSymbol typeSymbol, INamedTypeSymbol injekAttributeSymbol)
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

		private static bool IsComponent(in GeneratorExecutionContext context, INamedTypeSymbol classSymbol)
		{
			var componentType = context.Compilation.GetTypeByMetadataName("Component");
			if (componentType == null)
				return false;

			var baseType = classSymbol.BaseType;
			while (baseType != null)
			{
				if (SymbolEqualityComparer.Default.Equals(baseType, componentType))
					return true;

				baseType = baseType.BaseType;
			}

			return false;
		}
	}
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Injekko.Codegen
{
	[Generator]
	public class InjekkoSourceGenerator : ISourceGenerator
	{
		static readonly DiagnosticDescriptor MissingAttributeRule = new(
			id: "INJEK001",
			title: "Missing Injek attribute definition",
			messageFormat: "Could not find Injekko.InjekAttribute in the compilation.",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		static readonly DiagnosticDescriptor MultipleInjekMethodsRule = new(
			id: "INJEK002",
			title: "Only one [Injek] method is supported per type",
			messageFormat: "Type '{0}' declares multiple [Injek] methods. Keep a single pseudo-constructor method per type.",
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

		public void Initialize(GeneratorInitializationContext context) => context.RegisterForSyntaxNotifications(() => new InjekMethodReceiver());

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

			var methods = GetAnnotatedMethods(compilation, receiver.CandidateMethods, injekAttributeSymbol).ToList();
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

		static IEnumerable<IMethodSymbol> GetAnnotatedMethods(Compilation compilation, IEnumerable<MethodDeclarationSyntax> candidateMethods, INamedTypeSymbol injekAttributeSymbol)
		{
			foreach (var methodNode in candidateMethods)
			{
				var model = compilation.GetSemanticModel(methodNode.SyntaxTree);
				if (model.GetDeclaredSymbol(methodNode) is not IMethodSymbol methodSymbol)
					continue;

				if (methodSymbol.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, injekAttributeSymbol)))
					yield return methodSymbol;
			}
		}

		static void ReportDuplicateInjekMethods(GeneratorExecutionContext context, IEnumerable<IMethodSymbol> methods)
		{
			var groupedByType = methods.GroupBy(m => m.ContainingType);
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

		static bool TryValidateInjekMethod(GeneratorExecutionContext context, IMethodSymbol methodSymbol)
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

			static bool ReportInvalid(GeneratorExecutionContext context, IMethodSymbol methodSymbol, string reason) // shortener for reporting invalid methods easier
			{
				context.ReportDiagnostic(Diagnostic.Create(InvalidInjekMethodRule, methodSymbol.Locations.FirstOrDefault(), methodSymbol.ToDisplayString(), reason));
				return false;
			}
		}

		static void AppendResolver(GeneratorExecutionContext context, StringBuilder sourceBuilder, INamedTypeSymbol injekAttributeSymbol, IMethodSymbol methodSymbol)
		{
			var classSymbol = methodSymbol.ContainingType;
			var className = classSymbol.Name;
			var classNamespace = classSymbol.ContainingNamespace.ToDisplayString();
			var globalNs = classSymbol.ContainingNamespace.IsGlobalNamespace; // to check if the namespace is needed in the generated code or not
			var parameters = methodSymbol.Parameters;

			if (!globalNs) // generate namespace oppening 
			{
				sourceBuilder.AppendLine($"namespace {classNamespace}");
				sourceBuilder.AppendLine("{");
			}

			sourceBuilder.AppendLine($"	public static class {className}_Rizolver");
			sourceBuilder.AppendLine("	{");
			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine($"		public static void Injek(this {className} instance)");
			sourceBuilder.AppendLine("		{");

			AppendContainerLookup(context, sourceBuilder, classSymbol);
			sourceBuilder.AppendLine("			if(container == null)"); //If any container found then error time!
			sourceBuilder.AppendLine("				throw new System.Exception(\"No Context found, please add one, maybe you are missing a SceneContext component?\");");

			foreach (var parameter in parameters) // resolve each parameter recursively
			{
				var paramType = parameter.Type.ToDisplayString();
				var paramName = parameter.Name;

				sourceBuilder.AppendLine($"			var {paramName} = container.Resolve<{paramType}>();");
				if (HasInjectableMethod(parameter.Type, injekAttributeSymbol))
					sourceBuilder.AppendLine($"			{paramType}_Rizolver.Injek({paramName});");
			}

			sourceBuilder.AppendLine($"			instance.{methodSymbol.Name}({string.Join(", ", parameters.Select(p => p.Name))});");
			sourceBuilder.AppendLine("		}");
			sourceBuilder.AppendLine("	}");

			if (!globalNs)
				sourceBuilder.AppendLine("}");
		}

		private static void AppendContainerLookup(GeneratorExecutionContext context, StringBuilder sourceBuilder, INamedTypeSymbol classSymbol)
		{
			if (IsComponent(context, classSymbol)) //TODO: Fuck! i just noticed im using here Unity api, i might need to plan something to let the dev define custom lookups or so...
			{
				sourceBuilder.AppendLine("			Context container = instance.GetComponent<Context>();"); // look for local context
				sourceBuilder.AppendLine("			if(container == null)");
				sourceBuilder.AppendLine("				container = Project.CurrentScene.FindObjectOfType<SceneContext>();"); // then fallback to scene context
				return;
			}

			sourceBuilder.AppendLine("			Context container = Project.CurrentScene.FindObjectOfType<ProjectContext>();"); // fallback to project context on non components
			//TODO: maybe also add a custom container parameter to the method so factories can pass a specific container?
		}

		private static bool HasInjectableMethod(ITypeSymbol typeSymbol, INamedTypeSymbol injekAttributeSymbol)
		{
			var currentType = typeSymbol; // the parameter type
			while (currentType != null) 
			{
				foreach (var method in currentType.GetMembers().OfType<IMethodSymbol>()) // for each method in this type
				{
					if (method.DeclaredAccessibility != Accessibility.Public || method.IsStatic) // only consider public instance methods as injectable
						continue;

					if (method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, injekAttributeSymbol)))
						return true; // has injek method, so this type needs to be injected as well
				}

				currentType = currentType.BaseType; //if no method found, then check the base type, because maybe the pseudo-constructor is declared in a parent class...
			}

			return false;
		}

		static bool IsComponent(GeneratorExecutionContext context, INamedTypeSymbol classSymbol)
		{
			//TODO: Add some kind of way to configure this for non-Unity users (Engine agnostic)
			// Maybe make some way to override this for other engines in a custom way?
			// It would be cool to add some kind of template class that users can write down onto a special folder
			// because unity has the GetComponent thing but other engines might have different ways to look for a container
			// Definitelly i need to change this to UnityEngine.Component instead for unity testing
			var componentType = context.Compilation.GetTypeByMetadataName("Component"); // this is the class needed to check against the class symbol
			if (componentType == null)
				return false;

			var baseType = classSymbol.BaseType; // to check if class symbol inheriting the component type
			while (baseType != null)
			{
				if (SymbolEqualityComparer.Default.Equals(baseType, componentType))
					return true;

				baseType = baseType.BaseType; // fallback to next parent type if not found
			}

			return false;
		}
	}
}

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

		static readonly DiagnosticDescriptor MissingScopeRule = new(
			id: "INJEK004",
			title: "Missing IInjekScope definition",
			messageFormat: "Could not find Injekko.IInjekScope in the compilation.",
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

			var methods = GetAnnotatedMethods(compilation, receiver.CandidateMethods, injekAttributeSymbol).ToList();
			ReportDuplicateInjekMethods(context, methods);

			var sourceBuilder = new StringBuilder();
			foreach (var methodSymbol in methods)
			{
				if (!TryValidateInjekMethod(context, methodSymbol))
					continue;

				AppendResolver(sourceBuilder, injekAttributeSymbol, methodSymbol);
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
			sourceBuilder.AppendLine();
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
	}
}

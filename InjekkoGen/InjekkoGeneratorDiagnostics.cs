using Microsoft.CodeAnalysis;

namespace Injekko.Codegen
{
	internal static class InjekkoGeneratorDiagnostics
	{
		internal static readonly DiagnosticDescriptor MissingAttributeRule = new(
			id: "INJEK001",
			title: "Missing Injek attribute definition",
			messageFormat: "Could not find Injekko.InjekAttribute in the compilation",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		internal static readonly DiagnosticDescriptor MultipleInjekMethodsRule = new(
			id: "INJEK002",
			title: "Only one [Injek] method is supported per type",
			messageFormat: "Type '{0}' declares multiple [Injek] methods, Keep a single pseudo-constructor method per type",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		internal static readonly DiagnosticDescriptor InvalidInjekMethodRule = new(
			id: "INJEK003",
			title: "Invalid [Injek] method",
			messageFormat: "Method '{0}' is not a supported [Injek] method: {1}",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		internal static readonly DiagnosticDescriptor MissingScopeRule = new(
			id: "INJEK004",
			title: "Missing IInjekScope definition",
			messageFormat: "Could not find Injekko.IInjekScope in the compilation",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		internal static readonly DiagnosticDescriptor InvalidFucktoryRule = new(
			id: "INJEK005",
			title: "Invalid [CreateFucktory] target",
			messageFormat: "Type '{0}' cannot generate a Fucktory: {1}",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		internal static readonly DiagnosticDescriptor InvalidFucktoryArgumentsRule = new(
			id: "INJEK006",
			title: "Invalid [CreateFucktory] runtime arguments",
			messageFormat: "Type '{0}' declares [CreateFucktory] runtime arguments that do not match the trailing [Injek] parameters",
			category: "Injekko",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true);
	}
}

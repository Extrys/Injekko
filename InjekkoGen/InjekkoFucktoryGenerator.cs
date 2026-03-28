using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Injekko.Codegen
{
	[Generator]
	public sealed class InjekkoFucktoryGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var injekMethods = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => InjekkoGeneratorShared.IsCandidateMethod(node),
					static (generatorContext, _) => InjekkoGeneratorShared.GetAnnotatedMethod(generatorContext))
				.Where(static methodSymbol => methodSymbol != null)
				.Select(static (methodSymbol, _) => methodSymbol!);

			var fucktoryTargets = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => InjekkoGeneratorShared.IsCandidateFucktoryTarget(node),
					static (generatorContext, _) => InjekkoGeneratorShared.GetFucktoryTarget(generatorContext))
				.Where(static target => target != null)
				.Select(static (target, _) => target!);

			var compilationAndInjekMethods = context.CompilationProvider.Combine(injekMethods.Collect());
			var fullInput = compilationAndInjekMethods.Combine(fucktoryTargets.Collect());

			context.RegisterSourceOutput(fullInput, static (productionContext, source) =>
			{
				Execute(productionContext, source.Left.Left, source.Left.Right, source.Right);
			});
		}

		static void Execute(
			SourceProductionContext context,
			Compilation compilation,
			ImmutableArray<IMethodSymbol> candidateMethods,
			ImmutableArray<FucktoryTargetModel> candidateFucktories)
		{
			var injekScopeSymbol = compilation.GetTypeByMetadataName("Injekko.IInjekScope");
			if (injekScopeSymbol == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(InjekkoGeneratorShared.MissingScopeRule, Location.None));
				return;
			}

			var methods = InjekkoGeneratorShared.DistinctMethods(candidateMethods);
			var fucktories = InjekkoGeneratorShared.DistinctFucktories(candidateFucktories);

			var sourceBuilder = new StringBuilder();
			foreach (var fucktory in fucktories)
			{
				if (!InjekkoGeneratorShared.TryValidateFucktory(context, compilation, methods, fucktory, out var plan))
					continue;

				InjekkoGeneratorShared.AppendFucktory(sourceBuilder, plan);
			}

			if (sourceBuilder.Length > 0)
				context.AddSource("InjekkoFucktoriesGenerated.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
		}
	}
}

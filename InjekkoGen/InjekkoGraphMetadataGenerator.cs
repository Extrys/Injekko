using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Injekko.Codegen
{
	[Generator]
	public sealed class InjekkoGraphMetadataGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var injekMethods = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => InjekkoGeneratorDiscovery.IsCandidateInjekMethod(node),
					static (generatorContext, _) => InjekkoGeneratorDiscovery.GetAnnotatedMethod(generatorContext))
				.Where(static methodSymbol => methodSymbol != null)
				.Select(static (methodSymbol, _) => methodSymbol!);

			var fucktoryTargets = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => InjekkoGeneratorDiscovery.IsCandidateFucktoryTarget(node),
					static (generatorContext, _) => InjekkoGeneratorDiscovery.GetFucktoryTarget(generatorContext))
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
			if (compilation.GetTypeByMetadataName("Injekko.InjekGraphMetadata") == null)
				return;

			var methods = InjekkoGeneratorDiscovery.DistinctMethods(candidateMethods);
			var fucktories = InjekkoGeneratorDiscovery.DistinctFucktories(candidateFucktories);
			var sourceBuilder = new StringBuilder();
			InjekkoGraphMetadataGeneration.AppendGraphMetadata(sourceBuilder, methods, fucktories);
			context.AddSource("InjekkoGraphMetadata.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
		}
	}
}

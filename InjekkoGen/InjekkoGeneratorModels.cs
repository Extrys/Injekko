using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Injekko.Codegen
{
	internal sealed class FucktoryTargetModel
	{
		public FucktoryTargetModel(INamedTypeSymbol targetType, ImmutableArray<ITypeSymbol> runtimeArgumentTypes)
		{
			TargetType = targetType;
			RuntimeArgumentTypes = runtimeArgumentTypes;
		}

		public INamedTypeSymbol TargetType { get; }
		public ImmutableArray<ITypeSymbol> RuntimeArgumentTypes { get; }
		public string FactoryName => InjekkoGeneratorNaming.GetFucktoryName(TargetType);
		public string FullyQualifiedFactoryName => InjekkoGeneratorNaming.GetFullyQualifiedFucktoryName(TargetType);
	}

	internal sealed class FucktoryPlan
	{
		public FucktoryPlan(
			INamedTypeSymbol targetType,
			ImmutableArray<ITypeSymbol> runtimeArgumentTypes,
			IMethodSymbol injekMethod,
			bool isComponentTarget,
			ITypeSymbol gameObjectType,
			ITypeSymbol componentType)
		{
			TargetType = targetType;
			RuntimeArgumentTypes = runtimeArgumentTypes;
			InjekMethod = injekMethod;
			IsComponentTarget = isComponentTarget;
			GameObjectType = gameObjectType;
			ComponentType = componentType;
		}

		public INamedTypeSymbol TargetType { get; }
		public ImmutableArray<ITypeSymbol> RuntimeArgumentTypes { get; }
		public IMethodSymbol InjekMethod { get; }
		public bool IsComponentTarget { get; }
		public ITypeSymbol GameObjectType { get; }
		public ITypeSymbol ComponentType { get; }
	}
}

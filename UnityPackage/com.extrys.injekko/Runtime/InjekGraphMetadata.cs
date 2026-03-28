using System.Collections.Generic;

namespace Injekko
{
	public sealed class InjekGraphTypeInfo
	{
		public InjekGraphTypeInfo(string typeName, bool hasInjekMethod, bool hasFucktory)
		{
			TypeName = typeName;
			HasInjekMethod = hasInjekMethod;
			HasFucktory = hasFucktory;
		}

		public string TypeName { get; }
		public bool HasInjekMethod { get; }
		public bool HasFucktory { get; }
	}

	public sealed class InjekGraphDependencyInfo
	{
		public InjekGraphDependencyInfo(string ownerTypeName, string dependencyTypeName, string parameterName, bool isFucktory, bool isRuntimeArgument)
		{
			OwnerTypeName = ownerTypeName;
			DependencyTypeName = dependencyTypeName;
			ParameterName = parameterName;
			IsFucktory = isFucktory;
			IsRuntimeArgument = isRuntimeArgument;
		}

		public string OwnerTypeName { get; }
		public string DependencyTypeName { get; }
		public string ParameterName { get; }
		public bool IsFucktory { get; }
		public bool IsRuntimeArgument { get; }
	}

	public sealed class InjekGraphFucktoryInfo
	{
		public InjekGraphFucktoryInfo(string fucktoryName, string targetTypeName, int runtimeArgumentCount)
		{
			FucktoryName = fucktoryName;
			TargetTypeName = targetTypeName;
			RuntimeArgumentCount = runtimeArgumentCount;
		}

		public string FucktoryName { get; }
		public string TargetTypeName { get; }
		public int RuntimeArgumentCount { get; }
	}

	public sealed class InjekGraphMetadata
	{
		public InjekGraphMetadata(
			IReadOnlyList<InjekGraphTypeInfo> types,
			IReadOnlyList<InjekGraphDependencyInfo> dependencies,
			IReadOnlyList<InjekGraphFucktoryInfo> fucktories)
		{
			Types = types;
			Dependencies = dependencies;
			Fucktories = fucktories;
		}

		public IReadOnlyList<InjekGraphTypeInfo> Types { get; }
		public IReadOnlyList<InjekGraphDependencyInfo> Dependencies { get; }
		public IReadOnlyList<InjekGraphFucktoryInfo> Fucktories { get; }
	}
}

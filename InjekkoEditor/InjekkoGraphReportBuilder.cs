using Injekko.Unity;
using System.Text;

namespace Injekko.Editor
{
	public static class InjekkoGraphReportBuilder
	{
		public static string BuildReport(InjekGraphMetadata metadata)
		{
			StringBuilder builder = new();
			builder.AppendLine("Injekko Graph Report");
			builder.AppendLine("Types:");
			foreach (var type in metadata.Types)
				builder.AppendLine($"- {type.TypeName} | Injek: {type.HasInjekMethod} | Fucktory: {type.HasFucktory}");

			builder.AppendLine("Dependencies:");
			foreach (var dependency in metadata.Dependencies)
				builder.AppendLine($"- {dependency.OwnerTypeName} -> {dependency.DependencyTypeName} ({dependency.ParameterName})");

			builder.AppendLine("Fucktories:");
			foreach (var fucktory in metadata.Fucktories)
				builder.AppendLine($"- {fucktory.FucktoryName} => {fucktory.TargetTypeName} (runtime args: {fucktory.RuntimeArgumentCount})");

			builder.AppendLine("Active Scope Nodes:");
			foreach (var scope in InjekScopeRegistry.EnumerateScopes())
			{
				builder.AppendLine($"- {scope.Kind}: {scope.Name}");
				foreach (var binding in scope.Bindings)
					builder.AppendLine($"  - {binding.ServiceTypeName} [{binding.Lifetime}]");
			}

			return builder.ToString();
		}
	}
}

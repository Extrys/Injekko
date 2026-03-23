using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Injekko.Codegen
{
	[Generator]
	public class InjekkoSourceGenerator : ISourceGenerator
	{
		//Dictionary<>
		public void Initialize(GeneratorInitializationContext context)
		{
			context.RegisterForSyntaxNotifications(() => new InjekMethodReceiver());
		}

		public void Execute(GeneratorExecutionContext context)
		{
			var sourceBuilder = new StringBuilder();
			//var attributeBuilder = new StringBuilder();
			//attributeBuilder.AppendLine("using System;");
			//attributeBuilder.AppendLine("namespace Injekko");
			//attributeBuilder.AppendLine("{");
			//attributeBuilder.AppendLine("    [AttributeUsage(AttributeTargets.Method)]");
			//attributeBuilder.AppendLine("    internal class InjekAttribute : Attribute {}");
			//attributeBuilder.AppendLine("}");
			//SourceText attributeSource = SourceText.From(attributeBuilder.ToString(), Encoding.UTF8);
			//context.AddSource("InjekAttribute", attributeSource);
			var compilation = context.Compilation;
			var injekAttributeSymbol = compilation.GetTypeByMetadataName("Injekko.InjekAttribute");
			if (context.SyntaxReceiver is InjekMethodReceiver receiver)
			{
				foreach (var method in receiver.MethodsWithInjectAttribute)
				{
					var model = compilation.GetSemanticModel(method.SyntaxTree);
					var methodSymbol = model.GetDeclaredSymbol(method) as IMethodSymbol;
					if (methodSymbol == null)
						continue;

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
					StringBuilder parameterResolution = new StringBuilder();
					foreach (var parameter in parameters)
					{
						var paramTypeSymbol = parameter.Type;
						var paramName = parameter.Name;
						var paramType = paramTypeSymbol.ToDisplayString();
						bool hasInjekMethod = false;
						var currentType = paramTypeSymbol;
						while (currentType != null)
						{
							foreach (var mthd in currentType.GetMembers().OfType<IMethodSymbol>())
							{
								if (mthd.DeclaredAccessibility == Accessibility.Public)
								{
									foreach (var attribute in mthd.GetAttributes())
									{
										if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, injekAttributeSymbol))
										{
											hasInjekMethod = true;
											break;
										}
									}
								}
								if (hasInjekMethod)
									break;
							}
							if (hasInjekMethod)
								break;

							currentType = currentType.BaseType;
						}

						sourceBuilder.AppendLine($"		static {paramType} {paramName} = null;");

						parameterResolution.AppendLine($"				{paramName} = container.Resolve<{paramType}>();");
						if (hasInjekMethod)
							parameterResolution.AppendLine($"				{paramType}_Rizolver.Injek({paramName});");
					}

					sourceBuilder.AppendLine();
					sourceBuilder.AppendLine($"        public static void Injek(this {className} instance)");
					sourceBuilder.AppendLine("        {");
					if (IsComponent(context, classSymbol))
					{
						sourceBuilder.AppendLine($"				Context container = instance.GetComponent<Context>();");
						sourceBuilder.AppendLine($"				if(container == null)");
						sourceBuilder.AppendLine($"					container = Project.CurrentScene.FindObjectOfType<SceneContext>();");
					}
					else
					{
						sourceBuilder.AppendLine($"					Context container = Project.CurrentScene.FindObjectOfType<SceneContext>();");
					}
					sourceBuilder.AppendLine($"				if(container == null)");
					sourceBuilder.AppendLine($"					throw new System.Exception(\"No SceneContext found in scene, please add one\");");
					sourceBuilder.AppendLine(parameterResolution.ToString());										
					sourceBuilder.AppendLine($"			instance.{methodSymbol.Name}({string.Join(", ", parameters.Select(p => p.Name))});");
					sourceBuilder.AppendLine("        }"); 
					sourceBuilder.AppendLine("    }");

					if (!globalNs)
						sourceBuilder.AppendLine("}");
				}
			}

			SourceText source = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);
			context.AddSource("InjekkoGenerated", source);
		}

		static bool IsComponent(in GeneratorExecutionContext context, INamedTypeSymbol classSymbol)
		{
			var componentType = context.Compilation.GetTypeByMetadataName("Component");

			bool inheritsFromComponent = false;
			if (componentType != null)
			{
				var baseType = classSymbol.BaseType;
				while (baseType != null)
				{
					if (SymbolEqualityComparer.Default.Equals(baseType, componentType))
					{
						inheritsFromComponent = true;
						break;
					}
					baseType = baseType.BaseType;
				}
			}

			return inheritsFromComponent;
		}
	}
}

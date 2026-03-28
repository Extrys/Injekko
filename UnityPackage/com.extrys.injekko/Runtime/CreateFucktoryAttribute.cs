using System;

namespace Injekko
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class CreateFucktoryAttribute : Attribute
	{
		public Type[] RuntimeArgumentTypes { get; }

		public CreateFucktoryAttribute(params Type[] runtimeArgumentTypes)
		{
			RuntimeArgumentTypes = runtimeArgumentTypes ?? Array.Empty<Type>();
		}
	}
}

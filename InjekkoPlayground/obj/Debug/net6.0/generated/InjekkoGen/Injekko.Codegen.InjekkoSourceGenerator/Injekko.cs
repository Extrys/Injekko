using System;
namespace Injekko
{
	[AttributeUsage(AttributeTargets.Method)]
	internal class InjekAttribute : Attribute {}


	public static class Player_Rizolver
	{
		static DependencyA depA;
		static DependencyB depB;


		public static void Injek(this Player instance)
		{
			instance.Inject(depA, depB);
		}
	}
}

namespace Injekko
{
	public sealed class InjekBindingInfo
	{
		public InjekBindingInfo(string serviceTypeName, InjekLifetime lifetime)
		{
			ServiceTypeName = serviceTypeName;
			Lifetime = lifetime;
		}

		public string ServiceTypeName { get; }
		public InjekLifetime Lifetime { get; }
	}
}

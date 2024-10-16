using Injekko;

namespace Pepe
{
	public class DependencyC
	{
		public DependencyB depB;
		[Injek]
		public void Inject(DependencyB depB) => this.depB = depB;
		public string value;
		public DependencyC(string newVal)
		{
			value = newVal;
		}
	}

}





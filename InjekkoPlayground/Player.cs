using Injekko;
using Pepe;

public class Player : Component
{
	DependencyA depA;
	DependencyC depC;

	[Injek] 
	public void Construc(DependencyA depA, DependencyC depC)
	{
		this.depA = depA;
		this.depC = depC;
		Console.WriteLine("Player constructed");
		Console.WriteLine("DependencyA value: " + depA.value);
		Console.WriteLine("DependencyB value: " + depC.depB.value);
		Console.WriteLine("DependencyC value: " + depC.value);
	}

	public override void Awake()
	{
		Console.WriteLine("Player awake");
		this.Injek();
	}
}





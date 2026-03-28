using Injekko;
using Pepe;

[CreateFucktory]
public class Player : Component
{
	DependencyA depA;
	DependencyC depC;
	Bullet_Fucktory bulletFactory;
	SpecialBullet_Fucktory specialBulletFactory;

	[Injek]
	public void Construc(
		DependencyA depA,
		DependencyC depC,
		Bullet_Fucktory bulletFactory,
		SpecialBullet_Fucktory specialBulletFactory)
	{
		this.depA = depA;
		this.depC = depC;
		this.bulletFactory = bulletFactory;
		this.specialBulletFactory = specialBulletFactory;
		Console.WriteLine("Player constructed");
		Console.WriteLine("DependencyA value: " + depA.value);
		Console.WriteLine("DependencyB value: " + depC.depB.value);
		Console.WriteLine("DependencyC value: " + depC.value);
	}

	public override void Awake()
	{
		Console.WriteLine("Player awake");
	}

	void Shoot()
	{
		bulletFactory.Create();
		specialBulletFactory.Create(1, false);
	}
}

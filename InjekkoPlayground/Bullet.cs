using Injekko;

[CreateFucktory]
public class Bullet : Component
{
	VelocityProvider depV;

	[Injek]
	public void Construc(VelocityProvider depV)
	{
		this.depV = depV;
	}
}

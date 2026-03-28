using Injekko;

[CreateFucktory(typeof(int), typeof(bool))]
public class SpecialBullet : Component
{
	VelocityProvider depV;
	int damageLevel;
	bool isExplosive;

	[Injek]
	public void Construc(VelocityProvider depV, int damageLevel, bool isExplosive)
	{
		this.depV = depV;
		this.damageLevel = damageLevel;
		this.isExplosive = isExplosive;
	}
}

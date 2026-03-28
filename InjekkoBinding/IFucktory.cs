namespace Injekko
{
	public interface IFucktory<out T>
	{
		T Create();
	}

	public interface IFucktory<out T, in TArg1>
	{
		T Create(TArg1 arg1);
	}

	public interface IFucktory<out T, in TArg1, in TArg2>
	{
		T Create(TArg1 arg1, TArg2 arg2);
	}
}

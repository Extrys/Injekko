using System;

namespace Injekko
{
	public sealed class InjekException : Exception
	{
		public InjekException(string message) : base(message)
		{
		}
	}
}

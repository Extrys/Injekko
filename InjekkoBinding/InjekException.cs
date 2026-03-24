using System;

namespace Injekko
{
	public class InjekException : InvalidOperationException
	{
		public InjekException(string message) : base(message)
		{
		}
	}
}

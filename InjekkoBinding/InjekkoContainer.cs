//using System;
//using System.Collections.Generic;

//namespace Injekko
//{
//	public class InjekkoContainer
//	{
//		public InjekkoContainer Parent { get; set; }
//		private static Dictionary<Type, object> _bindings = new Dictionary<Type, object>();

//		public void Bind<TInterface, TImplementation>() where TImplementation : TInterface, new()
//		{
//			_bindings[typeof(TInterface)] = new TImplementation();
//		}

//		public void BindInstance<TInterface>(TInterface instance)
//		{
//			_bindings[typeof(TInterface)] = instance;
//		}

//		public TInterface Resolve<TInterface>()
//		{
//			if (_bindings.TryGetValue(typeof(TInterface), out object value))
//				return (TInterface)value;
//			else if (Parent != null)
//				return Parent.Resolve<TInterface>();
//			else
//				throw new Exception($"No binding found for {typeof(TInterface).Name}");
//		}
//	}
//}

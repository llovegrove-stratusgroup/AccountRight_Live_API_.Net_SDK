using Castle.DynamicProxy;

namespace Stratus.AccountRightExport.Data;

public class Proxy
{

	#region Methods

	public static T Create<T>() where T : class
	{

		ProxyGenerator pg = new();

		object proxy = pg.CreateClassProxy(
			typeof(T),
			new Type[]
			{
				typeof(INotifyPropertyChanged)
			},
			ProxyGenerationOptions.Default,
			new NotifierInterceptor());

		return (T)proxy;

	}

	#endregion

}
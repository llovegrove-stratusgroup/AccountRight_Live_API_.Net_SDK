using Castle.DynamicProxy;
using System.Reflection;

namespace Stratus.AccountRightExport.Data;

public class NotifierInterceptor : IInterceptor
{

	#region Constants

	private const string GetPrefix = "get_";

	private const string SetPrefix = "set_";

	#endregion

	#region Event Handlers

	private PropertyChangedEventHandler? _handler;

	#endregion

	#region Methods

	public PropertyInfo? GetProperty(IInvocation invocation)
	{

		Type? type = invocation.InvocationTarget?.GetType();
		return type?.GetProperty(invocation.Method.Name.Substring(SetPrefix.Length));

	}

	public void Intercept(IInvocation invocation)
	{

		try
		{

			if (invocation.Method.Name == "add_PropertyChanged")
			{

				_handler = (PropertyChangedEventHandler?)Delegate.Combine(
					_handler,
					(Delegate)invocation.Arguments[0]);

				invocation.ReturnValue = _handler;

			}

			if (invocation.Method.Name == "remove_PropertyChanged")
			{

				_handler = (PropertyChangedEventHandler?)Delegate.Remove(
					_handler,
					(Delegate)invocation.Arguments[0]);

				invocation.ReturnValue = _handler;

			}
			else
			{

				PropertyInfo? pi = GetProperty(invocation);
				if (pi is null) return;

				invocation.Proceed();
				if (invocation.Method.Name.StartsWith(SetPrefix))
				{

					invocation.Proceed();
					string propertyName = invocation.Method.Name.Substring(SetPrefix.Length);

					MethodInfo? mi = invocation
						.Proxy
						.GetType()
						.GetMethod("OnPropertyChanged");

					if (mi is not null)
						mi.Invoke(
							invocation.Proxy,
							new object[] { mi.Name.Substring(SetPrefix.Length) });

					if (_handler is not null)
						_handler(
							invocation.Proxy,
							new PropertyChangedEventArgs(
								invocation.Method.Name.Substring(SetPrefix.Length)));

				}

			}

		}
		catch
		{

			throw;

		}

	}

	#endregion

}
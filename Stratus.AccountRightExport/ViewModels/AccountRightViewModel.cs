using MYOB.AccountRight.SDK.Contracts.Version2;
using MYOB.AccountRight.SDK.Services;
using MYOB.AccountRight.SDK.Services.Sale;
using System.Reflection;
using System.ServiceProcess;
using static DevExpress.ReportServer.Printing.RemoteDocumentSource;

namespace Stratus.AccountRightExport.ViewModels;

public class AccountRightViewModel : ViewModel
{

	#region Construction & Finalization

	#endregion

	#region Methods

	public List<AccountRightServiceModel> GetAccountRightServices()
	{

		List<AccountRightServiceModel> serviceList = new();

		try
		{
			
			List<Assembly> assemblyList = AppDomain
				.CurrentDomain
				.GetAssemblies()
				.Where(a => a.FullName is not null && a.FullName.Contains("MYOB.AccountRight.SDK"))
				.ToList();

			foreach (Assembly assembly in assemblyList)
			{

				List<Type> assemblyTypeList = assembly
					.GetTypes()
					.Where(
						t =>
							(!t.IsAbstract) &&
							(t.Namespace is not null) &&
							(t.FullName is not null) &&
							(t.Namespace.StartsWith("MYOB.AccountRight.SDK.Services")) &&
							(!t.Namespace.Contains("Report")) &&
							(t.FullName.EndsWith("Service")))
					.ToList();

				foreach (Type type in assemblyTypeList)
				{

					if (type.FullName is null) continue;

					List<MethodInfo> typeMethodList = type
						.GetMethods()
						.Where(m =>
							(
								(m.Name == "Get") ||
								(m.Name == "GetAsync") ||
								(m.Name == "GetRange") ||
								(m.Name == "GetRangeAsync")
							) &&
							(m.Name != "GetType"))
						.OrderBy(e => e.Name)
						.ToList();

					if (typeMethodList.Count > 0)
					{

						if (typeMethodList.Any(e => e.Name == "Get"))
						{

							AccountRightServiceModel serviceModel = Proxy.Create<AccountRightServiceModel>();
							serviceModel.ServiceType = type.FullName;
							serviceModel.ContractType = typeMethodList
								.FirstOrDefault(e =>
									(e.ReturnParameter.ParameterType?.FullName?.StartsWith("MYOB") == true) &&
									(e.ReturnParameter.ParameterType?.IsArray == false))?
								.ReturnParameter
								.ParameterType?
								.FullName;

							if (serviceModel.ContractType is not null)
								serviceList.Add(serviceModel);

						}
						else
						{

							AccountRightServiceModel serviceModel = Proxy.Create<AccountRightServiceModel>();
							serviceModel.ServiceType = type.FullName;

							MethodInfo? mi = typeMethodList
								.FirstOrDefault(e =>
									(e.ReturnParameter.ParameterType?.FullName?.StartsWith("MYOB") == true) &&
									(e.ReturnParameter.ParameterType?.IsArray == false));

							if (mi is not null)
							{

								bool isPagedCollectionType =
									(mi.ReturnParameter.ParameterType?.IsGenericType ?? false) &&
									mi.ReturnParameter.ParameterType?.GetGenericTypeDefinition() == typeof(PagedCollection<>);

								if (isPagedCollectionType)
								{

									Type? internalType = mi
										.ReturnParameter
										.ParameterType?
										.GetGenericArguments()
										.FirstOrDefault();

									if (internalType is not null)
									{

										serviceModel.ContractType = internalType.FullName;

									}

									serviceList.Add(serviceModel);

								}

							}

						}

					}

				}

			}

		}
		catch
		{

			throw;

		}

		return serviceList.Where(e => !e.ContractName.Contains("EmployeePaymentSummary") && !e.ContractName.Contains("Payroll")).ToList();

	}

	#endregion

}
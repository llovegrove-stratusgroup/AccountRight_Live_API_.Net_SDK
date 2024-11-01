using MYOB.AccountRight.SDK.Contracts;

namespace Stratus.AccountRightExport.Data.Models;

public class AccountRightReadParametersModel
{

	public CompanyFile AccountRightCompany { get; set; } = default!;

	public CompanyFileModel CompanyFile { get; set; } = default!;

	public DatabaseModel Database { get; set; } = default!;

	public List<AccountRightServiceModel> Services { get; set; } = default!;

}
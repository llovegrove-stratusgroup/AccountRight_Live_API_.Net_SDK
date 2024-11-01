namespace Stratus.AccountRightExport.Data.Models;

public class AccountRightServiceModel
{

	public Type? Contract { get; set; }

	public string ContractName => ContractType?
		.Split(".")
		.LastOrDefault() ?? string.Empty;

	public virtual string? ContractType { get; set; } = default!;

	public virtual int ReadProgress { get; set; } = 0;

	public string ServiceName => ServiceType
		.Split(".")
		.LastOrDefault() ?? string.Empty;

	public virtual string ServiceType { get; set; } = default!;

	public virtual int WriteProgress { get; set; } = 0;

}
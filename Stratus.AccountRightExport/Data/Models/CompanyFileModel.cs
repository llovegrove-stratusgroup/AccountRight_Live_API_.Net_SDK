namespace Stratus.AccountRightExport.Data.Models;

public class CompanyFileModel
{

	#region Properties

	public virtual string? Password { get; set; } = "peggy";

	public virtual string? ServerAddress { get; set; } = "http://localhost:8080/accountright";

	public virtual string? UserName { get; set; } = "Administrator";

	#endregion

}
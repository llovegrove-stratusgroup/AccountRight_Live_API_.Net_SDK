namespace Stratus.AccountRightExport.Data.Models;

public class DatabaseModel
{

	#region Properties

	public string? ConnectionString => $"Data Source={SqlServerInstance};Initial Catalog={DatabaseName};User ID={UserID};Password={Password};Persist Security Info=true;TrustServerCertificate=true";

	public virtual string? DatabaseName { get; set; } = "ConversionTest";

	public virtual string? Password { get; set; } = "ExoAdmin";

	public virtual string? SqlServerInstance { get; set; } = ".\\SQL17";

	public virtual string? UserID { get; set; } = "sa";

	#endregion

}
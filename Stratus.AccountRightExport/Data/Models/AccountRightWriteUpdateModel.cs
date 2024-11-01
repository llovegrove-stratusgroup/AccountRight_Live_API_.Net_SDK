namespace Stratus.AccountRightExport.Data.Models;

public class AccountRightWriteUpdateModel
{

	public virtual AccountRightServiceModel Service { get; set; } = default!;

	public virtual long TotalRecordsAvailable { get; set; }

	public virtual long TotalRecordsWritten { get; set; }

}

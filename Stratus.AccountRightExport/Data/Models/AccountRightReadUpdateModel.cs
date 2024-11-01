namespace Stratus.AccountRightExport.Data.Models;

public class AccountRightReadUpdateModel
{

	#region Properties

	public virtual AccountRightServiceModel Service { get; set; } = default!;

	public virtual List<object> Records { get; set; } = new();

	public virtual long TotalRecordsAvailable { get; set; }

	public virtual long TotalRecordsExtracted { get; set; }

	#endregion

}
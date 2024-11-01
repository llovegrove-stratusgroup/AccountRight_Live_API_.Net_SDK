namespace Stratus.AccountRightExport.Data.Models;

public class AccountRightQueueItemModel
{

	public QueueItemType ItemType { get; set; }

	public List<object> Records { get; set; } = new();

	public AccountRightServiceModel Service { get; set; } = default!;

	public virtual long TotalRecordsAvailable { get; set; }

	public virtual long TotalRecordsWritten { get; set; }

}
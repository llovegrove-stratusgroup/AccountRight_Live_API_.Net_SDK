namespace Stratus.AccountRightExport.Data.Models;

public class ProcessingStatusModel
{

	#region Properties

	public virtual string? CurrentAction { get; set; }

	public virtual int CurrentValue { get; set; }

	public virtual int MinimumValue { get; set; }

	public virtual int MaximumValue { get; set; }

	#endregion

}
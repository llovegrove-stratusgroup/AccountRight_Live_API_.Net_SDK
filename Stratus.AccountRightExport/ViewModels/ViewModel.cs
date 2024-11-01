namespace Stratus.AccountRightExport.ViewModels;

public abstract class ViewModel : ViewModelBase
{

	#region Services

	public IMessageBoxService MessageBoxService => GetService<IMessageBoxService>();

	#endregion

	#region Properties

	public static Autofac.IContainer Container { get; set; } = null!;

	public Proxy Proxy { get; } = new();

	#endregion

}
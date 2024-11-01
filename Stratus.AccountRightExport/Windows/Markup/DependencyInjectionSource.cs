using System.Windows.Markup;

namespace Stratus.AccountRightExport.Windows.Markup;

public sealed class DependencyInjectionSource : MarkupExtension
{

	#region Properties

	public static Func<Type, object>? Resolver { get; set; }

	public Type? Type { get; set; }

	#endregion

	#region Methods

	public override object? ProvideValue(IServiceProvider serviceProvider) =>
		(Type is null) ? null : Resolver?.Invoke(Type);

	#endregion

}
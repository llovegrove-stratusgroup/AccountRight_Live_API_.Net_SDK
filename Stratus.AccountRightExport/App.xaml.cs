using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace Stratus.AccountRightExport;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{

	#region Methods

	protected override void OnStartup(StartupEventArgs e)
	{
		
		base.OnStartup(e);

		ThemedWindow.RoundCorners = true;

		ContainerBuilder builder = new();
		builder.RegisterAssemblyTypes(typeof(ViewModel).Assembly)
			.Where(t =>
				t.Name.ToUpper() != "VIEWMODEL" &&
				t.Name.ToUpper().EndsWith("VIEWMODEL"));

		Autofac.IContainer container = builder.Build();
		DependencyInjectionSource.Resolver = (type) =>
		{

			return container.Resolve(type);

		};

		ViewModel.Container = container;
		MainWindow window = new();
		Current.MainWindow = window;
		window.Show();

	}

	#endregion

}
using Microsoft.Extensions.DependencyInjection;
using DevExpress.Maui.Core;

namespace Slyv.Maui
{
	public partial class App : Application
	{
		private readonly IServiceProvider _serviceProvider;

		public App(IServiceProvider serviceProvider)
		{
			Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Mzg2NjkxNUAzMjM5MmUzMDJlMzAzYjMzMzMzYmxXZXVSOVVsRWovZ2U2L1U5YytOWDRlRlA4YWk1RlM3TnhKMmtET3A4MUk9");

			// CRITICAL: Initialize DevExpress ThemeManager BEFORE creating any controls
			ThemeManager.ApplyThemeToSystemBars = true;

			InitializeComponent();
			_serviceProvider = serviceProvider;
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			// Now it's safe to create AppShell which will create pages with DevExpress controls
			var appShell = _serviceProvider.GetRequiredService<AppShell>();
			return new Window(appShell);
		}
	}
}
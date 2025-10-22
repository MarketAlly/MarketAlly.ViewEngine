using Slyv.Maui.Views;

namespace Slyv.Maui
{
	public partial class AppShell : Shell
	{
		public AppShell(Overview overview, ChatPage chatPage, BrowserPage browserPage, SettingsPage settingsPage)
		{
			InitializeComponent();

			// Set singleton page instances directly to persist state
			OverviewContent.Content = overview;
			ChatShellContent.Content = chatPage;
			BrowserShellContent.Content = browserPage;
			SettingsShellContent.Content = settingsPage;
		}
	}
}

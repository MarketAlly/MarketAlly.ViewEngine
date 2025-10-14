using Android.App;
using Android.Content.PM;
using Android.OS;

namespace TestApp
{
	[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
	public class MainActivity : MauiAppCompatActivity
	{
		protected override void OnCreate(Bundle? savedInstanceState)
		{
			// Clear saved instance state to prevent fragment restoration errors
			base.OnCreate(null);
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			// Don't save instance state - prevents fragment restoration issues
			// Comment out the base call to prevent state saving
			// base.OnSaveInstanceState(outState);
		}
	}
}

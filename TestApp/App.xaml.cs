namespace TestApp
{
	public partial class App : Application
	{
		public App()
		{
			try
			{
				// Add global exception handlers
				AppDomain.CurrentDomain.UnhandledException += (s, e) =>
				{
					System.Diagnostics.Debugger.Break();
				};

				TaskScheduler.UnobservedTaskException += (s, e) =>
				{
					e.SetObserved();
					System.Diagnostics.Debugger.Break();
				};

				InitializeComponent();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debugger.Break();
				throw;
			}
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			try
			{
				var window = new Window(new MainPage());
				return window;
			}
			catch (Exception ex)
			{
				throw;
			}
		}
	}
}
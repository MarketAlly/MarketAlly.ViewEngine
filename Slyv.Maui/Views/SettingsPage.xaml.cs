using Slyv.Maui.ViewModels;

namespace Slyv.Maui.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsPage constructor error: {ex}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Reload settings when the page appears
        if (_viewModel != null)
        {
            try
            {
                await _viewModel.LoadSettingsCommand.ExecuteAsync(null);
            }
            catch { }
        }
    }
}
using DevExpress.Maui.CollectionView;
using Microsoft.Maui.Controls;
using UnifiedBrowser.Maui.Models;
using UnifiedBrowser.Maui.ViewModels;

namespace UnifiedBrowser.Maui.Views
{
    /// <summary>
    /// Tab switcher view for managing browser tabs
    /// </summary>
    public partial class TabSwitcherView : ContentView
    {
        private readonly BrowserViewModel _viewModel;

        public TabSwitcherView(BrowserViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Handle tab selection
            TabsCollectionView.SelectionChanged += OnTabSelectionChanged;
        }

        private void OnTabSelectionChanged(object? sender, CollectionViewSelectionChangedEventArgs e)
        {
            if (e.SelectedItems?.Count > 0 && e.SelectedItems[0] is BrowserTab selectedTab)
            {
                // Select the tab
                _viewModel.SelectTab(selectedTab);

                // Hide the tab switcher
                _viewModel.IsTabSwitcherVisible = false;

                // Clear selection to allow re-selecting the same tab
                TabsCollectionView.SelectedItem = null;
            }
        }
    }

    /// <summary>
    /// Converter that returns true if the value is null
    /// </summary>
    public class IsNullConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            bool isNull = value == null;
            bool invert = parameter?.ToString() == "True";
            return invert ? isNull : !isNull;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that returns true if the value is not null
    /// </summary>
    public class IsNotNullConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value != null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that inverts a boolean value
    /// </summary>
    public class InvertedBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}
using MarketAlly.Maui.ViewEngine;
using System.Collections.ObjectModel;

namespace TestApp
{
	public partial class MainPage : ContentPage
	{
		private bool _isSidebarVisible = false;
		private PageData _currentPageData;
		private bool _showingBodyLinksOnly = false;
		private DateTime _lastThumbnailCapture = DateTime.MinValue;

		public MainPage()
		{
			try
			{
				System.Diagnostics.Debug.WriteLine("MainPage: Constructor starting");
				InitializeComponent();
				System.Diagnostics.Debug.WriteLine("MainPage: InitializeComponent completed");
				UpdateSidebarVisibility();
				System.Diagnostics.Debug.WriteLine("MainPage: Constructor completed successfully");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"MainPage: Constructor FAILED - {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"MainPage: Stack trace - {ex.StackTrace}");
				throw;
			}
		}

		private async void urlEntry_Completed(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(urlEntry.Text))
			{
				// The WebView control will automatically normalize the URL
				webView.Source = urlEntry.Text;
			}
		}

		private void webView_PageDataChanged(object sender, PageData e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				_currentPageData = e;
				titleEntry.Text = e.Title;
				bodyEntry.Text = e.Body;
			});
		}

		private void ToggleSidebar_Clicked(object sender, EventArgs e)
		{
			_isSidebarVisible = !_isSidebarVisible;
			UpdateSidebarVisibility();
		}

		private void UpdateSidebarVisibility()
		{
			if (_isSidebarVisible)
			{
				sidePanel.IsVisible = true;
				sideColumn.Width = new GridLength(300);
			}
			else
			{
				sidePanel.IsVisible = false;
				sideColumn.Width = new GridLength(0);
			}
		}

		private async void RefreshData_Clicked(object sender, EventArgs e)
		{
			try
			{
				var pageData = await webView.GetPageDataAsync();
				if (pageData != null)
				{
					_currentPageData = pageData;
					titleEntry.Text = pageData.Title;
					bodyEntry.Text = pageData.Body;
				}
			}
			catch (Exception ex)
			{
				await DisplayAlert("Error", $"Failed to refresh data: {ex.Message}", "OK");
			}
		}

		private void ShowLinks_Clicked(object sender, EventArgs e)
		{
			_showingBodyLinksOnly = false;
			DisplayLinksCollection();
		}

		private void ShowAllLinks_Clicked(object sender, EventArgs e)
		{
			_showingBodyLinksOnly = false;
			UpdateLinkButtonStyles();
			DisplayLinksCollection();
		}

		private void ShowBodyLinks_Clicked(object sender, EventArgs e)
		{
			_showingBodyLinksOnly = true;
			UpdateLinkButtonStyles();
			DisplayLinksCollection();
		}

		private void UpdateLinkButtonStyles()
		{
			if (_showingBodyLinksOnly)
			{
				allLinksButton.BackgroundColor = Color.FromArgb("#E0E0E0");
				allLinksButton.TextColor = Colors.Black;
				bodyLinksButton.BackgroundColor = Color.FromArgb("#2196F3");
				bodyLinksButton.TextColor = Colors.White;
			}
			else
			{
				allLinksButton.BackgroundColor = Color.FromArgb("#2196F3");
				allLinksButton.TextColor = Colors.White;
				bodyLinksButton.BackgroundColor = Color.FromArgb("#E0E0E0");
				bodyLinksButton.TextColor = Colors.Black;
			}
		}

		private void DisplayLinksCollection()
		{
			var routesToDisplay = _showingBodyLinksOnly ? _currentPageData?.BodyRoutes : _currentPageData?.Routes;

			if (routesToDisplay != null && routesToDisplay.Count > 0)
			{
				// Create display items with additional properties for UI
				var linkItems = routesToDisplay.Select(r => new
				{
					r.Rank,
					r.Url,
					Text = string.IsNullOrWhiteSpace(r.Text) ? "[No Text]" : r.Text,
					r.Occurrences,
					DisplayText = r.Occurrences > 1
						? $"{(string.IsNullOrWhiteSpace(r.Text) ? "[No Text]" : r.Text)} ({r.Occurrences}x)"
						: (string.IsNullOrWhiteSpace(r.Text) ? "[No Text]" : r.Text),
					r.IsPotentialAd,
					AdLabel = r.IsPotentialAd ? $"⚠️ Potential Ad: {r.AdReason}" : "",
					BackgroundColor = r.IsPotentialAd ? Color.FromArgb("#FFF3E0") : Colors.White,
					RouteInfo = r // Keep original for navigation
				}).ToList();

				linksCollectionView.ItemsSource = linkItems;

				var totalLinks = routesToDisplay.Count;
				var adLinks = routesToDisplay.Count(r => r.IsPotentialAd);
				var regularLinks = totalLinks - adLinks;

				var linkType = _showingBodyLinksOnly ? "Body" : "All";
				linksStatsLabel.Text = $"{linkType} Links - Total: {totalLinks} | Regular: {regularLinks} | Ads: {adLinks}";

				UpdateLinkButtonStyles();
				linksOverlay.IsVisible = true;
			}
			else
			{
				var linkType = _showingBodyLinksOnly ? "body" : "";
				DisplayAlert("No Links", $"No {linkType} links found on the current page.", "OK");
			}
		}

		private void HideLinks_Clicked(object sender, EventArgs e)
		{
			linksOverlay.IsVisible = false;
		}

		private void LinkSelected(object sender, SelectionChangedEventArgs e)
		{
			if (e.CurrentSelection.FirstOrDefault() is { } selectedItem)
			{
				// Get the URL from the selected item
				var urlProperty = selectedItem.GetType().GetProperty("Url");
				var url = urlProperty?.GetValue(selectedItem) as string;

				if (!string.IsNullOrEmpty(url))
				{
					// Navigate to the selected link
					webView.Source = url;
					linksOverlay.IsVisible = false;
				}

				// Clear selection
				linksCollectionView.SelectedItem = null;
			}
		}

		private async void CaptureThumbnail_Clicked(object sender, EventArgs e)
		{
			try
			{
				// Show loading state
				thumbnailStatusLabel.Text = "Capturing thumbnail...";
				thumbnailImage.Source = null;
				thumbnailOverlay.IsVisible = true;

				// Capture the thumbnail
				var thumbnail = await webView.CaptureThumbnailAsync(640, 360);

				if (thumbnail != null)
				{
					_lastThumbnailCapture = DateTime.Now;
					thumbnailImage.Source = thumbnail;
					thumbnailStatusLabel.Text = $"Captured at {_lastThumbnailCapture:HH:mm:ss} | Size: 640x360";
				}
				else
				{
					thumbnailStatusLabel.Text = "Failed to capture thumbnail";
					await DisplayAlert("Error", "Failed to capture thumbnail. WebView may not be ready.", "OK");
				}
			}
			catch (Exception ex)
			{
				thumbnailStatusLabel.Text = $"Error: {ex.Message}";
				await DisplayAlert("Error", $"Failed to capture thumbnail: {ex.Message}", "OK");
			}
		}

		private void HideThumbnail_Clicked(object sender, EventArgs e)
		{
			thumbnailOverlay.IsVisible = false;
		}
	}

}

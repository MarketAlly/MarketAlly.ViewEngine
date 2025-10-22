using MarketAlly.Maui.ViewEngine;
using System.Collections.ObjectModel;

namespace TestApp
{
	public partial class MainPage : ContentPage
	{
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

				// Bind history to the CollectionView
				historyCollectionView.ItemsSource = browserView.NavigationHistory;

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
				// The BrowserView control will automatically normalize the URL and handle PDFs
				browserView.Source = new UrlWebViewSource { Url = urlEntry.Text };
			}
		}

		private void BrowserView_PageDataChanged(object sender, PageData e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				_currentPageData = e;
				pageTitle.Text = e.Title ?? "Untitled";
				urlEntry.Text = e.Url;

				// Update history stats
				historyStatsLabel.Text = $"Total: {browserView.NavigationHistory.Count} pages | Current: #{browserView.CurrentHistoryIndex + 1}";

				System.Diagnostics.Debug.WriteLine($"Page loaded: {e.Title} | URL: {e.Url}");
				if (!string.IsNullOrEmpty(e.FaviconUrl))
				{
					System.Diagnostics.Debug.WriteLine($"Favicon: {e.FaviconUrl}");
				}
			});
		}

		#region Navigation History

		private void ShowHistory_Clicked(object sender, EventArgs e)
		{
			historyStatsLabel.Text = $"Total: {browserView.NavigationHistory.Count} pages | Current: #{browserView.CurrentHistoryIndex + 1}";
			historyOverlay.IsVisible = true;
		}

		private void HideHistory_Clicked(object sender, EventArgs e)
		{
			historyOverlay.IsVisible = false;
		}

		private async void HistoryItemSelected(object sender, SelectionChangedEventArgs e)
		{
			if (e.CurrentSelection.FirstOrDefault() is NavigationHistoryItem historyItem)
			{
				var index = browserView.NavigationHistory.IndexOf(historyItem);
				await browserView.NavigateToHistoryItemAsync(index);
				historyOverlay.IsVisible = false;

				// Clear selection
				historyCollectionView.SelectedItem = null;
			}
		}

		private async void GoBack_Clicked(object sender, EventArgs e)
		{
			await browserView.GoBackInHistoryAsync();
		}

		private async void GoForward_Clicked(object sender, EventArgs e)
		{
			await browserView.GoForwardInHistoryAsync();
		}

		#endregion

		#region Links Display

		private async void RefreshData_Clicked(object sender, EventArgs e)
		{
			try
			{
				var pageData = await browserView.GetPageDataAsync();
				if (pageData != null)
				{
					_currentPageData = pageData;
					pageTitle.Text = pageData.Title;
				}
			}
			catch (Exception ex)
			{
				await DisplayAlert("Error", $"Failed to refresh data: {ex.Message}", "OK");
			}
		}

		private async void ShowLinks_Clicked(object sender, EventArgs e)
		{
			// Extract routes on-demand
			try
			{
				var pageData = await browserView.ExtractRoutesAsync();
				_currentPageData = pageData;
				_showingBodyLinksOnly = false;
				DisplayLinksCollection();
			}
			catch (Exception ex)
			{
				await DisplayAlert("Error", $"Failed to extract links: {ex.Message}", "OK");
			}
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
					browserView.Source = new UrlWebViewSource { Url = url };
					linksOverlay.IsVisible = false;
				}

				// Clear selection
				linksCollectionView.SelectedItem = null;
			}
		}

		#endregion

		#region Thumbnail

		private async void CaptureThumbnail_Clicked(object sender, EventArgs e)
		{
			try
			{
				// Show loading state
				thumbnailStatusLabel.Text = "Capturing thumbnail...";
				thumbnailImage.Source = null;
				thumbnailOverlay.IsVisible = true;

				// Capture the thumbnail
				var thumbnail = await browserView.CaptureThumbnailAsync(640, 360);

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

		#endregion
	}
}

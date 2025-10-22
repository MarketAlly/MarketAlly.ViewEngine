using Microsoft.Extensions.Logging;
using UnifiedChat.Maui.Controls;
using UnifiedChat.Maui.ViewModels;

namespace Slyv.Maui.Views
{
    /// <summary>
    /// Main chat page showcasing the UnifiedChat control
    /// </summary>
    public partial class ChatPage : ContentPage
    {
        private readonly ILogger<ChatPage>? _logger;
        private readonly UnifiedChatControl _chatControl;

        /// <summary>
        /// Constructor with dependency injection support
        /// </summary>
        public ChatPage(UnifiedChatControl chatControl, ILogger<ChatPage>? logger = null)
        {
            InitializeComponent();
            _logger = logger;
            _chatControl = chatControl;

            // Add the control to the host
            ChatHost.Content = _chatControl;

            // Subscribe to link clicks
            _chatControl.LinkClicked += OnLinkClicked;

            _logger?.LogInformation("ChatPage initialized with UnifiedChatControl");
        }

        private UnifiedChatControl ChatControl => _chatControl;

        private void OnToggleMemoryClicked(object sender, EventArgs e)
        {
            _logger?.LogInformation("Toggle memory panel clicked");
            _chatControl.ToggleMemoryPanel();
        }

        private async void OnLinkClicked(object? sender, string url)
        {
            try
            {
                _logger?.LogInformation($"Link clicked in chat: {url}");

                // Navigate to BrowserPage
                await Shell.Current.GoToAsync("//BrowserPage");
                await Task.Yield();
                await Task.Delay(1000);

                // Get the BrowserPage from the shell and tell it to navigate
                var browserPage = Shell.Current.CurrentPage as BrowserPage;
                if (browserPage != null)
                {
                    browserPage.NavigateToUrl(url);
                }
                else
                {
                    _logger?.LogWarning("Could not get BrowserPage reference to navigate to URL");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error navigating to URL: {url}");
            }
        }

        private void OnClearClicked(object sender, EventArgs e)
        {
            _logger?.LogInformation("Clear chat clicked");

            // Execute the clear command from the ViewModel
            if (_chatControl.BindingContext is ChatViewModel viewModel)
            {
                if (viewModel.ClearChatCommand?.CanExecute(null) == true)
                {
                    viewModel.ClearChatCommand.Execute(null);
                }
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            _logger?.LogInformation("ChatPage appearing");

            // Refresh status when page appears
            try
            {
                await _chatControl.RefreshStatusAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing chat status");
            }

            // Force refresh the control to ensure it's visible
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(10), () =>
            {
                if (ChatControl != null)
                {
                    try
                    {
                        ChatControl.InvalidateMeasure();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error invalidating chat control measure");
                    }
                }
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            _logger?.LogInformation("ChatPage disappearing");

            // Cleanup subscriptions
            try
            {
                _chatControl.Cleanup();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error cleaning up chat control");
            }
        }

        /// <summary>
        /// Handle hardware back button on Android
        /// </summary>
        protected override bool OnBackButtonPressed()
        {
            // Allow default back navigation behavior for chat
            return base.OnBackButtonPressed();
        }
    }
}

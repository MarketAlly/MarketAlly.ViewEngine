using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Maui.TouchEffect.Hosting;
using UnifiedRag.Maui;

namespace UnifiedBrowser.Maui.Extensions
{
    /// <summary>
    /// Extension to safely register TouchEffect only once across all control libraries
    /// </summary>
    internal static class SharedRegistrationTracker
    {
        /// <summary>
        /// Registers TouchEffect only if not already registered. Uses builder's service collection as shared state.
        /// The marker type is defined in UnifiedRag.Maui (shared assembly) so all libraries see the same type.
        /// </summary>
        public static void RegisterTouchEffectOnce(this MauiAppBuilder builder)
        {
            // Check if marker service exists - this is shared across all assemblies using the same builder
            var markerExists = builder.Services.Any(d => d.ServiceType == typeof(TouchEffectRegistrationMarker));
            System.Diagnostics.Debug.WriteLine($"[UnifiedBrowser] TouchEffect marker exists: {markerExists}, Type: {typeof(TouchEffectRegistrationMarker).FullName}");

            if (markerExists)
            {
                System.Diagnostics.Debug.WriteLine("[UnifiedBrowser] Skipping TouchEffect registration - already registered");
                return; // Already registered
            }

            // Add marker service to indicate TouchEffect is being registered
            builder.Services.AddSingleton<TouchEffectRegistrationMarker>();
            System.Diagnostics.Debug.WriteLine("[UnifiedBrowser] Registering TouchEffect...");

            // Register TouchEffect
            builder.UseMauiTouchEffect();
            System.Diagnostics.Debug.WriteLine("[UnifiedBrowser] TouchEffect registered successfully");
        }
    }
}

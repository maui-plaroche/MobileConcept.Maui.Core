using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MobileConcept.Core.GameEngine.Maui;

public static class AppBuilderExtensions
{
    /// <summary>À appeler après UseMobileConcept(). Les effets sont créés à la demande par fenêtre.</summary>
    public static MauiAppBuilder UseMobileConceptGameEngine(this MauiAppBuilder builder)
    {
        builder.UseSkiaSharp();
#if IOS
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<MetalSurface, MetalSurfaceHandler>());
#elif ANDROID
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<VulkanSurface, VulkanSurfaceHandler>());
#endif
        builder.Services.TryAddSingleton<IWindowEffects, NativeWindowEffects>();
        return builder;
    }
}

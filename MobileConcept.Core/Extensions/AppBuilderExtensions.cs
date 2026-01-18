using Microsoft.Maui.Hosting;
using MobileConcept.Core.Bootstrap;

namespace MobileConcept.Core.Extensions;

/// <summary>
/// 
/// </summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static MauiAppBuilder UseMobileConcept(this MauiAppBuilder builder)
    {
        var bootstrapper = new Bootstrapper(builder.Services);
        bootstrapper.Initialize();
        return builder;
    }
}
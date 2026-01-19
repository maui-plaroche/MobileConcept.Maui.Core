using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using MobileConcept.Core.Bootstrap;
using MobileConcept.Core.Services;
using MobileConcept.Core.Views;

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
        // -- DI
        var bootstrapper = new Bootstrapper(builder.Services);
        bootstrapper.Initialize();

        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android =>
            {
                android.OnCreate((activity, bundle) =>
                {
                });
                
                android.OnStart(activity =>
                {
                });

                android.OnResume(activity =>
                {
                    // -- Get Visible Page
                    var services = IPlatformApplication.Current?.Services;
                    var navigationService = services?.GetRequiredService<INavigationService>();
                    var currentPage = navigationService?.GetCurrentPage();
                    if (currentPage is IAppLifeCycleContentPage lifeCyclePage)
                    {
                        currentPage.Dispatcher?.DispatchAsync(lifeCyclePage.OnAppEnterForegroundAsync);
                    }
                });

                android.OnPause(activity =>
                {
                    // -- Get Visible Page
                    var services = IPlatformApplication.Current?.Services;
                    var navigationService = services?.GetRequiredService<INavigationService>();
                    var currentPage = navigationService?.GetCurrentPage();                   
                    if (currentPage is IAppLifeCycleContentPage lifeCyclePage)
                    {
                        currentPage.Dispatcher?.DispatchAsync(lifeCyclePage.OnAppEnterBackgroundAsync);
                    }
                });
            });
#endif
#if IOS
            events.AddiOS(ios =>
            {
                ios.OnActivated(app =>
                {
                    // -- Get Visible Page
                    var services = IPlatformApplication.Current?.Services;
                    var navigationService = services?.GetRequiredService<INavigationService>();
                    var currentPage = navigationService?.GetCurrentPage();
                    if (currentPage is IAppLifeCycleContentPage lifeCyclePage)
                    {
                        currentPage.Dispatcher?.DispatchAsync(lifeCyclePage.OnAppEnterForegroundAsync);
                    }
                });
                ios.OnResignActivation(app =>
                {
                    // -- Get Visible Page
                    var services = IPlatformApplication.Current?.Services;
                    var navigationService = services?.GetRequiredService<INavigationService>();
                    var currentPage = navigationService?.GetCurrentPage();                    if (currentPage is IAppLifeCycleContentPage lifeCyclePage)
                    {
                        currentPage.Dispatcher?.DispatchAsync(lifeCyclePage.OnAppEnterBackgroundAsync);
                    }
                });
            });
#endif
        });
        
       

        return builder;
    }
}
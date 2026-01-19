using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Microsoft.Maui.Controls;
using MobileConcept.Core.Helpers;
using MobileConcept.Core.ViewModels;
using Application = Microsoft.Maui.Controls.Application;

namespace MobileConcept.Core.Services;

public class NavigationService : INavigationService
{
    private static readonly ConcurrentDictionary<string, byte> RegisteredRoutes = new();

    private Page? MainPage => PlatformHelper.GetMainPage();

    public bool IsShellNavigation => MainPage is Shell;

    public bool IsStackNavigation => MainPage is NavigationPage || 
                                     (MainPage is not Shell && MainPage is ContentPage);

    public async Task NavigateToAsync<TPage>() where TPage : Page
    {
        Debug.WriteLine("=== NavigateToAsync WITHOUT params ===");

        var mainPage = PlatformHelper.GetMainPage();
        if (mainPage is NavigationPage navPage)
        {
            // NavigationPage : on doit créer la page manuellement
            var services = PlatformHelper.GetServices();
            var page = services?.GetRequiredService<TPage>();
            if (page != null)
            {
                await navPage.Navigation.PushAsync(page);
            }
        }
        else if (mainPage is Shell shell)
        {
            // -- Register routes for deep links / future navigation
            var route = typeof(TPage).Name;
            if (RegisteredRoutes.TryAdd(route, 1))
            {
                try
                {
                    Routing.RegisterRoute(route, typeof(TPage));
                }
                catch (Exception ex)
                {
                    RegisteredRoutes.TryRemove(route, out _);
                    Console.WriteLine($"Route registration error: {ex.Message}");
                }
            }

            // -- Create Page/ ViewModel via DI
            var services = PlatformHelper.GetServices();
            var page = services?.GetRequiredService<TPage>();
            if (page != null)
            {
                await shell.Navigation.PushAsync(page, true);
            }
        }
        else
        {
            throw new InvalidOperationException("Unsupported navigation container");
        }
    }

    public async Task NavigateToAsync<TPage>(params object[] args) where TPage : Page
    {
        Debug.WriteLine($"=== NavigateToAsync WITH params: {args.Length} ===");
        
        var mainPage = PlatformHelper.GetMainPage();
        if (mainPage is NavigationPage navPage)
        {
            // -- NavigationPage : Create Page/ ViewModel via DI
            var services = PlatformHelper.GetServices();
            var page = services?.GetRequiredService<TPage>();
            if (page != null)
            {
                if (args.Length > 0 && page.BindingContext is IViewModelBase vm)
                {
                    await vm.InitializeAsync(args);
                }
                await navPage.Navigation.PushAsync(page);
            }
        }
        else if (mainPage is Shell shell)
        {
            // -- Register routes for deep links / future navigation
            var route = typeof(TPage).Name;
            if (RegisteredRoutes.TryAdd(route, 1))
            {
                try
                {
                    Routing.RegisterRoute(route, typeof(TPage));
                }
                catch (Exception ex)
                {
                    RegisteredRoutes.TryRemove(route, out _);
                    Console.WriteLine($"Route registration error: {ex.Message}");
                    return;
                }
            }
         
            // -- Create Page/ ViewModel via DI
            var services = PlatformHelper.GetServices();
            var page = services?.GetRequiredService<TPage>();
            if (page != null)
            {
                if (args.Length > 0 && page.BindingContext is IViewModelBase vm)
                {
                    await vm.InitializeAsync(args);
                }
        
                await shell.Navigation.PushAsync(page, true);
            }
        }
        else
        {
            throw new InvalidOperationException("Unsupported navigation container");
        }
    }

    public ContentPage? GetCurrentPage()
    {
        return MainPage switch
        {
            // Shell navigation
            Shell shell => GetCurrentPageFromShell(shell),
            
            // NavigationPage stack
            NavigationPage navigationPage => GetCurrentPageFromNavigationPage(navigationPage),
            
            // FlyoutPage / MasterDetailPage
            FlyoutPage flyoutPage => GetCurrentPageFromFlyout(flyoutPage),
            
            // TabbedPage
            TabbedPage tabbedPage => GetCurrentPageFromTabbedPage(tabbedPage),
            
            // Direct ContentPage as MainPage
            ContentPage contentPage => contentPage,
            
            _ => null
        };
    }

    public T? GetCurrentPage<T>() where T : ContentPage
    {
        return GetCurrentPage() as T;
    }

    private static ContentPage? GetCurrentPageFromShell(Shell shell)
    {
        // Shell.CurrentPage is the most reliable way
        if (shell.CurrentPage is ContentPage shellCurrentPage)
            return shellCurrentPage;

        // Fallback: check navigation stack for modals or pushed pages
        var navigationStack = shell.Navigation?.NavigationStack;
        if (navigationStack?.Count > 0)
        {
            var lastPage = navigationStack.LastOrDefault();
            if (lastPage is ContentPage stackPage)
                return stackPage;
        }

        // Check modal stack
        var modalStack = shell.Navigation?.ModalStack;
        if (modalStack?.Count > 0)
        {
            var modalPage = modalStack.LastOrDefault();
            return GetContentPageFromPage(modalPage);
        }

        return null;
    }

    private static ContentPage? GetCurrentPageFromNavigationPage(NavigationPage navigationPage)
    {
        // Check modal stack first (modals are on top)
        var modalStack = navigationPage.Navigation?.ModalStack;
        if (modalStack?.Count > 0)
        {
            var modalPage = modalStack.LastOrDefault();
            return GetContentPageFromPage(modalPage);
        }

        // Then check navigation stack
        return navigationPage.CurrentPage as ContentPage;
    }

    private static ContentPage? GetCurrentPageFromFlyout(FlyoutPage flyoutPage)
    {
        var detail = flyoutPage.Detail;
        return GetContentPageFromPage(detail);
    }

    private static ContentPage? GetCurrentPageFromTabbedPage(TabbedPage tabbedPage)
    {
        var currentTab = tabbedPage.CurrentPage;
        return GetContentPageFromPage(currentTab);
    }

    private static ContentPage? GetContentPageFromPage(Page? page)
    {
        return page switch
        {
            ContentPage contentPage => contentPage,
            NavigationPage navPage => navPage.CurrentPage as ContentPage,
            TabbedPage tabbedPage => GetCurrentPageFromTabbedPage(tabbedPage),
            FlyoutPage flyoutPage => GetCurrentPageFromFlyout(flyoutPage),
            _ => null
        };
    }
}
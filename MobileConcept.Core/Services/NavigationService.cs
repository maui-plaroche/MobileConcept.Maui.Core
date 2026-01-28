
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Maui.Controls;
using MobileConcept.Core.Helpers;
using MobileConcept.Core.ViewModels;

namespace MobileConcept.Core.Services;

/// <summary>
/// Provides navigation services for MAUI applications supporting both Shell and NavigationPage patterns.
/// </summary>
public class NavigationService : INavigationService
{
    private static readonly ConcurrentDictionary<string, byte> RegisteredRoutes = new();

    private Page? MainPage => PlatformHelper.GetMainPage();

    /// <inheritdoc/>
    public bool IsShellNavigation => MainPage is Shell;

    /// <inheritdoc/>
    public bool IsStackNavigation => MainPage is NavigationPage || 
                                     (MainPage is not Shell && MainPage is ContentPage);

    /// <inheritdoc/>
    public async Task NavigateToAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(bool animated = true) where TPage : Page
    {
        Debug.WriteLine("=== NavigateToAsync WITHOUT params ===");

        var mainPage = PlatformHelper.GetMainPage();
        if (mainPage is NavigationPage navPage)
        {
            var services = PlatformHelper.GetServices();
            var page = services?.GetRequiredService<TPage>();
            if (page != null)
            {
                await navPage.Navigation.PushAsync(page, animated: animated);
            }
        }
        else if (mainPage is Shell shell)
        {
            // Register routes for deep links / future navigation
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

            var services = PlatformHelper.GetServices();
            var page = services?.GetRequiredService<TPage>();
            if (page != null)
            {
                await shell.Navigation.PushAsync(page, animated: animated);
            }
        }
        else
        {
            throw new InvalidOperationException("Unsupported navigation container");
        }
    }

    /// <inheritdoc/>
    public Task NavigateToAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>() where TPage : Page
    {
        return NavigateToAsync<TPage>(true, Array.Empty<object>());
    }

    /// <inheritdoc/>
    public Task NavigateToAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(params object[] args) where TPage : Page
    {
        return NavigateToAsync<TPage>(true, args);
    }

    /// <inheritdoc/>
    public async Task NavigateToAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(bool animated = true, params object[] args) where TPage : Page
    {
        Debug.WriteLine($"=== NavigateToAsync WITH params: {args.Length} ===");
        
        var mainPage = PlatformHelper.GetMainPage();
        if (mainPage is NavigationPage navPage)
        {
            var services = PlatformHelper.GetServices();
            var page = services?.GetRequiredService<TPage>();
            if (page != null)
            {
                if (args.Length > 0 && page.BindingContext is IViewModelBase vm)
                {
                    await vm.InitializeAsync(args);
                }
                await navPage.Navigation.PushAsync(page, animated: animated);
            }
        }
        else if (mainPage is Shell shell)
        {
            // Register routes for deep links / future navigation
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
         
            var services = PlatformHelper.GetServices();
            var page = services?.GetRequiredService<TPage>();
            if (page != null)
            {
                if (args.Length > 0 && page.BindingContext is IViewModelBase vm)
                {
                    await vm.InitializeAsync(args);
                }
        
                await shell.Navigation.PushAsync(page, animated: animated);
            }
        }
        else
        {
            throw new InvalidOperationException("Unsupported navigation container");
        }
    }

    /// <inheritdoc/>
    public ContentPage? GetCurrentPage()
    {
        return MainPage switch
        {
            Shell shell => GetCurrentPageFromShell(shell),
            NavigationPage navigationPage => GetCurrentPageFromNavigationPage(navigationPage),
            FlyoutPage flyoutPage => GetCurrentPageFromFlyout(flyoutPage),
            TabbedPage tabbedPage => GetCurrentPageFromTabbedPage(tabbedPage),
            ContentPage contentPage => contentPage,
            _ => null
        };
    }

    /// <inheritdoc/>
    public T? GetCurrentPage<T>() where T : ContentPage
    {
        return GetCurrentPage() as T;
    }

    /// <summary>
    /// Retrieves the current content page from a Shell navigation container.
    /// </summary>
    /// <param name="shell">The Shell instance to examine.</param>
    /// <returns>The current <see cref="ContentPage"/>, or null if not found.</returns>
    private static ContentPage? GetCurrentPageFromShell(Shell shell)
    {
        if (shell.CurrentPage is ContentPage shellCurrentPage)
            return shellCurrentPage;

        var navigationStack = shell.Navigation?.NavigationStack;
        if (navigationStack?.Count > 0)
        {
            var lastPage = navigationStack.LastOrDefault();
            if (lastPage is ContentPage stackPage)
                return stackPage;
        }

        var modalStack = shell.Navigation?.ModalStack;
        if (modalStack?.Count > 0)
        {
            var modalPage = modalStack.LastOrDefault();
            return GetContentPageFromPage(modalPage);
        }

        return null;
    }

    /// <summary>
    /// Retrieves the current content page from a NavigationPage container.
    /// </summary>
    /// <param name="navigationPage">The NavigationPage instance to examine.</param>
    /// <returns>The current <see cref="ContentPage"/>, or null if not found.</returns>
    private static ContentPage? GetCurrentPageFromNavigationPage(NavigationPage navigationPage)
    {
        var modalStack = navigationPage.Navigation?.ModalStack;
        if (modalStack?.Count > 0)
        {
            var modalPage = modalStack.LastOrDefault();
            return GetContentPageFromPage(modalPage);
        }

        return navigationPage.CurrentPage as ContentPage;
    }

    /// <summary>
    /// Retrieves the current content page from a FlyoutPage container.
    /// </summary>
    /// <param name="flyoutPage">The FlyoutPage instance to examine.</param>
    /// <returns>The current <see cref="ContentPage"/>, or null if not found.</returns>
    private static ContentPage? GetCurrentPageFromFlyout(FlyoutPage flyoutPage)
    {
        var detail = flyoutPage.Detail;
        return GetContentPageFromPage(detail);
    }

    /// <summary>
    /// Retrieves the current content page from a TabbedPage container.
    /// </summary>
    /// <param name="tabbedPage">The TabbedPage instance to examine.</param>
    /// <returns>The current <see cref="ContentPage"/>, or null if not found.</returns>
    private static ContentPage? GetCurrentPageFromTabbedPage(TabbedPage tabbedPage)
    {
        var currentTab = tabbedPage.CurrentPage;
        return GetContentPageFromPage(currentTab);
    }

    /// <summary>
    /// Extracts a ContentPage from various page types by recursively checking the page hierarchy.
    /// </summary>
    /// <param name="page">The page to examine.</param>
    /// <returns>The <see cref="ContentPage"/> found in the hierarchy, or null if not found.</returns>
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

    /// <inheritdoc/>
    public async Task GoBackAsync(bool animated = true)
    {
        var mainPage = PlatformHelper.GetMainPage();
    
        if (mainPage is NavigationPage navPage)
        {
            if (navPage.Navigation.NavigationStack.Count > 1)
            {
                await navPage.Navigation.PopAsync(animated);
            }
        }
        else if (mainPage is Shell shell)
        {
            if (shell.Navigation.NavigationStack.Count > 1)
            {
                await shell.GoToAsync("..", animated);
            }
        }
    }
}
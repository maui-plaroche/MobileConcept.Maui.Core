using Microsoft.Maui.Controls;

namespace MobileConcept.Core.Services;

/// <summary>
/// Provides an abstraction for navigation-related functionality within the application.
/// This interface defines methods and properties to manage the navigation stack and retrieve current pages.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the currently visible ContentPage.
    /// </summary>
    ContentPage? GetCurrentPage();

    /// <summary>
    /// Gets the currently visible page as a specific type.
    /// </summary>
    T? GetCurrentPage<T>() where T : ContentPage;

    /// <summary>
    /// Returns true if the app uses Shell navigation.
    /// </summary>
    bool IsShellNavigation { get; }

    /// <summary>
    /// Returns true if the app uses NavigationPage stack navigation.
    /// </summary>
    bool IsStackNavigation { get; }
    
    Task NavigateToAsync<TPage>() where TPage : Page;
    
    Task NavigateToAsync<TPage>(params object[] args) where TPage : Page;

}
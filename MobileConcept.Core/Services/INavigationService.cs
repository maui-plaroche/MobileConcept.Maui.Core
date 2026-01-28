
using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Controls;

namespace MobileConcept.Core.Services;

/// <summary>
/// Provides an abstraction for navigation-related functionality within the application.
/// Supports both Shell and NavigationPage navigation patterns with type-safe page navigation.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the currently visible <see cref="ContentPage"/> in the application.
    /// </summary>
    /// <returns>The current <see cref="ContentPage"/>, or null if no page is currently visible.</returns>
    ContentPage? GetCurrentPage();

    /// <summary>
    /// Gets the currently visible page as a specific type.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="ContentPage"/> to retrieve.</typeparam>
    /// <returns>The current page cast to type <typeparamref name="T"/>, or null if the cast fails or no page is visible.</returns>
    T? GetCurrentPage<T>() where T : ContentPage;

    /// <summary>
    /// Gets a value indicating whether the application uses Shell-based navigation.
    /// </summary>
    bool IsShellNavigation { get; }

    /// <summary>
    /// Gets a value indicating whether the application uses NavigationPage stack-based navigation.
    /// </summary>
    bool IsStackNavigation { get; }

    /// <summary>
    /// Navigates to the specified page type without parameters.
    /// </summary>
    /// <typeparam name="TPage">The type of page to navigate to. Must have a public constructor.</typeparam>
    /// <param name="animated">Whether to animate the navigation transition. Default is true.</param>
    /// <returns>A task representing the asynchronous navigation operation.</returns>
    Task NavigateToAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(bool animated = true) where TPage : Page;

    /// <summary>
    /// Navigates to the specified page type with parameters that will be passed to the page's ViewModel.
    /// </summary>
    /// <typeparam name="TPage">The type of page to navigate to. Must have a public constructor.</typeparam>
    /// <param name="args">Parameters to pass to the destination ViewModel's <see cref="ViewModels.IViewModelBase.InitializeAsync"/> method.</param>
    /// <returns>A task representing the asynchronous navigation operation.</returns>
    Task NavigateToAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(params object[] args) where TPage : Page;

    /// <summary>
    /// Navigates to the specified page type with animation control and parameters.
    /// </summary>
    /// <typeparam name="TPage">The type of page to navigate to. Must have a public constructor.</typeparam>
    /// <param name="animated">Whether to animate the navigation transition.</param>
    /// <param name="args">Parameters to pass to the destination ViewModel's <see cref="ViewModels.IViewModelBase.InitializeAsync"/> method.</param>
    /// <returns>A task representing the asynchronous navigation operation.</returns>
    Task NavigateToAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(bool animated, params object[] args) where TPage : Page;

    /// <summary>
    /// Navigates back to the previous page in the navigation stack.
    /// </summary>
    /// <param name="animated">Whether to animate the navigation transition. Default is true.</param>
    /// <returns>A task representing the asynchronous navigation operation.</returns>
    Task GoBackAsync(bool animated = true);
}
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using MobileConcept.Core.ViewModels;

namespace MobileConcept.Core.Views;

/// <summary>
/// Represents a base class for content pages that provides lifecycle hooks
/// and supports XAML design compatibility.
/// </summary>
public class BaseContentPage<T> : ContentPage, IBaseContentPage, IQueryAttributable
    where T : class, IViewModelBase
{
    /// <summary>
    /// Gets the strongly-typed ViewModel.
    /// </summary>
    private T ViewModel => (T)BindingContext;

    /// <summary>
    /// Initializes the page with the injected ViewModel.
    /// </summary>
    protected BaseContentPage(T viewModel)
    {
        BindingContext = viewModel;
    }

    /// <summary>
    /// Called when the application starts. Override to add custom logic.
    /// </summary>
    public virtual async Task OnAppStartAsync()
    {
        await ViewModel.OnAppStartAsync();
    }

    /// <summary>
    /// Called when the application resumes. Override to add custom logic.
    /// </summary>
    public virtual async Task OnAppResumeAsync()
    {
        await ViewModel.OnAppResumeAsync();
    }

    /// <summary>
    /// Called when the application goes to sleep. Override to add custom logic.
    /// </summary>
    public virtual async Task OnAppSleepAsync()
    {
        await ViewModel.OnAppSleepAsync();
    }

    /// <summary>
    /// Applies navigation query attributes. Override to handle parameters.
    /// </summary>
    public virtual void ApplyQueryAttributes(IDictionary<string, object> query) { }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.OnAppearingAsync();
    }

    /// <inheritdoc />
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await ViewModel.OnDisappearingAsync();
    }
}


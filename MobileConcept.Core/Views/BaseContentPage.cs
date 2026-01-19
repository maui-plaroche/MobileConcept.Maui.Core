using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
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
    /// Gets or sets a value indicating whether lifecycle events are enabled for the page.
    /// This property controls whether the lifecycle methods such as <c>OnAppStartAsync</c>,
    /// <c>OnAppResumeAsync</c>, and <c>OnAppSleepAsync</c> are invoked during the respective
    /// application lifecycle transitions.
    /// </summary>
    public bool EnableLifecycleEvents { get; set; } = true;
    
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

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Task OnAppStartAsync()
    {
        if (EnableLifecycleEvents)
        {
            return ViewModel.OnAppStartAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Task OnAppEnterForegroundAsync()
    {
        if (EnableLifecycleEvents)
        {
            return ViewModel.OnAppEnterForegroundAsync();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Task OnAppEnterBackgroundAsync()
    {
        if (EnableLifecycleEvents)
        {
            return ViewModel.OnAppEnterBackgroundAsync();
        }
        return Task.CompletedTask;
    }
}


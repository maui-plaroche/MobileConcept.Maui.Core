
# MobileConcept.Maui.Core

Essential core library for .NET MAUI applications. Provides utilities, base classes, and extension methods for cross-platform mobile development on Android and iOS.

## Installation
```
bash
dotnet add package MobileConcept.Maui.Core
```
Or via NuGet Package Manager:
```

Install-Package MobileConcept.Maui.Core
```
## Features

- **ViewModel Lifecycle Management** - Automatic handling of page appearing/disappearing events
- **Application Lifecycle Events** - React to app foreground/background transitions
- **Type-Safe Navigation** - Navigate between pages with strongly-typed parameters
- **Parameter Passing** - Pass multiple parameters during navigation
- **Bootstrapper Pattern** - Structured dependency injection with `BootstrapperBase`
- **Image Helpers** - Resize and rotate images with EXIF orientation handling
- **Base Classes** - MVVM foundation classes
- **Extension Methods** - Common utilities for MAUI development
- **Platform Helpers** - Android and iOS specific utilities

## Requirements

- .NET 10.0 or later
- .NET MAUI 10.0 or later

---

## Installation & Setup

### MauiProgram.cs Configuration

Add `.UseMobileConcept()` to your MAUI app builder and use a bootstrapper for DI:
```
csharp
using MobileConcept.Core.Extensions;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
        .UseMauiApp<App>()
        .UseMobileConcept()  // 👈 Required: Enable MobileConcept features
        .ConfigureFonts(fonts =>
        {
        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // -- DI: Use your custom bootstrapper
        var bootstrapper = new SampleDemoBootstrapper(builder.Services);
        bootstrapper.Initialize();
        
        return builder.Build();
    }
}
```
The `.UseMobileConcept()` extension:
- Registers core MobileConcept services (like `INavigationService`)
- Configures platform-specific lifecycle events (Android/iOS) for foreground/background detection
- Automatically forwards app lifecycle events to your ViewModels

---

## Bootstrapper Pattern

### BootstrapperBase

Use `BootstrapperBase` to organize your dependency injection registrations in a clean, structured way:
```
csharp
using Microsoft.Extensions.DependencyInjection;
using MobileConcept.Core.Bootstrap;

namespace MobileConceptSample.Bootstrap;

public class SampleDemoBootstrapper : BootstrapperBase
{
public SampleDemoBootstrapper(IServiceCollection services) : base(services)
{
}

    protected override void RegisterServices()
    {
        // Register your application services
        Services.AddSingleton<IMyService, MyService>();
        Services.AddTransient<IDataService, DataService>();
    }

    protected override void RegisterViewModels()
    {
        // Register all ViewModels
        Services.AddTransient<MainPageViewModel>();
        Services.AddTransient<SecondPageViewModel>();
    }

    protected override void RegisterViews()
    {
        // Register all Pages/Views
        Services.AddTransient<MainPage>();
        Services.AddTransient<SecondPage>();
    }
}
```
### BootstrapperBase Methods

| Method | Purpose |
|--------|---------|
| `RegisterServices()` | Register application services, repositories, API clients |
| `RegisterViewModels()` | Register all ViewModel classes |
| `RegisterViews()` | Register all Page/View classes |
| `Initialize()` | Calls all registration methods (called from MauiProgram.cs) |

---

## ViewModel Lifecycle

### ViewModelBase

All ViewModels should inherit from `ViewModelBase` to gain access to lifecycle methods:
```
csharp
using MobileConcept.Core.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
    public override Task OnAppearingAsync()
    {
        // Called when the page appears - ideal for loading/refreshing data
        return Task.CompletedTask;
    }

    public override Task OnDisappearingAsync()
    {
        // Called when the page disappears - cleanup resources here
        return Task.CompletedTask;
    }

    public override Task InitializeAsync(params object[] args)
    {
        // Called after navigation with passed parameters
        return Task.CompletedTask;
    }

    public override Task OnAppStartAsync()
    {
        // Called when the application starts
        return Task.CompletedTask;
    }

    public override Task OnAppEnterForegroundAsync()
    {
        // Called when app returns from background
        Debug.WriteLine("App entered foreground");
        return Task.CompletedTask;
    }

    public override Task OnAppEnterBackgroundAsync()
    {
        // Called when app goes to background
        Debug.WriteLine("App entered background");
        return Task.CompletedTask;
    }
}
```
### Available Lifecycle Methods

| Method | Description |
|--------|-------------|
| `OnAppearingAsync()` | Called when the page appears. Use for data refresh. |
| `OnDisappearingAsync()` | Called when the page disappears. Use for cleanup. |
| `InitializeAsync(params object[] args)` | Called after navigation with parameters. |
| `OnAppStartAsync()` | Called when the application starts. |
| `OnAppEnterForegroundAsync()` | Called when app enters foreground (Android: OnResume, iOS: OnActivated). |
| `OnAppEnterBackgroundAsync()` | Called when app enters background (Android: OnPause, iOS: OnResignActivation). |

---

## Navigation Service

### INavigationService

The `INavigationService` provides type-safe navigation between pages:
```
csharp
public partial class MainPageViewModel(INavigationService navigationService) : ViewModelBase
{
    [RelayCommand]
    private async Task NavigateAsync()
    {
        // Simple navigation without parameters
        await navigationService.NavigateToAsync<SecondPage>();

        // Navigation with multiple parameters
        await navigationService.NavigateToAsync<SecondPage>(
            "test",                              // string parameter
            1,                                   // int parameter
            new List<object> { "test", 1 }       // complex object
        );
    }
}
```
### Receiving Parameters

In your destination ViewModel, override `InitializeAsync` to receive the parameters:
```
csharp
public partial class SecondPageViewModel : ViewModelBase
{
    public override Task InitializeAsync(params object[] args)
    {
        if (args.Length > 0)
        {
            var firstParam = args[0] as string;    // "test"
            var secondParam = (int)args[1];        // 1
            var thirdParam = args[2] as List<object>;
        }

        return Task.CompletedTask;
    }
}
```
---

## Page Setup

### XAML Configuration

Pages should inherit from `BaseContentPage<TViewModel>`:
```
xml
<?xml version="1.0" encoding="utf-8"?>
<views:BaseContentPage x:TypeArguments="viewModels:MainPageViewModel"
                       xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                       xmlns:views="clr-namespace:MobileConcept.Core.Views;assembly=MobileConcept.Core"
                       xmlns:viewModels="clr-namespace:MobileConceptSample.ViewModels"
                       x:Class="MobileConceptSample.MainPage">

    <!-- Your page content -->

</views:BaseContentPage>
```
### Code-Behind Configuration

Enable lifecycle events in your page's code-behind:
```
csharp
using MobileConcept.Core.Views;
using MobileConceptSample.ViewModels;

namespace MobileConceptSample;

public partial class MainPage
{
    public MainPage(MainPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
        EnableLifecycleEvents = true;  // Enable automatic lifecycle event forwarding
    }
}
```
### AppShell Configuration

Register your routes in `AppShell.xaml`:
```
xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell x:Class="MobileConceptSample.AppShell"
       xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
       xmlns:local="clr-namespace:MobileConceptSample"
       Title="MobileConceptSample">

    <ShellContent
        Title="Home"
        ContentTemplate="{DataTemplate local:MainPage}"
        Route="MainPage" />

</Shell>
```
---

## Complete Example

### MainPageViewModel.cs
```
csharp
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileConcept.Core.Services;
using MobileConcept.Core.ViewModels;

namespace MobileConceptSample.ViewModels;

public partial class MainPageViewModel(INavigationService navigationService) : ViewModelBase
{
    private int count = 0;

    [ObservableProperty] 
    private string _counterBtnText = "Click me";
    
    [RelayCommand]
    private async Task CounterAsync()
    {
        count++;
        
        CounterBtnText = count == 1 
            ? $"Clicked {count} time" 
            : $"Clicked {count} times";
        
        SemanticScreenReader.Announce(CounterBtnText);
        
        // Navigate after 5 clicks with parameters
        if (count >= 5)
        {
            await navigationService.NavigateToAsync<SecondPage>(
                "test", 
                1, 
                new List<object> { "test", 1 }
            );
        }
    }

    public override Task OnAppearingAsync()
    {
        // Load or refresh data when page appears
        return Task.CompletedTask;
    }

    public override Task OnAppEnterForegroundAsync()
    {
        Debug.WriteLine("MainPageViewModel.OnAppEnterForegroundAsync");
        return base.OnAppEnterForegroundAsync();
    }

    public override Task OnAppEnterBackgroundAsync()
    {
        Debug.WriteLine("MainPageViewModel.OnAppEnterBackgroundAsync");
        return base.OnAppEnterBackgroundAsync();
    }
}
```
---

## Image Helpers

### Image Rotation and Resizing

The `ImageRotationHelper` class helps you handle images from the camera or photo library, automatically managing EXIF rotation and resizing:
```
csharp
using MobileConcept.Maui.Core.Helpers;

private async Task MediaClickAsync(string mediaType)
{
    var options = new MediaPickerOptions { SelectionLimit = 1 };
    
    if (mediaType == "camera")
    {
        var result = await MediaPicker.CapturePhotoAsync(options);
        if (result is not null)
        {
            // Capture and process photo
            var stream = await result.OpenReadAsync();
            var resizedAndRotatedStream = ImageRotationHelper.ResizeProportionalWithExifRotation(
                stream, 
                maxWidth: 400, 
                maxHeight: 400
            );
            
            // Use the processed stream
            // ...
        }
    }
    else if (mediaType == "library")
    {
        var result = await MediaPicker.PickPhotosAsync(options);
        if (result.Count == 1)
        {
            // Select and process photo from library
            var stream = await result[0].OpenReadAsync();
            var resizedAndRotatedStream = ImageRotationHelper.ResizeProportionalWithExifRotation(
                stream, 
                maxWidth: 400, 
                maxHeight: 400
            );
            
            // Use the processed stream
            // ...
        }
    }
}
```
#### Why use ResizeProportionalWithExifRotation?

- **EXIF Rotation**: Automatically reads and applies the correct orientation from image metadata
- **Proportional Resize**: Maintains aspect ratio while fitting within specified dimensions
- **Memory Efficient**: Optimizes image size for mobile devices
- **Cross-Platform**: Works consistently on both Android and iOS

---

## Project Structure
```

MyMauiApp/
├── Bootstrap/
│   └── AppBootstrapper.cs      # Your custom bootstrapper
├── ViewModels/
│   ├── MainPageViewModel.cs
│   └── SecondPageViewModel.cs
├── Views (or root)/
│   ├── MainPage.xaml
│   ├── MainPage.xaml.cs
│   ├── SecondPage.xaml
│   └── SecondPage.xaml.cs
├── Services/
│   └── MyService.cs
├── App.xaml
├── App.xaml.cs
├── AppShell.xaml
├── AppShell.xaml.cs
└── MauiProgram.cs
```
---

## Best Practices

1. **Always call `.UseMobileConcept()`** - This is required for lifecycle events to work
2. **Use the Bootstrapper pattern** - Keep DI registrations organized and maintainable
3. **Always enable lifecycle events** - Set `EnableLifecycleEvents = true` in your page constructor
4. **Use `OnAppearingAsync` for data loading** - This ensures fresh data when returning to a page
5. **Clean up in `OnDisappearingAsync`** - Cancel subscriptions and release resources
6. **Use `OnAppEnterBackgroundAsync` to save state** - Persist important data before the app is suspended
7. **Pass only serializable parameters** - Keep navigation parameters simple and serializable

---

## Platform Support

| Platform | Foreground Event | Background Event |
|----------|-----------------|------------------|
| Android | `OnResume` | `OnPause` |
| iOS | `OnActivated` | `OnResignActivation` |

---

## Documentation

For more information, visit [Mobile Concept Blog](https://www.mobile-concept.com)

## License

MIT

## Support

For issues and questions, please visit our [GitHub repository](https://github.com/maui-plaroche/MobileConcept.Maui.Core)

---

Part of the **MobileConcept.Maui** suite of packages for .NET MAUI development.
```

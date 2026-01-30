# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MobileConcept.Core** is a .NET MAUI library distributed as a NuGet package that provides essential utilities, base classes, and patterns for building cross-platform mobile applications. The library targets .NET 9.0+ on Android (API 23+) and iOS (15.0+).

**Current Version:** 1.0.6
**Package ID:** MobileConcept.Core

## Build & Development Commands

### Building the Project
```bash
# Build for all target frameworks (Android + iOS)
dotnet build MobileConcept.Core.sln

# Build for specific target framework
dotnet build MobileConcept.Core/MobileConcept.Core.csproj -f net9.0-android
dotnet build MobileConcept.Core/MobileConcept.Core.csproj -f net9.0-ios

# Build in Release mode
dotnet build MobileConcept.Core.sln -c Release
```

### Creating NuGet Package
```bash
# Create NuGet package (outputs to nupkgs/ directory)
dotnet pack MobileConcept.Core/MobileConcept.Core.csproj -c Release -o nupkgs/

# The package includes:
# - Multi-targeted DLLs (Android + iOS)
# - XML documentation (from <GenerateDocumentationFile>)
# - README.md and icon.png from parent directory
```

### SDK Version
The project requires .NET SDK 9.0.305 or later (defined in `global.json`).

## High-Level Architecture

### Core Design Patterns

**1. Template Method Pattern (Bootstrapper)**
The `BootstrapperBase` class defines a dependency injection registration pipeline with 10 distinct stages that execute in a fixed order. Applications create a custom bootstrapper by inheriting from `BootstrapperBase` and overriding specific registration methods:

```
Initialize() sequence:
RegisterEssentials() → RegisterPlatformServices() → RegisterServices() →
RegisterSerialization() → RegisterApi() → RegisterCommonServices() →
RegisterRepository() → RegisterViewModels() → RegisterViews() → RegisterHandlers()
```

This ensures consistent initialization order across all applications while allowing customization at each stage.

**2. Three-Tier Lifecycle System**
The library implements a hierarchical lifecycle event system:

- **App Lifecycle** (Platform-specific): Maps Android `OnResume`/`OnPause` and iOS `OnActivated`/`OnResignActivation` to `OnAppEnterForegroundAsync`/`OnAppEnterBackgroundAsync`
- **Page Lifecycle** (MAUI standard): `OnAppearingAsync`/`OnDisappearingAsync` called when pages appear/disappear
- **Navigation Lifecycle** (Custom): `InitializeAsync(params object[] args)` called when navigating with parameters

All lifecycle events are asynchronous and propagate from the page to its bound ViewModel automatically when `EnableLifecycleEvents = true` is set in the page constructor.

**3. Type-Safe Generic Base Classes**
- `BaseContentPage<TViewModel>`: Generic page base class enforcing ViewModel type at compile time
- `ViewModelBase`: Inherits from CommunityToolkit's `ObservableObject`, implements all lifecycle interfaces
- Pages receive strongly-typed ViewModel via constructor injection, eliminating runtime casting

**4. Railway-Oriented Programming (RoP)**
The `Result<T>` record type enables functional error handling with composition operators:
- `Map<T,U>`: Transforms successful results
- `Bind<T,U>`: Chains operations that return `Result<U>`

This provides an alternative to exception-based control flow for service layer operations.

### Directory Structure & Responsibilities

**Bootstrap/** - DI configuration using Template Method pattern
- `BootstrapperBase.cs`: Abstract base with 10 registration stages
- `Bootstrapper.cs`: Concrete implementation registering core services

**Extensions/** - MAUI integration
- `AppBuilderExtensions.cs`: Contains `UseMobileConcept()` which wires up platform lifecycle events and initializes the bootstrapper

**Services/** - Navigation abstraction
- `NavigationService.cs`: Supports Shell, NavigationPage, FlyoutPage, TabbedPage patterns with type-safe generics

**ViewModels/** - MVVM foundation
- `ViewModelBase.cs`: Base class with all lifecycle methods as virtual `Task`-returning methods
- Interfaces: `IAppLifeCycleViewModel`, `IViewModelBase`

**Views/** - Page base classes
- `BaseContentPage<T>.cs`: Generic base page with automatic lifecycle event forwarding to ViewModel

**Tasks/** - Advanced async utilities
- `TaskPool.cs`: Bounded concurrent task executor (limits parallel execution, completion callbacks)
- `Debouncer.cs`: Rate-limiting utility with configurable delay (default 300ms)

**RoP/** - Functional programming utilities
- `Result<T>.cs`: Record-based result type
- `ResultExtensions.cs`: Composition operators for method chaining

**Helpers/** - Platform utilities
- `PlatformHelper.cs`: Static accessor for MAUI runtime objects (Application, Window, MainPage, Services)

**Images/** - Media processing
- `ImageRotationHelper.cs`: SkiaSharp-based EXIF-aware image rotation/resizing with two methods:
  - `ResizeWithExifRotation()`: Exact dimensions with cropping (profile images)
  - `ResizeProportionalWithExifRotation()`: Max bounds preserving aspect ratio (gallery images)

**Handlers/** - Empty directory reserved for custom control handlers

### Platform-Specific Code

Platform code uses preprocessor directives:
```csharp
#if ANDROID
    // Android-specific implementation
#endif

#if IOS
    // iOS-specific implementation
#endif
```

The project multi-targets `net9.0-android` and `net9.0-ios`, producing separate compiled assemblies.

### Service Registration Flow

```
MauiProgram.cs calls .UseMobileConcept()
    ↓
AppBuilderExtensions creates Bootstrapper instance
    ↓
bootstrapper.Initialize() executes 10 registration stages sequentially
    ↓
Platform lifecycle events configured (Android/iOS)
    ↓
Services available via DI throughout app lifetime
```

### Integration Pattern for Consuming Applications

1. Install NuGet package `MobileConcept.Core`
2. Add `.UseMobileConcept()` to `MauiApp.CreateBuilder()` chain in `MauiProgram.cs`
3. Create custom `AppBootstrapper : BootstrapperBase` and call `bootstrapper.Initialize()` after builder setup
4. Create ViewModels inheriting from `ViewModelBase`
5. Create Pages inheriting from `BaseContentPage<TViewModel>`
6. Set `EnableLifecycleEvents = true` in page constructors
7. Use `INavigationService` for type-safe navigation

### Key Design Decisions

**TaskPool Over Task.Run**: Provides bounded concurrency control to prevent resource exhaustion on mobile devices. Completion callbacks enable UI updates without `Task.ContinueWith` complexity.

**Static PlatformHelper**: Pragmatic trade-off for accessing global MAUI singletons (like `Application.Current`) without excessive DI boilerplate.

**Generic Page Base Class**: Compile-time type safety for ViewModel binding eliminates runtime casting errors.

**Event-Based Lifecycle**: Platform lifecycle events (foreground/background) automatically route to the currently visible page's ViewModel, simplifying state management.

### Package Publishing Notes

- `GeneratePackageOnBuild` is set to `false` - use `dotnet pack` manually
- Package output directory: `nupkgs/` (excluded from git via `.gitignore`)
- Version controlled in `.csproj`: Update `<Version>1.0.6</Version>` before packing
- Package includes XML documentation from `<GenerateDocumentationFile>`
- README.md and icon.png are included from parent directory

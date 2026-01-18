using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace MobileConcept.Core.Bootstrap;

/// <summary>
/// Provides a base class for application bootstrapping and dependency injection configuration.
/// Inherit from this class to customize service registration for your platform-specific needs.
/// </summary>
public class BootstrapperBase
{
    /// <summary>
    /// Gets the service collection used for dependency injection registrations.
    /// </summary>
    protected IServiceCollection Container { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BootstrapperBase"/> class.
    /// </summary>
    /// <param name="container">The service collection to register dependencies into.</param>
    protected BootstrapperBase(IServiceCollection container)
    {
        Container = container;
    }

    /// <summary>
    /// Initializes the application by registering all required services in the dependency injection container.
    /// Call this method during application startup to configure all dependencies.
    /// </summary>
    public void Initialize()
    {
        RegisterEssentials();
        RegisterPlatformServices();
        RegisterServices();
        RegisterSerialization();
        RegisterApi();
        RegisterCommonServices();
        RegisterRepository();
        RegisterViewModels();
        RegisterViews();
        RegisterHandlers();
#if DEBUG
        Debug.WriteLine("Container Registred");
#endif
    }

    /// <summary>
    /// Registers JSON serialization services. Override to provide custom serialization configuration.
    /// </summary>
    protected virtual void RegisterSerialization()
    {
    }

    /// <summary>
    /// Registers API-related services such as API clients and configurations.
    /// </summary>
    protected virtual void RegisterApi()
    {
    }

    /// <summary>
    /// Registers essential platform services like secure storage, device info, and preferences.
    /// </summary>
    protected virtual void RegisterEssentials()
    {
    }

    /// <summary>
    /// Registers custom handlers for the application. Override to add platform-specific handlers.
    /// </summary>
    protected virtual void RegisterHandlers()
    {
    }

    /// <summary>
    /// Registers common cross-platform services such as connectivity and phone services.
    /// </summary>
    protected virtual void RegisterCommonServices()
    {
    }

    /// <summary>
    /// Registers view models for the MVVM pattern. Override to add your application's view models.
    /// </summary>
    protected virtual void RegisterViewModels()
    {
    }

    /// <summary>
    /// Registers views/pages for navigation. Override to add your application's views.
    /// </summary>
    protected virtual void RegisterViews()
    {
    }

    /// <summary>
    /// Registers application services including error handling, task management, and HTTP configuration.
    /// </summary>
    protected virtual void RegisterServices()
    {
    }

    /// <summary>
    /// Registers data repositories for data access operations.
    /// </summary>
    protected virtual void RegisterRepository()
    {
    }

    /// <summary>
    /// Registers platform-specific services. Override in platform projects to add native implementations.
    /// </summary>
    protected virtual void RegisterPlatformServices()
    {
    }
}
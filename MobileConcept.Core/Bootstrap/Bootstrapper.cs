using Microsoft.Extensions.DependencyInjection;
using MobileConcept.Core.Services;

namespace MobileConcept.Core.Bootstrap;

/// <summary>
/// Represents a platform-specific application bootstrapper responsible for configuring
/// dependency injection during application startup.
/// This class inherits from <see cref="BootstrapperBase"/> and allows customization
/// of service registration tailored to the application's requirements.
/// </summary>
public class Bootstrapper(IServiceCollection container) : BootstrapperBase(container)
{
    /// <summary>
    /// Registers application-specific services into the dependency injection container.
    /// This method is called during the initialization process to add custom services
    /// or override the default service registrations provided by the base class.
    /// </summary>
    protected override void RegisterServices()
    {
        base.RegisterServices();
        Container.AddSingleton<INavigationService, NavigationService>();
    }
}
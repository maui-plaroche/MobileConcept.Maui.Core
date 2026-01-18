using Microsoft.Extensions.DependencyInjection;

namespace MobileConcept.Core.Bootstrap;

/// <summary>
/// Represents a platform-specific application bootstrapper responsible for configuring
/// dependency injection during application startup.
/// This class inherits from <see cref="BootstrapperBase"/> and allows customization
/// of service registration tailored to the application's requirements.
/// </summary>
public class Bootstrapper(IServiceCollection container) : BootstrapperBase(container);
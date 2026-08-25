using Microsoft.Extensions.DependencyInjection;
using System;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Information about a registered service tracked by EasyDI.
    /// </summary>
    public record ServiceRegistrationInfo(
        Type ServiceType,
        Type ImplementationType,
        ServiceLifetime Lifetime,
        string Source,
        DateTime RegisteredAt,
        object? ServiceKey = null
    );
}

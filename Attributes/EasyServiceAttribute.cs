using Microsoft.Extensions.DependencyInjection;
using System;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Marks a class for automatic service registration with specified lifetime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class EasyServiceAttribute : Attribute
    {
        /// <summary>
        /// Lifetime of the registered service. Default is <see cref="ServiceLifetime.Scoped"/>.
        /// </summary>
        public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Scoped;

        /// <summary>
        /// If true, replaces existing registration for the service type if found.
        /// </summary>
        public bool Replace { get; init; } = false;

        public EasyServiceAttribute() { }

        public EasyServiceAttribute(ServiceLifetime lifetime)
        {
            Lifetime = lifetime;
        }
    }
}

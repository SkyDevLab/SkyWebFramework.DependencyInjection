using Microsoft.Extensions.DependencyInjection;
using System;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Registers the service with Singleton lifetime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SingletonAttribute : EasyServiceAttribute
    {
        public SingletonAttribute() : base(ServiceLifetime.Singleton) { }
    }

    /// <summary>
    /// Registers the service with Scoped lifetime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ScopedAttribute : EasyServiceAttribute
    {
        public ScopedAttribute() : base(ServiceLifetime.Scoped) { }
    }

    /// <summary>
    /// Registers the service with Transient lifetime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class TransientAttribute : EasyServiceAttribute
    {
        public TransientAttribute() : base(ServiceLifetime.Transient) { }
    }
}

using Microsoft.Extensions.DependencyInjection;
using System;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Explicitly specifies which interface or base type to use for registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class RegisterAsAttribute : Attribute
    {
        public Type ServiceType { get; }
        public ServiceLifetime? Lifetime { get; init; }

        public RegisterAsAttribute(Type serviceType)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        }
    }

    /// <summary>
    /// Registers all implemented interfaces for this service.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RegisterAllInterfacesAttribute : Attribute { }

    /// <summary>
    /// Excludes a class from automatic service registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class IgnoreServiceAttribute : Attribute { }

    /// <summary>
    /// Marks a service as a decorator for another service type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DecoratorAttribute : Attribute
    {
        public Type DecoratedType { get; }

        public DecoratorAttribute(Type decoratedType)
        {
            DecoratedType = decoratedType ?? throw new ArgumentNullException(nameof(decoratedType));
        }
    }

    /// <summary>
    /// Registers service with a static factory method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class ServiceFactoryAttribute : Attribute
    {
        public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Scoped;

        public ServiceFactoryAttribute() { }

        public ServiceFactoryAttribute(ServiceLifetime lifetime)
        {
            Lifetime = lifetime;
        }
    }

    /// <summary>
    /// Registers a service as a keyed service (.NET 8+).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class KeyedServiceAttribute : Attribute
    {
        public object ServiceKey { get; }
        public Type? ServiceType { get; init; }
        public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Scoped;

        public KeyedServiceAttribute(object serviceKey)
        {
            ServiceKey = serviceKey ?? throw new ArgumentNullException(nameof(serviceKey));
        }

        public KeyedServiceAttribute(object serviceKey, ServiceLifetime lifetime)
        {
            ServiceKey = serviceKey ?? throw new ArgumentNullException(nameof(serviceKey));
            Lifetime = lifetime;
        }
    }

    /// <summary>
    /// Marks a service module class with an optional name and registration priority.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ServiceModuleAttribute : Attribute
    {
        public string Name { get; }
        public int Priority { get; init; }

        public ServiceModuleAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// Marks a service profile class with a name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ServiceProfileAttribute : Attribute
    {
        public string Name { get; }

        public ServiceProfileAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}

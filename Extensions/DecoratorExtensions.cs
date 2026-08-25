using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Extension methods for applying decorator patterns to registered services.
    /// </summary>
    public static class DecoratorExtensions
    {
        /// <summary>
        /// Decorates an already registered service with a decorator implementation, preserving the original lifetime.
        /// </summary>
        public static IServiceCollection Decorate<TService, TDecorator>(
            this IServiceCollection services)
            where TService : class
            where TDecorator : class, TService
        {
            var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(TService));
            if (descriptor == null)
            {
                throw new InvalidOperationException(
                    $"Service '{typeof(TService).FullName}' is not registered and cannot be decorated.");
            }

            services.Remove(descriptor);

            ServiceDescriptor decoratedDescriptor;

            if (descriptor.ImplementationInstance != null)
            {
                var instance = (TService)descriptor.ImplementationInstance;
                decoratedDescriptor = ServiceDescriptor.Describe(
                    typeof(TService),
                    provider => ActivatorUtilities.CreateInstance<TDecorator>(provider, instance),
                    ServiceLifetime.Singleton
                );
            }
            else if (descriptor.ImplementationFactory != null)
            {
                var factory = descriptor.ImplementationFactory;
                decoratedDescriptor = ServiceDescriptor.Describe(
                    typeof(TService),
                    provider =>
                    {
                        var instance = (TService)factory(provider)!;
                        return ActivatorUtilities.CreateInstance<TDecorator>(provider, instance);
                    },
                    descriptor.Lifetime
                );
            }
            else
            {
                var implType = descriptor.ImplementationType!;
                decoratedDescriptor = ServiceDescriptor.Describe(
                    typeof(TService),
                    provider =>
                    {
                        var instance = (TService)ActivatorUtilities.CreateInstance(provider, implType);
                        return ActivatorUtilities.CreateInstance<TDecorator>(provider, instance);
                    },
                    descriptor.Lifetime
                );
            }

            services.Add(decoratedDescriptor);
            return services;
        }

        /// <summary>
        /// Decorates a service with multiple decorators in the specified order.
        /// </summary>
        public static IServiceCollection DecorateWith<TService>(
            this IServiceCollection services,
            params Type[] decoratorTypes)
            where TService : class
        {
            if (decoratorTypes == null || decoratorTypes.Length == 0)
                return services;

            foreach (var decoratorType in decoratorTypes)
            {
                if (!typeof(TService).IsAssignableFrom(decoratorType))
                {
                    throw new ArgumentException($"Type '{decoratorType.Name}' does not implement service type '{typeof(TService).Name}'.");
                }

                var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(TService));
                if (descriptor == null)
                {
                    throw new InvalidOperationException($"Service '{typeof(TService).FullName}' is not registered and cannot be decorated.");
                }

                services.Remove(descriptor);

                var currentDescriptor = descriptor;
                var currentLifetime = currentDescriptor.Lifetime;

                ServiceDescriptor newDescriptor = ServiceDescriptor.Describe(
                    typeof(TService),
                    provider =>
                    {
                        object decoratedInstance;
                        if (currentDescriptor.ImplementationInstance != null)
                        {
                            decoratedInstance = currentDescriptor.ImplementationInstance;
                        }
                        else if (currentDescriptor.ImplementationFactory != null)
                        {
                            decoratedInstance = currentDescriptor.ImplementationFactory(provider)!;
                        }
                        else
                        {
                            decoratedInstance = ActivatorUtilities.CreateInstance(provider, currentDescriptor.ImplementationType!);
                        }

                        return ActivatorUtilities.CreateInstance(provider, decoratorType, decoratedInstance);
                    },
                    currentLifetime
                );

                services.Add(newDescriptor);
            }

            return services;
        }
    }
}

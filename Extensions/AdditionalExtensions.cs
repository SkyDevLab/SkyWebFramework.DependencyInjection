using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Extension methods for conditional and environment-based service registrations.
    /// </summary>
    public static class ConditionalRegistrationExtensions
    {
        public static IServiceCollection AddEasyServiceWhen<TService, TImplementation>(
            this IServiceCollection services,
            Func<bool> condition,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService
        {
            if (condition())
            {
                services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
            }
            return services;
        }

        public static IServiceCollection AddEasyServiceForEnvironment<TService, TImplementation>(
            this IServiceCollection services,
            string environment,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService
        {
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            if (currentEnv.Equals(environment, StringComparison.OrdinalIgnoreCase))
            {
                services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
            }
            return services;
        }

        public static IServiceCollection AddEasyServiceByEnvironment<TService>(
            this IServiceCollection services,
            Type productionImplementation,
            Type developmentImplementation,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
        {
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var implementation = currentEnv.Equals("Development", StringComparison.OrdinalIgnoreCase)
                ? developmentImplementation
                : productionImplementation;

            services.Add(new ServiceDescriptor(typeof(TService), implementation, lifetime));
            return services;
        }
    }

    /// <summary>
    /// Keyed service registration helpers (.NET 8+).
    /// </summary>
    public static class KeyedServiceExtensions
    {
        public static IServiceCollection AddKeyedEasyService<TService, TImplementation>(
            this IServiceCollection services,
            object serviceKey,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService
        {
            services.Add(new ServiceDescriptor(
                typeof(TService),
                serviceKey,
                typeof(TImplementation),
                lifetime));
            return services;
        }

        public static IServiceCollection AddNamedEasyServices<TService>(
            this IServiceCollection services,
            params (string name, Type implementation)[] namedImplementations)
            where TService : class
        {
            foreach (var (name, implementation) in namedImplementations)
            {
                services.AddKeyedScoped(typeof(TService), name, implementation);
            }
            return services;
        }
    }

    /// <summary>
    /// Batch service registration helpers.
    /// </summary>
    public static class BatchRegistrationExtensions
    {
        public static IServiceCollection AddAllImplementationsOf<TInterface>(
            this IServiceCollection services,
            Assembly assembly,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            var interfaceType = typeof(TInterface);
            var implementations = Scanning.ReflectionExtensions.GetLoadableTypes(assembly)
                .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t))
                .ToList();

            foreach (var implementation in implementations)
            {
                services.Add(new ServiceDescriptor(interfaceType, implementation, lifetime));
            }

            return services;
        }

        public static IServiceCollection AddTypesThat(
            this IServiceCollection services,
            Assembly assembly,
            Func<Type, bool> predicate,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            var types = Scanning.ReflectionExtensions.GetLoadableTypes(assembly)
                .Where(t => t.IsClass && !t.IsAbstract && predicate(t))
                .ToList();

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces().Where(i => !i.IsGenericTypeDefinition && !Scanning.ReflectionExtensions.IsIgnoredInterface(i)).ToList();
                var primaryInterface = interfaces.FirstOrDefault(i => i.Name == "I" + type.Name) ?? interfaces.FirstOrDefault();

                if (primaryInterface != null)
                {
                    services.Add(new ServiceDescriptor(primaryInterface, type, lifetime));
                }
                else
                {
                    services.Add(new ServiceDescriptor(type, type, lifetime));
                }
            }

            return services;
        }
    }

    /// <summary>
    /// Lazy resolution registration extensions.
    /// </summary>
    public static class LazyRegistrationExtensions
    {
        public static IServiceCollection AddLazyEasyService<TService>(
            this IServiceCollection services)
            where TService : class
        {
            services.AddTransient(provider => new Lazy<TService>(provider.GetRequiredService<TService>));
            return services;
        }

        public static IServiceCollection EnableLazyResolution(
            this IServiceCollection services)
        {
            var registeredServiceTypes = services
                .Where(d => !d.ServiceType.IsGenericTypeDefinition && !d.IsKeyedService)
                .Select(d => d.ServiceType)
                .Distinct()
                .ToList();

            foreach (var serviceType in registeredServiceTypes)
            {
                var lazyType = typeof(Lazy<>).MakeGenericType(serviceType);

                if (!services.Any(s => !s.IsKeyedService && s.ServiceType == lazyType))
                {
                    services.Add(ServiceDescriptor.Describe(
                        lazyType,
                        provider =>
                        {
                            var funcType = typeof(Func<>).MakeGenericType(serviceType);
                            var method = typeof(ServiceProviderServiceExtensions)
                                .GetMethods()
                                .First(m => m.Name == nameof(ServiceProviderServiceExtensions.GetRequiredService) && m.IsGenericMethod && m.GetParameters().Length == 1)
                                .MakeGenericMethod(serviceType);

                            var del = Delegate.CreateDelegate(funcType, provider, method);
                            return Activator.CreateInstance(lazyType, del)!;
                        },
                        ServiceLifetime.Transient));
                }
            }

            return services;
        }
    }

    /// <summary>
    /// Assembly scanning helpers.
    /// </summary>
    public static class AssemblyScanningExtensions
    {
        public static IServiceCollection AddEasyServicesFromReferencedAssemblies(
            this IServiceCollection services,
            Assembly entryAssembly,
            Action<EasyDiOptions>? configure = null)
        {
            var referencedAssemblies = entryAssembly.GetReferencedAssemblies()
                .Select(Assembly.Load)
                .Append(entryAssembly);

            return services.AddEasyServicesFromAssemblies(referencedAssemblies, configure);
        }

        public static IServiceCollection AddEasyServicesFromAssembliesMatching(
            this IServiceCollection services,
            string assemblyPrefix,
            Action<EasyDiOptions>? configure = null)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name?.StartsWith(assemblyPrefix, StringComparison.OrdinalIgnoreCase) == true);

            return services.AddEasyServicesFromAssemblies(assemblies, configure);
        }
    }

    /// <summary>
    /// Interceptor support.
    /// </summary>
    public static class InterceptorExtensions
    {
        public static IServiceCollection InterceptService<TService>(
            this IServiceCollection services,
            Func<TService, IServiceProvider, TService> interceptor)
            where TService : class
        {
            var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(TService));
            if (descriptor == null)
            {
                throw new InvalidOperationException($"Service '{typeof(TService).FullName}' is not registered and cannot be intercepted.");
            }

            services.Remove(descriptor);

            services.Add(ServiceDescriptor.Describe(
                typeof(TService),
                provider =>
                {
                    TService instance;
                    if (descriptor.ImplementationInstance != null)
                    {
                        instance = (TService)descriptor.ImplementationInstance;
                    }
                    else if (descriptor.ImplementationFactory != null)
                    {
                        instance = (TService)descriptor.ImplementationFactory(provider)!;
                    }
                    else
                    {
                        instance = (TService)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
                    }
                    return interceptor(instance, provider);
                },
                descriptor.Lifetime));

            return services;
        }
    }

    /// <summary>
    /// Diagnostics export extensions.
    /// </summary>
    public static class DiagnosticExtensions
    {
        public static ServiceDiagnostics GetDiagnostics(this IServiceCollection services)
        {
            var registrations = EasyDiRegistry.GetRegistrations(services);

            return new ServiceDiagnostics
            {
                TotalServices = registrations.Count,
                SingletonCount = registrations.Count(r => r.Lifetime == ServiceLifetime.Singleton),
                ScopedCount = registrations.Count(r => r.Lifetime == ServiceLifetime.Scoped),
                TransientCount = registrations.Count(r => r.Lifetime == ServiceLifetime.Transient),
                RegistrationsBySource = registrations.GroupBy(r => r.Source)
                    .ToDictionary(g => g.Key, g => g.Count()),
                OldestRegistration = registrations.MinBy(r => r.RegisteredAt)?.RegisteredAt,
                NewestRegistration = registrations.MaxBy(r => r.RegisteredAt)?.RegisteredAt,
                Registrations = registrations
            };
        }

        public static string ExportRegistrationsAsJson(this IServiceCollection services)
        {
            var registrations = EasyDiRegistry.GetRegistrations(services);
            var data = registrations.Select(r => new
            {
                ServiceType = r.ServiceType.FullName,
                ImplementationType = r.ImplementationType.FullName,
                Lifetime = r.Lifetime.ToString(),
                Source = r.Source,
                RegisteredAt = r.RegisteredAt.ToString("o"),
                ServiceKey = r.ServiceKey?.ToString()
            });

            return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
}

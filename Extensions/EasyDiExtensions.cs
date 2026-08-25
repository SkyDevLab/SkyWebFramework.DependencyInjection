using Microsoft.Extensions.DependencyInjection;
using SkyWebFramework.DependencyInjection.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Core EasyDI extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class EasyDiExtensions
    {
        /// <summary>
        /// Automatically registers services from the calling assembly using conventions and attributes.
        /// </summary>
        public static IServiceCollection AddEasyServices(
            this IServiceCollection services,
            Action<EasyDiOptions>? configure = null)
        {
            var callingAssembly = Assembly.GetCallingAssembly();
            return services.AddEasyServicesFromAssembly(callingAssembly, configure);
        }

        /// <summary>
        /// Automatically registers services from the specified assembly with options.
        /// </summary>
        public static IServiceCollection AddEasyServicesFromAssembly(
            this IServiceCollection services,
            Assembly assembly,
            Action<EasyDiOptions>? configure = null)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            var options = new EasyDiOptions();
            configure?.Invoke(options);

            ServiceRegistrationEngine.Register(services, new[] { assembly }, options);

            if (options.AutoValidate)
            {
                services.ValidateEasyServices(throwOnError: false);
            }

            return services;
        }

        /// <summary>
        /// Registers services from multiple assemblies.
        /// </summary>
        public static IServiceCollection AddEasyServicesFromAssemblies(
            this IServiceCollection services,
            IEnumerable<Assembly> assemblies,
            Action<EasyDiOptions>? configure = null)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

            var options = new EasyDiOptions();
            configure?.Invoke(options);

            ServiceRegistrationEngine.Register(services, assemblies, options);

            if (options.AutoValidate)
            {
                services.ValidateEasyServices(throwOnError: false);
            }

            return services;
        }

        /// <summary>
        /// Registers services from the assembly containing the specified type.
        /// </summary>
        public static IServiceCollection AddEasyServicesFromAssemblyContaining<T>(
            this IServiceCollection services,
            Action<EasyDiOptions>? configure = null)
        {
            return services.AddEasyServicesFromAssembly(typeof(T).Assembly, configure);
        }

        /// <summary>
        /// Scans all loaded assemblies in the AppDomain for services.
        /// </summary>
        public static IServiceCollection AddEasyServicesFromAllAssemblies(
            this IServiceCollection services,
            Action<EasyDiOptions>? configure = null)
        {
            var options = new EasyDiOptions();
            configure?.Invoke(options);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !ServiceRegistrationPlanner.IsExcludedAssembly(a, options));

            return services.AddEasyServicesFromAssemblies(assemblies, configure);
        }

        /// <summary>
        /// Registers a single service type using convention and attribute inspection.
        /// </summary>
        public static IServiceCollection AddEasyService<TImplementation>(
            this IServiceCollection services,
            ServiceLifetime? lifetime = null)
            where TImplementation : class
        {
            var options = new EasyDiOptions();
            if (lifetime.HasValue)
                options.DefaultLifetime = lifetime.Value;

            var plan = ServiceRegistrationPlanner.BuildPlan(new[] { typeof(TImplementation).Assembly }, options);
            var itemForType = plan.Items.Where(i => i.ImplementationType == typeof(TImplementation)).ToList();

            var customPlan = new ServiceRegistrationPlan();
            customPlan.Items.AddRange(itemForType);

            ServiceRegistrationExecutor.Execute(services, customPlan, options);
            return services;
        }

        /// <summary>
        /// Previews services that would be registered from an assembly without mutating the collection.
        /// </summary>
        public static IReadOnlyList<ServicePreviewInfo> PreviewEasyServicesFromAssembly(
            this IServiceCollection services,
            Assembly assembly,
            Action<EasyDiOptions>? configure = null)
        {
            var options = new EasyDiOptions();
            configure?.Invoke(options);
            return ServiceRegistrationEngine.Preview(services, new[] { assembly }, options);
        }

        /// <summary>
        /// Previews services that would be registered from the assembly containing <typeparamref name="T"/>.
        /// </summary>
        public static IReadOnlyList<ServicePreviewInfo> PreviewEasyServicesFromAssemblyContaining<T>(
            this IServiceCollection services,
            Action<EasyDiOptions>? configure = null)
        {
            return services.PreviewEasyServicesFromAssembly(typeof(T).Assembly, configure);
        }

        /// <summary>
        /// Gets information about all registered EasyServices tracked in the registry.
        /// </summary>
        public static List<ServiceRegistrationInfo> GetEasyServiceRegistrations(
            this IServiceCollection services)
        {
            return EasyDiRegistry.GetRegistrations(services);
        }

        /// <summary>
        /// Removes the first registration matching the specified service type.
        /// </summary>
        public static IServiceCollection RemoveEasyService<TService>(
            this IServiceCollection services)
            where TService : class
        {
            var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(TService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            return services;
        }

        /// <summary>
        /// Removes all registrations matching the specified service type.
        /// </summary>
        public static IServiceCollection RemoveAllEasyServices<TService>(
            this IServiceCollection services)
            where TService : class
        {
            var descriptors = services.Where(s => s.ServiceType == typeof(TService)).ToList();
            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }
            return services;
        }

        /// <summary>
        /// Replaces any existing service registration for <typeparamref name="TService"/> with <typeparamref name="TImplementation"/>.
        /// </summary>
        public static IServiceCollection ReplaceEasyService<TService, TImplementation>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService
        {
            services.RemoveAllEasyServices<TService>();
            services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
            return services;
        }

        /// <summary>
        /// Tries to add service only if not already registered.
        /// </summary>
        public static IServiceCollection TryAddEasyService<TService, TImplementation>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
            where TImplementation : class, TService
        {
            if (!services.Any(s => s.ServiceType == typeof(TService)))
            {
                services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
            }
            return services;
        }

        /// <summary>
        /// Adds multiple implementations of the same interface.
        /// </summary>
        public static IServiceCollection AddEasyServiceCollection<TService>(
            this IServiceCollection services,
            params Type[] implementationTypes)
            where TService : class
        {
            foreach (var implType in implementationTypes)
            {
                if (!typeof(TService).IsAssignableFrom(implType))
                {
                    throw new ArgumentException($"{implType.Name} does not implement {typeof(TService).Name}");
                }
                services.AddTransient(typeof(TService), implType);
            }
            return services;
        }
    }
}

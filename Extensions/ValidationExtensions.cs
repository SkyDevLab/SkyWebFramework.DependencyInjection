using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Extension methods for container validation, missing dependencies, and captive dependency inspection.
    /// </summary>
    public static class ValidationExtensions
    {
        /// <summary>
        /// Validates that registered services can be built and resolved using native DI validation.
        /// </summary>
        public static IServiceCollection ValidateEasyServices(
            this IServiceCollection services,
            bool throwOnError = true)
        {
            var errors = new List<string>();

            try
            {
                // Native DI validation: ValidateOnBuild and ValidateScopes
                using var provider = services.BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });

                // Scope test for scoped/transient resolution
                using var scope = provider.CreateScope();
                foreach (var descriptor in services)
                {
                    if (descriptor.ServiceType.IsGenericTypeDefinition)
                        continue;

                    try
                    {
                        if (descriptor.IsKeyedService && scope.ServiceProvider is IKeyedServiceProvider keyedProvider)
                        {
                            keyedProvider.GetKeyedService(descriptor.ServiceType, descriptor.ServiceKey);
                        }
                        else
                        {
                            scope.ServiceProvider.GetService(descriptor.ServiceType);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Failed to resolve '{descriptor.ServiceType.FullName}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"BuildServiceProvider validation failed: {ex.Message}");
            }

            if (errors.Any() && throwOnError)
            {
                throw new InvalidOperationException($"Service validation failed:\n{string.Join("\n", errors)}");
            }

            return services;
        }

        /// <summary>
        /// Detects captive dependencies (e.g. Scoped dependencies injected into Singleton constructors).
        /// </summary>
        public static IServiceCollection DetectCaptiveDependencies(
            this IServiceCollection services,
            bool throwOnError = true)
        {
            var captiveErrors = new List<string>();

            // Build lookup of registered lifetimes
            var lifetimeMap = new Dictionary<Type, ServiceLifetime>();
            foreach (var descriptor in services)
            {
                if (!descriptor.IsKeyedService && !descriptor.ServiceType.IsGenericTypeDefinition)
                {
                    lifetimeMap[descriptor.ServiceType] = descriptor.Lifetime;
                }
            }

            // Check singletons with concrete implementation types
            foreach (var descriptor in services)
            {
                if (descriptor.Lifetime == ServiceLifetime.Singleton && descriptor.ImplementationType != null)
                {
                    var ctors = descriptor.ImplementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var ctor in ctors)
                    {
                        foreach (var param in ctor.GetParameters())
                        {
                            if (lifetimeMap.TryGetValue(param.ParameterType, out var paramLifetime))
                            {
                                if (paramLifetime == ServiceLifetime.Scoped)
                                {
                                    captiveErrors.Add($"Captive Dependency: Singleton '{descriptor.ImplementationType.FullName}' depends on Scoped service '{param.ParameterType.FullName}'.");
                                }
                            }
                        }
                    }
                }
            }

            if (captiveErrors.Any() && throwOnError)
            {
                throw new InvalidOperationException($"Captive dependencies detected:\n{string.Join("\n", captiveErrors)}");
            }

            return services;
        }

        /// <summary>
        /// Checks for circular dependencies in registered services.
        /// </summary>
        public static IServiceCollection DetectCircularDependencies(
            this IServiceCollection services,
            bool throwOnError = true)
        {
            // First check captive dependencies
            DetectCaptiveDependencies(services, throwOnError);

            var circularDeps = new List<string>();

            try
            {
                using var provider = services.BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });

                using var scope = provider.CreateScope();
                foreach (var descriptor in services)
                {
                    if (descriptor.ServiceType.IsGenericTypeDefinition)
                        continue;

                    try
                    {
                        if (descriptor.IsKeyedService && scope.ServiceProvider is IKeyedServiceProvider keyedProvider)
                        {
                            keyedProvider.GetKeyedService(descriptor.ServiceType, descriptor.ServiceKey);
                        }
                        else
                        {
                            scope.ServiceProvider.GetService(descriptor.ServiceType);
                        }
                    }
                    catch (InvalidOperationException ex) when (ex.InnerException != null || ex.Message.Contains("circular", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase))
                    {
                        circularDeps.Add($"Circular dependency detected for '{descriptor.ServiceType.FullName}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                circularDeps.Add($"Validation error during dependency cycle analysis: {ex.Message}");
            }

            if (circularDeps.Any() && throwOnError)
            {
                throw new InvalidOperationException($"Circular dependencies detected:\n{string.Join("\n", circularDeps)}");
            }

            return services;
        }

        /// <summary>
        /// Prints a summary of all EasyDI registrations to standard output.
        /// </summary>
        public static void PrintEasyServiceSummary(this IServiceCollection services)
        {
            var registrations = EasyDiRegistry.GetRegistrations(services);

            Console.WriteLine($"\n=== EasyDI Registration Summary ===");
            Console.WriteLine($"Total services registered: {registrations.Count}");
            Console.WriteLine($"Singleton: {registrations.Count(r => r.Lifetime == ServiceLifetime.Singleton)}");
            Console.WriteLine($"Scoped: {registrations.Count(r => r.Lifetime == ServiceLifetime.Scoped)}");
            Console.WriteLine($"Transient: {registrations.Count(r => r.Lifetime == ServiceLifetime.Transient)}");
            Console.WriteLine($"\nRegistrations by source:");

            foreach (var group in registrations.GroupBy(r => r.Source))
            {
                Console.WriteLine($"  {group.Key}: {group.Count()}");
            }

            Console.WriteLine($"===================================\n");
        }
    }
}

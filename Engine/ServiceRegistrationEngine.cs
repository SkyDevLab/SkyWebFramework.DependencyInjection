using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SkyWebFramework.DependencyInjection.Engine
{
    /// <summary>
    /// Unified engine combining planning, execution, and dry-run previewing of service registrations.
    /// </summary>
    public static class ServiceRegistrationEngine
    {
        /// <summary>
        /// Registers services from the given assemblies into the service collection.
        /// </summary>
        public static IServiceCollection Register(
            IServiceCollection services,
            IEnumerable<Assembly> assemblies,
            EasyDiOptions options)
        {
            var plan = ServiceRegistrationPlanner.BuildPlan(assemblies, options);
            ServiceRegistrationExecutor.Execute(services, plan, options);
            return services;
        }

        /// <summary>
        /// Generates a preview dry-run of services that would be registered without mutating the service collection.
        /// </summary>
        public static IReadOnlyList<ServicePreviewInfo> Preview(
            IServiceCollection services,
            IEnumerable<Assembly> assemblies,
            EasyDiOptions options)
        {
            var plan = ServiceRegistrationPlanner.BuildPlan(assemblies, options);
            var results = new List<ServicePreviewInfo>();

            // Analyze items
            foreach (var item in plan.Items)
            {
                var isKeyed = item.ServiceKey != null;
                var existing = isKeyed
                    ? services.FirstOrDefault(s => s.IsKeyedService && s.ServiceType == item.ServiceType && Equals(s.ServiceKey, item.ServiceKey))
                    : services.FirstOrDefault(s => !s.IsKeyedService && s.ServiceType == item.ServiceType);

                PlannedAction action;
                if (existing != null)
                {
                    if (item.ExplicitReplace || options.DuplicateBehavior == DuplicateRegistrationBehavior.Replace)
                    {
                        action = PlannedAction.Replace;
                    }
                    else if (options.DuplicateBehavior == DuplicateRegistrationBehavior.Skip)
                    {
                        action = PlannedAction.Skip;
                    }
                    else
                    {
                        action = PlannedAction.Register;
                    }
                }
                else
                {
                    action = PlannedAction.Register;
                }

                results.Add(new ServicePreviewInfo(
                    item.ServiceType,
                    item.ImplementationType,
                    item.Lifetime,
                    item.Source,
                    action,
                    item.ServiceKey
                ));
            }

            // Decorators
            foreach (var dec in plan.Decorators)
            {
                results.Add(new ServicePreviewInfo(
                    dec.ServiceType,
                    dec.ImplementationType,
                    dec.Lifetime,
                    dec.Source,
                    PlannedAction.Register
                ));
            }

            // Factories
            foreach (var fact in plan.Factories)
            {
                results.Add(new ServicePreviewInfo(
                    fact.ServiceType,
                    fact.ImplementationType,
                    fact.Lifetime,
                    fact.Source,
                    PlannedAction.Register
                ));
            }

            return results;
        }
    }
}

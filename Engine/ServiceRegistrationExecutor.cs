using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyWebFramework.DependencyInjection.Engine
{
    /// <summary>
    /// Executes a planned service registration against an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceRegistrationExecutor
    {
        public static void Execute(IServiceCollection services, ServiceRegistrationPlan plan, EasyDiOptions options)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (options == null) throw new ArgumentNullException(nameof(options));

            // 1. Register main services
            foreach (var item in plan.Items)
            {
                RegisterItem(services, item, options);
            }

            // 2. Register decorators
            if (options.EnableDecorators)
            {
                foreach (var decoratorItem in plan.Decorators)
                {
                    ApplyDecorator(services, decoratorItem, options);
                }
            }

            // 3. Register factory methods
            foreach (var factoryItem in plan.Factories)
            {
                RegisterFactory(services, factoryItem, options);
            }
        }

        private static void RegisterItem(IServiceCollection services, ServiceRegistrationItem item, EasyDiOptions options)
        {
            var isKeyed = item.ServiceKey != null;
            ServiceDescriptor? existing = isKeyed
                ? services.FirstOrDefault(s => s.IsKeyedService && s.ServiceType == item.ServiceType && Equals(s.ServiceKey, item.ServiceKey))
                : services.FirstOrDefault(s => !s.IsKeyedService && s.ServiceType == item.ServiceType);

            if (existing != null)
            {
                if (item.ExplicitReplace || options.DuplicateBehavior == DuplicateRegistrationBehavior.Replace)
                {
                    services.Remove(existing);
                }
                else if (options.DuplicateBehavior == DuplicateRegistrationBehavior.Throw)
                {
                    var existingImpl = existing.ImplementationType?.FullName ?? existing.ServiceType.FullName;
                    throw new InvalidOperationException(
                        $"Service '{item.ServiceType.FullName}' is already registered with implementation '{existingImpl}'.");
                }
                else if (options.DuplicateBehavior == DuplicateRegistrationBehavior.Skip)
                {
                    return;
                }
                // Append falls through
            }

            ServiceDescriptor descriptor;

            if (isKeyed)
            {
                descriptor = new ServiceDescriptor(item.ServiceType, item.ServiceKey, item.ImplementationType, item.Lifetime);
            }
            else if (item.Kind == RegistrationKind.InterfaceForwarding)
            {
                // Single-instance forwarding factory
                descriptor = ServiceDescriptor.Describe(
                    item.ServiceType,
                    sp => sp.GetRequiredService(item.ImplementationType),
                    item.Lifetime
                );
            }
            else
            {
                descriptor = new ServiceDescriptor(item.ServiceType, item.ImplementationType, item.Lifetime);
            }

            services.Add(descriptor);

            var info = new ServiceRegistrationInfo(
                item.ServiceType,
                item.ImplementationType,
                item.Lifetime,
                item.Source,
                DateTime.UtcNow,
                item.ServiceKey
            );

            EasyDiRegistry.Track(services, info);

            if (options.EnableLogging)
            {
                var keyPart = item.ServiceKey != null ? $" [Key={item.ServiceKey}]" : "";
                Console.WriteLine($"[EasyDI] Registered {item.ServiceType.Name}{keyPart} -> {item.ImplementationType.Name} ({item.Lifetime}) [{item.Source}]");
            }

            options.OnServiceRegistered?.Invoke(info);
        }

        private static void ApplyDecorator(IServiceCollection services, ServiceRegistrationItem decoratorItem, EasyDiOptions options)
        {
            var targetType = decoratorItem.TargetDecoratedType ?? decoratorItem.ServiceType;
            var decoratorType = decoratorItem.ImplementationType;

            var descriptor = services.FirstOrDefault(s => s.ServiceType == targetType);
            if (descriptor == null)
            {
                if (options.EnableLogging)
                {
                    Console.WriteLine($"[EasyDI] Warning: Cannot decorate {targetType.Name} - no existing registration found.");
                }
                return;
            }

            services.Remove(descriptor);

            ServiceDescriptor newDescriptor;
            var lifetime = descriptor.Lifetime;

            if (descriptor.ImplementationInstance != null)
            {
                newDescriptor = ServiceDescriptor.Describe(
                    targetType,
                    provider =>
                    {
                        var instance = descriptor.ImplementationInstance;
                        return ActivatorUtilities.CreateInstance(provider, decoratorType, instance);
                    },
                    ServiceLifetime.Singleton
                );
            }
            else if (descriptor.ImplementationFactory != null)
            {
                newDescriptor = ServiceDescriptor.Describe(
                    targetType,
                    provider =>
                    {
                        var instance = descriptor.ImplementationFactory(provider)!;
                        return ActivatorUtilities.CreateInstance(provider, decoratorType, instance);
                    },
                    lifetime
                );
            }
            else
            {
                newDescriptor = ServiceDescriptor.Describe(
                    targetType,
                    provider =>
                    {
                        var instance = ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
                        return ActivatorUtilities.CreateInstance(provider, decoratorType, instance);
                    },
                    lifetime
                );
            }

            services.Add(newDescriptor);

            var info = new ServiceRegistrationInfo(
                targetType,
                decoratorType,
                lifetime,
                "Decorator",
                DateTime.UtcNow
            );

            EasyDiRegistry.Track(services, info);

            if (options.EnableLogging)
            {
                Console.WriteLine($"[EasyDI] Decorated {targetType.Name} with {decoratorType.Name} ({lifetime})");
            }

            options.OnServiceRegistered?.Invoke(info);
        }

        private static void RegisterFactory(IServiceCollection services, ServiceRegistrationItem factoryItem, EasyDiOptions options)
        {
            var method = factoryItem.FactoryMethod!;
            var returnType = factoryItem.ServiceType;
            var lifetime = factoryItem.Lifetime;

            var descriptor = ServiceDescriptor.Describe(
                returnType,
                provider =>
                {
                    var parameters = method.GetParameters()
                        .Select(p =>
                        {
                            if (p.HasDefaultValue && provider.GetService(p.ParameterType) == null)
                            {
                                return p.DefaultValue;
                            }
                            return provider.GetRequiredService(p.ParameterType);
                        })
                        .ToArray();

                    return method.Invoke(null, parameters)!;
                },
                lifetime
            );

            services.Add(descriptor);

            var info = new ServiceRegistrationInfo(
                returnType,
                factoryItem.ImplementationType,
                lifetime,
                "ServiceFactory",
                DateTime.UtcNow
            );

            EasyDiRegistry.Track(services, info);

            if (options.EnableLogging)
            {
                Console.WriteLine($"[EasyDI] Registered factory for {returnType.Name} from {factoryItem.ImplementationType.Name}.{method.Name}");
            }

            options.OnServiceRegistered?.Invoke(info);
        }
    }
}

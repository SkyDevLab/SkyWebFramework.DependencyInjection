using Microsoft.Extensions.DependencyInjection;
using SkyWebFramework.DependencyInjection.Scanning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SkyWebFramework.DependencyInjection.Engine
{
    /// <summary>
    /// Central engine responsible for building execution plans from types according to conventions and attributes.
    /// </summary>
    public static class ServiceRegistrationPlanner
    {
        public static ServiceRegistrationPlan BuildPlan(IEnumerable<Assembly> assemblies, EasyDiOptions options)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var plan = new ServiceRegistrationPlan();
            var allTypes = new List<Type>();

            foreach (var assembly in assemblies)
            {
                if (IsExcludedAssembly(assembly, options))
                    continue;

                var types = assembly.GetLoadableTypes(options.OnScanError);
                allTypes.AddRange(types);
            }

            var candidateTypes = FilterCandidateTypes(allTypes, options);

            // 1. Process standard services
            foreach (var implType in candidateTypes)
            {
                if (implType.GetCustomAttribute<IgnoreServiceAttribute>() != null)
                    continue;

                if (implType.GetCustomAttribute<DecoratorAttribute>() != null)
                {
                    if (options.EnableDecorators)
                    {
                        var decAttr = implType.GetCustomAttribute<DecoratorAttribute>()!;
                        plan.Decorators.Add(new ServiceRegistrationItem(
                            ServiceType: decAttr.DecoratedType,
                            ImplementationType: implType,
                            Lifetime: GetLifetime(implType, options),
                            Source: "DecoratorAttribute",
                            Kind: RegistrationKind.Decorator,
                            TargetDecoratedType: decAttr.DecoratedType
                        ));
                    }
                    continue;
                }

                if (options.TypeFilter != null && !options.TypeFilter(implType))
                    continue;

                PlanTypeRegistration(implType, options, plan);
            }

            // 2. Process factory methods
            foreach (var type in allTypes.Where(t => t.IsClass))
            {
                var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.GetCustomAttribute<ServiceFactoryAttribute>() != null);

                foreach (var method in staticMethods)
                {
                    var factoryAttr = method.GetCustomAttribute<ServiceFactoryAttribute>()!;
                    var returnType = method.ReturnType;

                    if (returnType == typeof(void))
                    {
                        throw new InvalidOperationException($"Service factory method '{type.FullName}.{method.Name}' must have a non-void return type.");
                    }

                    if (method.IsGenericMethodDefinition)
                    {
                        throw new InvalidOperationException($"Open generic service factory method '{type.FullName}.{method.Name}' is not supported.");
                    }

                    plan.Factories.Add(new ServiceRegistrationItem(
                        ServiceType: returnType,
                        ImplementationType: type,
                        Lifetime: factoryAttr.Lifetime,
                        Source: "ServiceFactory",
                        Kind: RegistrationKind.FactoryMethod,
                        FactoryMethod: method
                    ));
                }
            }

            return plan;
        }

        private static List<Type> FilterCandidateTypes(IEnumerable<Type> types, EasyDiOptions options)
        {
            var result = types.Where(t => t.IsServiceCandidate());

            if (!options.RegisterGenericTypes)
            {
                result = result.Where(t => !t.IsGenericTypeDefinition);
            }

            if (!options.IncludeInternalTypes)
            {
                result = result.Where(t => t.IsPublic || t.IsNestedPublic);
            }

            if (options.IncludeNamespaces?.Any() == true)
            {
                result = result.Where(t =>
                    options.IncludeNamespaces.Any(ns =>
                        t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true));
            }

            if (options.ExcludeNamespaces?.Any() == true)
            {
                result = result.Where(t =>
                    !options.ExcludeNamespaces.Any(ns =>
                        t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true));
            }

            return result.ToList();
        }

        private static void PlanTypeRegistration(Type implementationType, EasyDiOptions options, ServiceRegistrationPlan plan)
        {
            var easyServiceAttr = implementationType.GetCustomAttribute<EasyServiceAttribute>();
            var registerAllInterfaces = implementationType.GetCustomAttribute<RegisterAllInterfacesAttribute>() != null;
            var registerAsAttributes = implementationType.GetCustomAttributes<RegisterAsAttribute>().ToList();
            var keyedAttributes = implementationType.GetCustomAttributes<KeyedServiceAttribute>().ToList();

            // Handle Keyed Services
            if (keyedAttributes.Any())
            {
                var keyedInterfaces = implementationType.GetInterfaces()
                    .Where(i => !i.IsGenericTypeDefinition && !i.IsIgnoredInterface())
                    .ToList();
                var keyedMatchingInterface = keyedInterfaces.FirstOrDefault(i =>
                    i.Name == "I" + implementationType.Name)
                    ?? keyedInterfaces.FirstOrDefault(i => options.ServiceSuffixes.Any(suffix => i.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                    ?? (keyedInterfaces.Count == 1 ? keyedInterfaces.First() : null);

                foreach (var attr in keyedAttributes)
                {
                    var serviceType = attr.ServiceType ?? keyedMatchingInterface ?? implementationType;
                    ValidateAssignability(serviceType, implementationType, "KeyedService");
                    plan.Items.Add(new ServiceRegistrationItem(
                        ServiceType: serviceType,
                        ImplementationType: implementationType,
                        Lifetime: attr.Lifetime,
                        Source: "KeyedService",
                        Kind: RegistrationKind.Keyed,
                        ExplicitReplace: easyServiceAttr?.Replace ?? false,
                        ServiceKey: attr.ServiceKey
                    ));
                }
            }

            // Handle explicit [RegisterAs]
            if (registerAsAttributes.Any())
            {
                foreach (var attr in registerAsAttributes)
                {
                    var serviceType = attr.ServiceType;
                    ValidateAssignability(serviceType, implementationType, "RegisterAs");

                    var lifetime = attr.Lifetime ?? GetLifetime(implementationType, options);

                    if (implementationType.IsGenericTypeDefinition && serviceType.IsGenericTypeDefinition)
                    {
                        plan.Items.Add(new ServiceRegistrationItem(
                            ServiceType: serviceType,
                            ImplementationType: implementationType,
                            Lifetime: lifetime,
                            Source: "RegisterAs",
                            Kind: RegistrationKind.OpenGeneric,
                            ExplicitReplace: easyServiceAttr?.Replace ?? false
                        ));
                    }
                    else
                    {
                        plan.Items.Add(new ServiceRegistrationItem(
                            ServiceType: serviceType,
                            ImplementationType: implementationType,
                            Lifetime: lifetime,
                            Source: "RegisterAs",
                            Kind: RegistrationKind.Direct,
                            ExplicitReplace: easyServiceAttr?.Replace ?? false
                        ));
                    }
                }
                return;
            }

            // Handle Open Generic Types
            if (implementationType.IsGenericTypeDefinition)
            {
                PlanOpenGenericRegistration(implementationType, options, plan, easyServiceAttr, registerAllInterfaces);
                return;
            }

            var serviceLifetime = GetLifetime(implementationType, options);
            var interfaces = implementationType.GetInterfaces()
                .Where(i => !i.IsGenericTypeDefinition && !i.IsIgnoredInterface())
                .ToList();

            // Handle [RegisterAllInterfaces] or multiple interfaces with single instance sharing
            if (registerAllInterfaces && interfaces.Any())
            {
                if (serviceLifetime == ServiceLifetime.Transient)
                {
                    // Transient: Direct registration per interface
                    foreach (var iface in interfaces)
                    {
                        plan.Items.Add(new ServiceRegistrationItem(
                            ServiceType: iface,
                            ImplementationType: implementationType,
                            Lifetime: ServiceLifetime.Transient,
                            Source: "AllInterfaces",
                            Kind: RegistrationKind.Direct,
                            ExplicitReplace: easyServiceAttr?.Replace ?? false
                        ));
                    }
                }
                else
                {
                    // Scoped or Singleton: Register concrete type + interface forwarding for single-instance sharing!
                    plan.Items.Add(new ServiceRegistrationItem(
                        ServiceType: implementationType,
                        ImplementationType: implementationType,
                        Lifetime: serviceLifetime,
                        Source: "AllInterfaces",
                        Kind: RegistrationKind.ConcreteOnly,
                        ExplicitReplace: easyServiceAttr?.Replace ?? false
                    ));

                    foreach (var iface in interfaces)
                    {
                        plan.Items.Add(new ServiceRegistrationItem(
                            ServiceType: iface,
                            ImplementationType: implementationType,
                            Lifetime: serviceLifetime,
                            Source: "AllInterfaces",
                            Kind: RegistrationKind.InterfaceForwarding,
                            ExplicitReplace: easyServiceAttr?.Replace ?? false
                        ));
                    }
                }

                if (options.RegisterBothInterfaceAndConcrete && serviceLifetime == ServiceLifetime.Transient)
                {
                    plan.Items.Add(new ServiceRegistrationItem(
                        ServiceType: implementationType,
                        ImplementationType: implementationType,
                        Lifetime: serviceLifetime,
                        Source: "Concrete",
                        Kind: RegistrationKind.ConcreteOnly,
                        ExplicitReplace: easyServiceAttr?.Replace ?? false
                    ));
                }
                return;
            }

            // Convention Strategy 1: IServiceName pattern
            var matchingInterface = interfaces.FirstOrDefault(i =>
                i.Name == "I" + implementationType.Name);

            // Convention Strategy 2: Interface ending with configured suffix
            if (matchingInterface == null)
            {
                matchingInterface = interfaces.FirstOrDefault(i =>
                    options.ServiceSuffixes.Any(suffix =>
                        i.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
            }

            // Convention Strategy 3: Single implemented interface
            if (matchingInterface == null && interfaces.Count == 1)
            {
                matchingInterface = interfaces.First();
            }

            if (matchingInterface != null)
            {
                plan.Items.Add(new ServiceRegistrationItem(
                    ServiceType: matchingInterface,
                    ImplementationType: implementationType,
                    Lifetime: serviceLifetime,
                    Source: "Interface",
                    Kind: RegistrationKind.Direct,
                    ExplicitReplace: easyServiceAttr?.Replace ?? false
                ));

                if (options.RegisterBothInterfaceAndConcrete)
                {
                    plan.Items.Add(new ServiceRegistrationItem(
                        ServiceType: implementationType,
                        ImplementationType: implementationType,
                        Lifetime: serviceLifetime,
                        Source: "Concrete",
                        Kind: RegistrationKind.ConcreteOnly,
                        ExplicitReplace: easyServiceAttr?.Replace ?? false
                    ));
                }
            }
            else if (easyServiceAttr != null ||
                    (options.RegisterConcreteTypes && options.ServiceSuffixes.Any(suffix =>
                        implementationType.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))))
            {
                plan.Items.Add(new ServiceRegistrationItem(
                    ServiceType: implementationType,
                    ImplementationType: implementationType,
                    Lifetime: serviceLifetime,
                    Source: "Concrete",
                    Kind: RegistrationKind.ConcreteOnly,
                    ExplicitReplace: easyServiceAttr?.Replace ?? false
                ));
            }
        }

        private static void PlanOpenGenericRegistration(
            Type implementationType,
            EasyDiOptions options,
            ServiceRegistrationPlan plan,
            EasyServiceAttribute? easyServiceAttr,
            bool registerAllInterfaces)
        {
            var serviceLifetime = GetLifetime(implementationType, options);
            var implementedInterfaces = implementationType.GetInterfaces()
                .Where(i => i.IsGenericType && !i.IsIgnoredInterface())
                .Select(i => i.GetGenericTypeDefinition())
                .Distinct()
                .ToList();

            if (registerAllInterfaces && implementedInterfaces.Any())
            {
                foreach (var ifaceDef in implementedInterfaces)
                {
                    if (ReflectionExtensions.IsGenericTypeDefinitionAssignable(ifaceDef, implementationType))
                    {
                        plan.Items.Add(new ServiceRegistrationItem(
                            ServiceType: ifaceDef,
                            ImplementationType: implementationType,
                            Lifetime: serviceLifetime,
                            Source: "AllInterfaces",
                            Kind: RegistrationKind.OpenGeneric,
                            ExplicitReplace: easyServiceAttr?.Replace ?? false
                        ));
                    }
                }
                return;
            }

            // Strategy 1: Matching open generic interface name IRepository<> for Repository<>
            var cleanImplName = implementationType.Name;
            var tickIndex = cleanImplName.IndexOf('`');
            if (tickIndex > 0) cleanImplName = cleanImplName.Substring(0, tickIndex);

            var matchingIface = implementedInterfaces.FirstOrDefault(i =>
            {
                var cleanIfaceName = i.Name;
                var ifaceTick = cleanIfaceName.IndexOf('`');
                if (ifaceTick > 0) cleanIfaceName = cleanIfaceName.Substring(0, ifaceTick);
                return cleanIfaceName == "I" + cleanImplName;
            });

            // Strategy 2: Single open generic interface
            if (matchingIface == null && implementedInterfaces.Count == 1)
            {
                matchingIface = implementedInterfaces.First();
            }

            if (matchingIface != null && ReflectionExtensions.IsGenericTypeDefinitionAssignable(matchingIface, implementationType))
            {
                plan.Items.Add(new ServiceRegistrationItem(
                    ServiceType: matchingIface,
                    ImplementationType: implementationType,
                    Lifetime: serviceLifetime,
                    Source: "Interface",
                    Kind: RegistrationKind.OpenGeneric,
                    ExplicitReplace: easyServiceAttr?.Replace ?? false
                ));
            }
            else if (easyServiceAttr != null ||
                    (options.RegisterConcreteTypes && options.ServiceSuffixes.Any(suffix =>
                        cleanImplName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))))
            {
                plan.Items.Add(new ServiceRegistrationItem(
                    ServiceType: implementationType,
                    ImplementationType: implementationType,
                    Lifetime: serviceLifetime,
                    Source: "Concrete",
                    Kind: RegistrationKind.OpenGeneric,
                    ExplicitReplace: easyServiceAttr?.Replace ?? false
                ));
            }
        }

        private static void ValidateAssignability(Type serviceType, Type implementationType, string attributeName)
        {
            if (serviceType.IsGenericTypeDefinition && implementationType.IsGenericTypeDefinition)
            {
                if (!ReflectionExtensions.IsGenericTypeDefinitionAssignable(serviceType, implementationType))
                {
                    throw new InvalidOperationException($"Type '{implementationType.FullName}' does not implement open generic '{serviceType.FullName}' specified in [{attributeName}].");
                }
            }
            else if (!serviceType.IsAssignableFrom(implementationType))
            {
                throw new InvalidOperationException($"Type '{implementationType.FullName}' does not implement or inherit from '{serviceType.FullName}' specified in [{attributeName}].");
            }
        }

        public static ServiceLifetime GetLifetime(Type implementationType, EasyDiOptions options)
        {
            var attr = implementationType.GetCustomAttribute<EasyServiceAttribute>();
            return attr?.Lifetime ?? options.DefaultLifetime;
        }

        public static bool IsExcludedAssembly(Assembly assembly, EasyDiOptions options)
        {
            var assemblyName = assembly.GetName().Name ?? "";
            return options.ExcludedAssemblies.Any(pattern =>
            {
                if (pattern.EndsWith("*"))
                {
                    var prefix = pattern.TrimEnd('*');
                    return assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                }
                return assemblyName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}

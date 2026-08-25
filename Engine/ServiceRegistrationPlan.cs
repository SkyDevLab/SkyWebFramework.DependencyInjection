using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SkyWebFramework.DependencyInjection.Engine
{
    public enum RegistrationKind
    {
        Direct,
        ConcreteOnly,
        InterfaceForwarding,
        OpenGeneric,
        Decorator,
        FactoryMethod,
        Keyed
    }

    public enum PlannedAction
    {
        Register,
        Replace,
        Skip
    }

    /// <summary>
    /// Represents a single planned service registration item.
    /// </summary>
    public record ServiceRegistrationItem(
        Type ServiceType,
        Type ImplementationType,
        ServiceLifetime Lifetime,
        string Source,
        RegistrationKind Kind,
        bool ExplicitReplace = false,
        object? ServiceKey = null,
        MethodInfo? FactoryMethod = null,
        Type? TargetDecoratedType = null
    );

    /// <summary>
    /// Summary of planned registrations for preview and diagnostic analysis.
    /// </summary>
    public class ServiceRegistrationPlan
    {
        public List<ServiceRegistrationItem> Items { get; } = new();
        public List<ServiceRegistrationItem> Decorators { get; } = new();
        public List<ServiceRegistrationItem> Factories { get; } = new();
    }

    /// <summary>
    /// Information for dry-run/preview mode.
    /// </summary>
    public record ServicePreviewInfo(
        Type ServiceType,
        Type ImplementationType,
        ServiceLifetime Lifetime,
        string Source,
        PlannedAction Action,
        object? ServiceKey = null
    )
    {
        public override string ToString()
        {
            var keyStr = ServiceKey != null ? $" (Key: {ServiceKey})" : "";
            return $"{ServiceType.Name}{keyStr} -> {ImplementationType.Name} ({Lifetime}) [{Source}] Action: {Action}";
        }
    }
}

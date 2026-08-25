using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Configuration options for automatic service scanning and registration.
    /// </summary>
    public class EasyDiOptions
    {
        /// <summary>
        /// Default lifetime for services without an explicit lifetime attribute. Default is <see cref="ServiceLifetime.Scoped"/>.
        /// </summary>
        public ServiceLifetime DefaultLifetime { get; set; } = ServiceLifetime.Scoped;

        /// <summary>
        /// Class name suffixes to scan for convention-based registration (e.g. "Service", "Repository").
        /// </summary>
        public List<string> ServiceSuffixes { get; set; } = new() { "Service", "Repository", "Manager" };

        /// <summary>
        /// If true, registers classes that match suffixes as concrete types if no interface is found. Default is true.
        /// </summary>
        public bool RegisterConcreteTypes { get; set; } = true;

        /// <summary>
        /// If true, registers both interface and concrete implementation. Default is false.
        /// </summary>
        public bool RegisterBothInterfaceAndConcrete { get; set; } = false;

        /// <summary>
        /// Custom filter to include/exclude types during scanning.
        /// </summary>
        public Func<Type, bool>? TypeFilter { get; set; }

        /// <summary>
        /// If true, throws an exception when duplicate registrations are found.
        /// Backwards-compatible shortcut that maps to <see cref="DuplicateBehavior"/>.
        /// </summary>
        public bool ThrowOnDuplicates
        {
            get => DuplicateBehavior == DuplicateRegistrationBehavior.Throw;
            set => DuplicateBehavior = value ? DuplicateRegistrationBehavior.Throw : DuplicateRegistrationBehavior.Skip;
        }

        /// <summary>
        /// Defines how duplicate registrations are handled. Default is <see cref="DuplicateRegistrationBehavior.Skip"/>.
        /// </summary>
        public DuplicateRegistrationBehavior DuplicateBehavior { get; set; } = DuplicateRegistrationBehavior.Skip;

        /// <summary>
        /// If true, includes non-public internal types in scanning. Default is false.
        /// </summary>
        public bool IncludeInternalTypes { get; set; } = false;

        /// <summary>
        /// Assemblies to exclude from scanning by name pattern.
        /// </summary>
        public List<string> ExcludedAssemblies { get; set; } = new()
        {
            "Microsoft.*",
            "System.*",
            "netstandard",
            "mscorlib"
        };

        /// <summary>
        /// Namespaces to include (if specified and non-empty, only matching namespaces are scanned).
        /// </summary>
        public List<string>? IncludeNamespaces { get; set; }

        /// <summary>
        /// Namespaces to exclude from scanning.
        /// </summary>
        public List<string> ExcludeNamespaces { get; set; } = new();

        /// <summary>
        /// If true, enables automatic decorator registration. Default is true.
        /// </summary>
        public bool EnableDecorators { get; set; } = true;

        /// <summary>
        /// If true, logs registration details to console. Default is false.
        /// </summary>
        public bool EnableLogging { get; set; } = false;

        /// <summary>
        /// If true, automatically validates services after registration. Default is false.
        /// </summary>
        public bool AutoValidate { get; set; } = false;

        /// <summary>
        /// If true, registers open generic type definitions. Default is true.
        /// </summary>
        public bool RegisterGenericTypes { get; set; } = true;

        /// <summary>
        /// Optional logger/callback for loader errors during safe type scanning.
        /// </summary>
        public Action<Exception>? OnScanError { get; set; }

        /// <summary>
        /// Custom action to execute after each service registration.
        /// </summary>
        public Action<ServiceRegistrationInfo>? OnServiceRegistered { get; set; }
    }
}

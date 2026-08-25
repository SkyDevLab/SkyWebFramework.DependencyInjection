using Microsoft.Extensions.DependencyInjection;
using SkyWebFramework.DependencyInjection.Scanning;
using System;
using System.Linq;
using System.Reflection;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Represents a modular service registration unit.
    /// </summary>
    public interface IEasyServiceModule
    {
        string Name => GetType().Name;
        void RegisterServices(IServiceCollection services);
    }

    /// <summary>
    /// Defines a registration profile.
    /// </summary>
    public interface IEasyServiceProfile
    {
        string Name => GetType().Name;
        void Configure(IServiceCollection services, EasyDiOptions options);
    }

    public static class ModuleExtensions
    {
        /// <summary>
        /// Registers services from a module instance.
        /// </summary>
        public static IServiceCollection AddEasyServiceModule<TModule>(
            this IServiceCollection services)
            where TModule : IEasyServiceModule, new()
        {
            var module = new TModule();
            module.RegisterServices(services);
            return services;
        }

        /// <summary>
        /// Scans and registers all modules from an assembly according to optional priority.
        /// </summary>
        public static IServiceCollection AddEasyServiceModules(
            this IServiceCollection services,
            Assembly assembly)
        {
            var moduleTypes = assembly.GetLoadableTypes()
                .Where(t => typeof(IEasyServiceModule).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    Type = t,
                    Attribute = t.GetCustomAttribute<ServiceModuleAttribute>(),
                    Priority = t.GetCustomAttribute<ServiceModuleAttribute>()?.Priority ?? 0
                })
                .OrderBy(m => m.Priority)
                .ToList();

            foreach (var moduleInfo in moduleTypes)
            {
                var module = (IEasyServiceModule)Activator.CreateInstance(moduleInfo.Type)!;
                module.RegisterServices(services);
            }

            return services;
        }
    }

    public static class ProfileExtensions
    {
        /// <summary>
        /// Registers services using a profile.
        /// </summary>
        public static IServiceCollection AddEasyServicesFromProfile<TProfile>(
            this IServiceCollection services,
            Action<EasyDiOptions>? configure = null)
            where TProfile : IEasyServiceProfile, new()
        {
            var options = new EasyDiOptions();
            configure?.Invoke(options);

            var profile = new TProfile();
            profile.Configure(services, options);

            return services;
        }

        /// <summary>
        /// Scans and registers all profiles from an assembly.
        /// </summary>
        public static IServiceCollection AddEasyServicesFromProfiles(
            this IServiceCollection services,
            Assembly assembly,
            Action<EasyDiOptions>? configure = null)
        {
            var options = new EasyDiOptions();
            configure?.Invoke(options);

            var profileTypes = assembly.GetLoadableTypes()
                .Where(t => typeof(IEasyServiceProfile).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .ToList();

            foreach (var profileType in profileTypes)
            {
                var profile = (IEasyServiceProfile)Activator.CreateInstance(profileType)!;
                profile.Configure(services, options);
            }

            return services;
        }
    }
}

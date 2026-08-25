using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Thread-safe registry that tracks EasyDI service registrations without leaking IServiceCollection references.
    /// </summary>
    public static class EasyDiRegistry
    {
        private class RegistryState
        {
            private readonly object _lock = new();
            private readonly List<ServiceRegistrationInfo> _items = new();

            public void Add(ServiceRegistrationInfo info)
            {
                lock (_lock)
                {
                    _items.Add(info);
                }
            }

            public List<ServiceRegistrationInfo> GetSnapshot()
            {
                lock (_lock)
                {
                    return new List<ServiceRegistrationInfo>(_items);
                }
            }

            public void Clear()
            {
                lock (_lock)
                {
                    _items.Clear();
                }
            }
        }

        private static readonly ConditionalWeakTable<IServiceCollection, RegistryState> _table = new();

        /// <summary>
        /// Records a service registration for diagnostic tracking.
        /// </summary>
        public static void Track(IServiceCollection services, ServiceRegistrationInfo info)
        {
            if (services == null || info == null) return;
            var state = _table.GetOrCreateValue(services);
            state.Add(info);
        }

        /// <summary>
        /// Gets a thread-safe snapshot list of all tracked registrations for the given service collection.
        /// </summary>
        public static List<ServiceRegistrationInfo> GetRegistrations(IServiceCollection services)
        {
            if (services == null) return new List<ServiceRegistrationInfo>();
            if (_table.TryGetValue(services, out var state))
            {
                return state.GetSnapshot();
            }
            return new List<ServiceRegistrationInfo>();
        }

        /// <summary>
        /// Clears all tracked registrations for the specified service collection.
        /// </summary>
        public static void Clear(IServiceCollection services)
        {
            if (services == null) return;
            if (_table.TryGetValue(services, out var state))
            {
                state.Clear();
            }
        }
    }
}

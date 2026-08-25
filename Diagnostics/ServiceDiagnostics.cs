using System;
using System.Collections.Generic;

namespace SkyWebFramework.DependencyInjection
{
    /// <summary>
    /// Detailed diagnostic report of EasyDI registered services.
    /// </summary>
    public class ServiceDiagnostics
    {
        public int TotalServices { get; init; }
        public int SingletonCount { get; init; }
        public int ScopedCount { get; init; }
        public int TransientCount { get; init; }
        public Dictionary<string, int> RegistrationsBySource { get; init; } = new();
        public DateTime? OldestRegistration { get; init; }
        public DateTime? NewestRegistration { get; init; }
        public IReadOnlyList<ServiceRegistrationInfo> Registrations { get; init; } = Array.Empty<ServiceRegistrationInfo>();
    }
}

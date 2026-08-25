# SkyWebFramework.DependencyInjection

[![NuGet Version](https://img.shields.io/nuget/v/SkyWebFramework.DependencyInjection.svg)](https://www.nuget.org/packages/SkyWebFramework.DependencyInjection)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-purple.svg)](https://dotnet.microsoft.com/)

**SkyWebFramework.DependencyInjection** (EasyDI) is an enterprise-grade, high-performance convention and attribute-driven Dependency Injection framework for .NET 8 and .NET 9.

It eliminates repetitive `builder.Services.AddScoped<...>()` boilerplate code by automatically scanning assemblies, resolving conventions, supporting modular composition, applying decorators/interceptors, and detecting captive/circular dependencies at startup.

---

## 🌟 Key Features

- **⚡ Zero-Boilerplate Assembly Scanning**: Automatically discovers interfaces and implementations by naming conventions.
- **🏷️ Attribute-Driven Lifetime Management**: Decorate classes with `[Scoped]`, `[Transient]`, `[Singleton]`, or `[EasyService]`.
- **🔑 Keyed Services (.NET 8/9)**: First-class support for `[KeyedService("key")]` with automatic interface inference.
- **🧩 Modular DI Architecture**: Organize registrations cleanly into decoupled modules (`IEasyServiceModule`) and profiles (`IEasyServiceProfile`) with priority ordering.
- **🛡️ Diagnostic & Safety Engine**: Built-in detection for **Captive Dependencies** (e.g. Scoped service inside Singleton) and **Circular References**.
- **🎨 Decorator & Interceptor Patterns**: Wrap services programmatically (`services.Decorate<T, TDecorator>()`) or via attributes (`[Decorator(typeof(ITarget))]`).
- **⏳ Lazy Resolution (`Lazy<T>`)**: Defer instantiation of heavy services until `.Value` is accessed.
- **⚙️ Flexible Configuration (`EasyDiOptions`)**: Custom type filters, namespace whitelist/blacklist, duplicate registration behaviors (`Skip`, `Replace`, `Throw`).

---

## 📦 Installation

Install via NuGet Package Manager:

```bash
dotnet add package SkyWebFramework.DependencyInjection
```

Or via Package Manager Console:

```powershell
Install-Package SkyWebFramework.DependencyInjection
```

---

## 🚀 Quick Start

In your ASP.NET Core `Program.cs`:

```csharp
using SkyWebFramework.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. One-line auto-registration for your entire assembly!
builder.Services.AddEasyServices();

// 2. (Optional) Auto-discover and run all IEasyServiceModule & IEasyServiceProfile instances
builder.Services.AddEasyServiceModules(Assembly.GetExecutingAssembly());

// 3. (Optional) Run container safety diagnostics at startup
builder.Services.DetectCaptiveDependencies(throwOnError: true);
builder.Services.ValidateEasyServices();

var app = builder.Build();
app.MapControllers();
app.Run();
```

---

## 💡 Usage Examples

### 1. Attribute-Driven Registration

Simply decorate your classes to define their lifetime:

```csharp
using SkyWebFramework.DependencyInjection;

public interface IOrderService { void CreateOrder(); }

[Scoped]
public class OrderService : IOrderService
{
    public void CreateOrder() { /* Implementation */ }
}

[Singleton]
public class CacheService : ICacheService { }

[Transient]
public class PaymentGateway : IPaymentGateway { }
```

### 2. Convention-Based Scanning

Classes ending with `Service`, `Repository`, or `Manager` are automatically matched to `I<ClassName>` without requiring attributes!

```csharp
// Automatically registered as Scoped under ICustomerRepository
public class CustomerRepository : ICustomerRepository { }
```

### 3. Keyed Services (.NET 8/9)

```csharp
[KeyedService("email")]
public class EmailNotificationService : INotificationService { }

[KeyedService("sms")]
public class SmsNotificationService : INotificationService { }

// Consumer injection
public class NotificationController(
    [FromKeyedServices("email")] INotificationService emailService,
    [FromKeyedServices("sms")] INotificationService smsService)
{
    // ...
}
```

### 4. Modular Architecture (`IEasyServiceModule`)

Cleanly decouple registration logic across distinct feature modules:

```csharp
[ServiceModule("BillingModule", Priority = 1)]
public class BillingModule : IEasyServiceModule
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IBillingService, BillingService>();
    }
}

// Auto-register all modules in priority order
services.AddEasyServiceModules(Assembly.GetExecutingAssembly());
```

### 5. Decorators & Interceptors

```csharp
// Programmatically decorate existing service registration
services.Decorate<IOrderService, LoggingOrderDecorator>();

// Attribute-driven decorator
[Decorator(typeof(ICalculatorService))]
public class AuditCalculatorDecorator : ICalculatorService
{
    private readonly ICalculatorService _inner;
    public AuditCalculatorDecorator(ICalculatorService inner) => _inner = inner;
    
    public int Add(int a, int b) => _inner.Add(a, b);
}

// Interceptor callback
services.InterceptService<IOrderService>((instance, provider) => 
{
    Console.WriteLine("Invoking OrderService");
    return instance;
});
```

### 6. Lazy Resolution (`Lazy<T>`)

```csharp
// Enable Lazy<T> for all registered services
services.EnableLazyResolution();

// Service is NOT instantiated until .Value is accessed
public class ReportController(Lazy<IHeavyReportGenerator> lazyReportGenerator)
{
    public void Generate() => lazyReportGenerator.Value.Build();
}
```

### 7. Diagnostics & Container Summary

```csharp
// Print a registration report to the console at startup
services.PrintEasyServiceSummary();

// Export full container state as formatted JSON
string jsonReport = services.ExportRegistrationsAsJson();
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE.txt).

Copyright (c) 2026 Surya Pratap Singh - **SkyWebFramework**

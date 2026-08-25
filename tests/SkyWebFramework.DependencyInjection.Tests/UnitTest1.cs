using Microsoft.Extensions.DependencyInjection;
using SkyWebFramework.DependencyInjection;
using SkyWebFramework.DependencyInjection.Scanning;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SkyWebFramework.DependencyInjection.Tests
{
    // ================= Test Sample Types =================

    public interface IUserService { string GetName(); }
    public interface IUserCache { string GetCached(); }

    public class UserService : IUserService
    {
        public string GetName() => "User1";
    }

    [Singleton]
    public class SingletonOrderService { }

    [Transient]
    public class TransientPaymentService { }

    [Scoped]
    [RegisterAllInterfaces]
    public class MultiScopedService : IUserService, IUserCache
    {
        public string GetName() => "MultiUser";
        public string GetCached() => "CachedValue";
    }

    [Singleton]
    [RegisterAllInterfaces]
    public class MultiSingletonService : IUserService, IUserCache
    {
        public string GetName() => "SingletonMulti";
        public string GetCached() => "SingletonCache";
    }

    [Transient]
    [RegisterAllInterfaces]
    public class MultiTransientService : IUserService, IUserCache
    {
        public string GetName() => "TransientMulti";
        public string GetCached() => "TransientCache";
    }

    public interface IRepository<T> { T Get(int id); }

    [Scoped]
    public class Repository<T> : IRepository<T>
    {
        public T Get(int id) => default!;
    }

    public interface ICustomService { }

    [RegisterAs(typeof(ICustomService))]
    public class ExplicitService : ICustomService { }

    [IgnoreService]
    public class IgnoredService { }

    public interface IDecoratedService { string Process(); }

    public class BaseDecoratedService : IDecoratedService
    {
        public string Process() => "Base";
    }

    public class LoggingDecorator : IDecoratedService
    {
        private readonly IDecoratedService _inner;
        public LoggingDecorator(IDecoratedService inner) => _inner = inner;
        public string Process() => $"Logged({_inner.Process()})";
    }

    public class AuditDecorator : IDecoratedService
    {
        private readonly IDecoratedService _inner;
        public AuditDecorator(IDecoratedService inner) => _inner = inner;
        public string Process() => $"Audited({_inner.Process()})";
    }

    public class FactoryService
    {
        public string Name { get; }
        public FactoryService(string name) => Name = name;

        [ServiceFactory(ServiceLifetime.Singleton)]
        public static FactoryService Create() => new FactoryService("FromFactory");
    }

    public class SingletonHoldingScoped
    {
        public IUserService User { get; }
        public SingletonHoldingScoped(IUserService user) => User = user;
    }

    public interface ICycleA { }
    public interface ICycleB { }

    public class CycleA : ICycleA
    {
        public CycleA(ICycleB b) { }
    }

    public class CycleB : ICycleB
    {
        public CycleB(ICycleA a) { }
    }

    // ================= Test Cases =================

    public class EasyDiTests
    {
        [Fact]
        public void AddEasyServices_RegistersByConvention()
        {
            var services = new ServiceCollection();
            services.AddEasyServicesFromAssembly(typeof(EasyDiTests).Assembly, opt =>
            {
                opt.IncludeNamespaces = new() { "SkyWebFramework.DependencyInjection.Tests" };
            });

            var provider = services.BuildServiceProvider();
            var userService = provider.GetService<IUserService>();

            Assert.NotNull(userService);
        }

        [Fact]
        public void Priority1_MultipleInterfaces_ScopedResolvesToSameInstance()
        {
            var services = new ServiceCollection();
            services.AddEasyService<MultiScopedService>();

            var provider = services.BuildServiceProvider();

            using var scope1 = provider.CreateScope();
            var user1 = scope1.ServiceProvider.GetRequiredService<IUserService>();
            var cache1 = scope1.ServiceProvider.GetRequiredService<IUserCache>();
            var concrete1 = scope1.ServiceProvider.GetRequiredService<MultiScopedService>();

            Assert.Same(user1, cache1);
            Assert.Same(user1, concrete1);

            using var scope2 = provider.CreateScope();
            var user2 = scope2.ServiceProvider.GetRequiredService<IUserService>();
            Assert.NotSame(user1, user2);
        }

        [Fact]
        public void Priority1_MultipleInterfaces_SingletonResolvesToSameInstance()
        {
            var services = new ServiceCollection();
            services.AddEasyService<MultiSingletonService>();

            var provider = services.BuildServiceProvider();

            var user1 = provider.GetRequiredService<IUserService>();
            var cache1 = provider.GetRequiredService<IUserCache>();
            var concrete1 = provider.GetRequiredService<MultiSingletonService>();

            Assert.Same(user1, cache1);
            Assert.Same(user1, concrete1);
        }

        [Fact]
        public void Priority1_MultipleInterfaces_TransientPreservesNormalSemantics()
        {
            var services = new ServiceCollection();
            services.AddEasyService<MultiTransientService>();

            var provider = services.BuildServiceProvider();

            var user1 = provider.GetRequiredService<IUserService>();
            var user2 = provider.GetRequiredService<IUserService>();

            Assert.NotSame(user1, user2);
        }

        [Fact]
        public void Priority2_PreviewMode_MatchesPlanWithoutMutatingCollection()
        {
            var services = new ServiceCollection();
            var countBefore = services.Count;

            var preview = services.PreviewEasyServicesFromAssembly(typeof(EasyDiTests).Assembly);

            Assert.Equal(countBefore, services.Count);
            Assert.NotEmpty(preview);
            Assert.Contains(preview, p => p.ServiceType == typeof(IUserService));
        }

        [Fact]
        public void Priority3_Registry_IsThreadSafeAndTracksRegistrations()
        {
            var services = new ServiceCollection();
            services.AddEasyService<UserService>();

            var regs = services.GetEasyServiceRegistrations();
            Assert.NotEmpty(regs);

            // Verify safe copy
            var initialCount = regs.Count;
            regs.Clear();
            Assert.Equal(initialCount, services.GetEasyServiceRegistrations().Count);

            // Concurrent adds
            Parallel.For(0, 100, i =>
            {
                EasyDiRegistry.Track(services, new ServiceRegistrationInfo(typeof(IUserService), typeof(UserService), ServiceLifetime.Scoped, "Test", DateTime.UtcNow));
            });

            Assert.True(services.GetEasyServiceRegistrations().Count > initialCount);
        }

        [Fact]
        public void Priority4_SafeScanning_HandlesIgnoredAndInfrastructureTypes()
        {
            var services = new ServiceCollection();
            services.AddEasyServicesFromAssembly(typeof(EasyDiTests).Assembly, opt =>
            {
                opt.IncludeNamespaces = new() { "SkyWebFramework.DependencyInjection.Tests" };
            });

            var provider = services.BuildServiceProvider();
            var ignored = provider.GetService<IgnoredService>();
            Assert.Null(ignored);
        }

        [Fact]
        public void Priority5_OpenGenerics_ResolvesCorrectly()
        {
            var services = new ServiceCollection();
            services.AddEasyServicesFromAssembly(typeof(EasyDiTests).Assembly, opt =>
            {
                opt.IncludeNamespaces = new() { "SkyWebFramework.DependencyInjection.Tests" };
            });

            var provider = services.BuildServiceProvider();

            var stringRepo = provider.GetService<IRepository<string>>();
            var intRepo = provider.GetService<IRepository<int>>();

            Assert.NotNull(stringRepo);
            Assert.NotNull(intRepo);
            Assert.IsType<Repository<string>>(stringRepo);
            Assert.IsType<Repository<int>>(intRepo);
        }

        [Fact]
        public void Priority6_Validation_DetectsCaptiveDependencies()
        {
            var services = new ServiceCollection();
            services.AddScoped<IUserService, UserService>();
            services.AddSingleton<SingletonHoldingScoped>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                services.DetectCaptiveDependencies(throwOnError: true);
            });
        }

        [Fact]
        public void Priority7_Decorators_ChainsInCorrectOrderAndPreservesLifetime()
        {
            var services = new ServiceCollection();
            services.AddScoped<IDecoratedService, BaseDecoratedService>();
            services.Decorate<IDecoratedService, LoggingDecorator>();
            services.Decorate<IDecoratedService, AuditDecorator>();

            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDecoratedService>();

            Assert.Equal("Audited(Logged(Base))", service.Process());
        }

        [Fact]
        public void Priority8_FactoryMethods_InjectsDependenciesCorrectly()
        {
            var services = new ServiceCollection();
            services.AddEasyServicesFromAssembly(typeof(EasyDiTests).Assembly, opt =>
            {
                opt.IncludeNamespaces = new() { "SkyWebFramework.DependencyInjection.Tests" };
            });

            var provider = services.BuildServiceProvider();
            var factorySvc = provider.GetService<FactoryService>();

            Assert.NotNull(factorySvc);
            Assert.Equal("FromFactory", factorySvc.Name);
        }

        [Fact]
        public void Priority9_RegisterAs_ValidatesAssignability()
        {
            var services = new ServiceCollection();
            services.AddEasyService<ExplicitService>();

            var provider = services.BuildServiceProvider();
            var custom = provider.GetService<ICustomService>();
            Assert.NotNull(custom);
        }

        [Fact]
        public void Priority10_DuplicateBehavior_WorksAsExpected()
        {
            var services = new ServiceCollection();
            services.AddScoped<IUserService, UserService>();

            var options = new EasyDiOptions
            {
                DuplicateBehavior = DuplicateRegistrationBehavior.Throw
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                services.AddEasyServicesFromAssembly(typeof(EasyDiTests).Assembly, opt =>
                {
                    opt.DuplicateBehavior = DuplicateRegistrationBehavior.Throw;
                    opt.IncludeNamespaces = new() { "SkyWebFramework.DependencyInjection.Tests" };
                });
            });
        }

        [Fact]
        public void Priority13_LazyResolution_WorksProperly()
        {
            var services = new ServiceCollection();
            services.AddScoped<IUserService, UserService>();
            services.EnableLazyResolution();

            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var lazyUser = scope.ServiceProvider.GetService<Lazy<IUserService>>();

            Assert.NotNull(lazyUser);
            Assert.Equal("User1", lazyUser.Value.GetName());
        }
    }
}

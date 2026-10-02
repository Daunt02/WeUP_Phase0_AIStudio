using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WeUP.Application.Resolution;
using WeUP.Domain.Resolution;
using WeUP.Infrastructure.Persistence;
using Xunit;

namespace WeUP.Tests.Resolution;

/// <summary>
/// Regression guard for the Postgres-mode startup defect (WEUP-DI-REPAIR-001):
/// <c>IProvenanceService</c> is registered as a singleton while
/// <c>WeUpDbContext</c> is scoped and Postgres-mode-only. A singleton capturing
/// a scoped service crashes startup under scope validation. The service must
/// remain constructible without any scoped dependency.
/// </summary>
public sealed class ProvenanceServiceRegistrationTests
{
    [Fact]
    public void SingletonRegistration_WithScopedDbContext_ResolvesWithoutCaptiveDependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProvenanceService, ProvenanceService>();
        // Stand-in for the Postgres-mode registration (scoped WeUpDbContext).
        // The factory is never invoked post-fix; pre-fix, scope validation throws first.
        services.AddScoped<WeUpDbContext>(_ => null!);
        // The generic host always provides ILogger<T>; without it the container
        // would silently fall back to the parameterless ctor and mask the defect.
        services.AddSingleton<ILogger<ProvenanceService>>(NullLogger<ProvenanceService>.Instance);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        var svc = provider.GetRequiredService<IProvenanceService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public void ProvenanceService_ExposesSingleParameterlessConstructor()
    {
        var ctor = Assert.Single(typeof(ProvenanceService).GetConstructors());
        Assert.Empty(ctor.GetParameters());
    }
}

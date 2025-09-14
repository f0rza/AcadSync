using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AcadSync.Processor.Configuration;
using AcadSync.Processor.Interfaces;
using AcadSync.Processor.Services;

namespace AcadSync.Processor.Extensions;

/// <summary>
/// Extension methods for configuring AcadSync Processor services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add AcadSync Processor services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="connectionString">Database connection string (optional, can be configured via options)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAcadSyncProcessor(
        this IServiceCollection services, 
        IConfiguration configuration,
        string? connectionString = null)
    {
        // Configure options
        services.Configure<ProcessorOptions>(options =>
        {
            configuration.GetSection(ProcessorOptions.SectionName).Bind(options);
            
            // Override connection string if provided
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.ConnectionString = connectionString;
            }
        });

        // Register core services
        services.AddScoped<IRuleEngine, RuleEngine>();
        services.AddScoped<IRuleLoader, FileSystemRuleLoader>();
        services.AddScoped<IEntityService, EntityService>();
        services.AddScoped<IValidationService, ValidationOrchestrator>();
        services.AddScoped<IRepairService, RepairService>();
        services.AddScoped<IRevertService, RevertService>();

        // Register repository (this assumes AnthologyExtPropRepository is the implementation)
        services.AddScoped<IExtPropRepository>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProcessorOptions>>();
            return new AnthologyExtPropRepository(options.Value.ConnectionString);
        });

        return services;
    }

    /// <summary>
    /// Add AcadSync Processor services with a custom repository implementation
    /// </summary>
    /// <typeparam name="TRepository">The repository implementation type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAcadSyncProcessor<TRepository>(
        this IServiceCollection services, 
        IConfiguration configuration)
        where TRepository : class, IExtPropRepository
    {
        // Configure options
        services.Configure<ProcessorOptions>(options =>
        {
            configuration.GetSection(ProcessorOptions.SectionName).Bind(options);
        });

        // Register core services
        services.AddScoped<IRuleEngine, RuleEngine>();
        services.AddScoped<IRuleLoader, FileSystemRuleLoader>();
        services.AddScoped<IEntityService, EntityService>();
        services.AddScoped<IValidationService, ValidationOrchestrator>();
        services.AddScoped<IRepairService, RepairService>();
        services.AddScoped<IRevertService, RevertService>();

        // Register custom repository
        services.AddScoped<IExtPropRepository, TRepository>();

        return services;
    }

    /// <summary>
    /// Add AcadSync Processor services with manual configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure processor options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAcadSyncProcessor(
        this IServiceCollection services,
        Action<ProcessorOptions> configureOptions)
    {
        // Configure options
        services.Configure(configureOptions);

        // Register core services
        services.AddScoped<IRuleEngine, RuleEngine>();
        services.AddScoped<IRuleLoader, FileSystemRuleLoader>();
        services.AddScoped<IEntityService, EntityService>();
        services.AddScoped<IValidationService, ValidationOrchestrator>();
        services.AddScoped<IRepairService, RepairService>();
        services.AddScoped<IRevertService, RevertService>();

        // Register repository
        services.AddScoped<IExtPropRepository>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProcessorOptions>>();
            return new AnthologyExtPropRepository(options.Value.ConnectionString);
        });

        return services;
    }

    /// <summary>
    /// Add only the core validation services (without repair functionality)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration instance</param>
    /// <param name="connectionString">Database connection string (optional)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAcadSyncValidation(
        this IServiceCollection services,
        IConfiguration configuration,
        string? connectionString = null)
    {
        // Configure options
        services.Configure<ProcessorOptions>(options =>
        {
            configuration.GetSection(ProcessorOptions.SectionName).Bind(options);
            
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.ConnectionString = connectionString;
            }
        });

        // Register validation-only services
        services.AddScoped<IRuleEngine, RuleEngine>();
        services.AddScoped<IRuleLoader, FileSystemRuleLoader>();
        services.AddScoped<IEntityService, EntityService>();
        services.AddScoped<IValidationService, ValidationOrchestrator>();

        // Register repository
        services.AddScoped<IExtPropRepository>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProcessorOptions>>();
            return new AnthologyExtPropRepository(options.Value.ConnectionString);
        });

        return services;
    }
}

using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using AykutOnPC.Infrastructure.Services;
using AykutOnPC.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.SemanticKernel;

namespace AykutOnPC.Web.Extensions;

public static class ServiceExtensions
{
    /// <summary>
    /// Registers all strongly-typed configuration sections.
    /// </summary>
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind structured sections
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<GeminiSettings>(configuration.GetSection(GeminiSettings.SectionName));
        services.Configure<SeedDataSettings>(configuration.GetSection(SeedDataSettings.SectionName));

        // Fallback for flat environment variables (common in Render/Docker)
        services.PostConfigure<GeminiSettings>(options =>
        {
            if (string.IsNullOrEmpty(options.ApiKey))
                options.ApiKey = configuration["GEMINI_API_KEY"] ?? string.Empty;
        });

        services.PostConfigure<SeedDataSettings>(options =>
        {
            if (string.IsNullOrEmpty(options.AdminUser.Password))
                options.AdminUser.Password = configuration["ADMIN_PASSWORD"] ?? string.Empty;
        });

        return services;
    }

    /// <summary>
    /// Registers JWT Bearer authentication with cookie-based token reading.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("JwtSettings:Key is not configured.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.Token = context.Request.Cookies["AykutOnPC.AuthToken"];
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    context.Response.Redirect("/Account/Login");
                    context.HandleResponse();
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Registers Entity Framework Core with PostgreSQL.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    sqlOptions.CommandTimeout(30);
                }));

        return services;
    }

    /// <summary>
    /// Registers all application services (business logic layer).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Auth
        services.AddScoped<IAuthService, AuthService>();

        // Domain Services
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IEducationService, EducationService>();
        services.AddScoped<IExperienceService, ExperienceService>();
        services.AddScoped<ISpecService, SpecService>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

        // AI Service & Kernel Registration
        services.AddTransient<AykutOnPC.Infrastructure.HttpHandlers.OpenAIToTargetHandler>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<GeminiSettings>>().Value;
            return new AykutOnPC.Infrastructure.HttpHandlers.OpenAIToTargetHandler(settings.Endpoint);
        });

        services.AddHttpClient("GroqClient")
            .AddHttpMessageHandler<AykutOnPC.Infrastructure.HttpHandlers.OpenAIToTargetHandler>();

        services.AddSingleton<Microsoft.SemanticKernel.Kernel>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<GeminiSettings>>().Value;
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                return Microsoft.SemanticKernel.Kernel.CreateBuilder().Build();

            var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            
            builder.AddOpenAIChatCompletion(
                modelId: settings.ModelId,
                apiKey: settings.ApiKey,
                httpClient: factory.CreateClient("GroqClient")
            );
            return builder.Build();
        });

        services.AddScoped<IAIService, GeminiService>();

        // GitHub Service (uses HttpClient factory)
        services.AddHttpClient<IGitHubService, GitHubService>();

        return services;
    }

    /// <summary>
    /// Registers CORS policy for API endpoints.
    /// </summary>
    public static IServiceCollection AddAppCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("DefaultPolicy", policy =>
            {
                var allowedOrigins = configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();

                if (allowedOrigins is { Length: > 0 })
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
                else
                {
                    // Development fallback
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Registers rate limiting for API endpoints.
    /// </summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("ChatApiPolicy", limiterOptions =>
            {
                limiterOptions.PermitLimit = 10;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 2;
            });

            options.AddFixedWindowLimiter("GeneralApiPolicy", limiterOptions =>
            {
                limiterOptions.PermitLimit = 60;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 5;
            });
        });

        return services;
    }

    /// <summary>
    /// Registers global exception handler.
    /// </summary>
    public static IServiceCollection AddAppExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    /// <summary>
    /// Registers health checks for database and external services.
    /// </summary>
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}

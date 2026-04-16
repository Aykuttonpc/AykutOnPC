using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using AykutOnPC.Infrastructure.HttpHandlers;
using AykutOnPC.Infrastructure.Services;
using AykutOnPC.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using System.Text;
using System.Threading.RateLimiting;

namespace AykutOnPC.Web.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<AiSettings>(configuration.GetSection(AiSettings.SectionName));
        services.Configure<SeedDataSettings>(configuration.GetSection(SeedDataSettings.SectionName));
        services.Configure<GitHubSettings>(configuration.GetSection(GitHubSettings.SectionName));
        services.Configure<SecuritySettings>(configuration.GetSection(SecuritySettings.SectionName));

        // Fallback for flat Docker environment variables
        services.PostConfigure<AiSettings>(options =>
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

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        var key = jwtSection["Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "JwtSettings:Key is not configured or is empty. " +
                "Set it in appsettings.Development.json (dev) or via environment variable JWT_SECRET_KEY (production).");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = jwtSection["Issuer"],
                ValidAudience            = jwtSection["Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ClockSkew                = TimeSpan.FromMinutes(1)
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

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                    sqlOptions.CommandTimeout(90);
                }));

        return services;
    }

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

        // AI: Groq via Semantic Kernel + OpenAI-compatible handler
        services.AddTransient<OpenAIToTargetHandler>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AiSettings>>().Value;
            return new OpenAIToTargetHandler(settings.Endpoint);
        });

        services.AddHttpClient("GroqClient")
            .AddHttpMessageHandler<OpenAIToTargetHandler>();

        services.AddSingleton<Kernel>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AiSettings>>().Value;
            var logger   = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AiKernelFactory");

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                logger.LogWarning("AI Kernel built WITHOUT API key — chat will return ApiNotConfigured.");
                return Kernel.CreateBuilder().Build();
            }

            logger.LogInformation("AI Kernel building — Model={Model} Endpoint={Endpoint}",
                settings.ModelId, settings.Endpoint);

            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId:    settings.ModelId,
                    apiKey:     settings.ApiKey,
                    httpClient: factory.CreateClient("GroqClient"))
                .Build();
        });

        services.AddScoped<IAiService, AiService>();

        // GitHub (typed HttpClient)
        services.AddHttpClient<IGitHubService, GitHubService>();

        // Visitor Intelligence
        services.AddScoped<IVisitorAnalyticsService, VisitorAnalyticsService>();

        return services;
    }

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
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("ChatApiPolicy", o =>
            {
                o.PermitLimit             = 10;
                o.Window                  = TimeSpan.FromMinutes(1);
                o.QueueProcessingOrder    = QueueProcessingOrder.OldestFirst;
                o.QueueLimit              = 2;
            });

            options.AddFixedWindowLimiter("GeneralApiPolicy", o =>
            {
                o.PermitLimit             = 60;
                o.Window                  = TimeSpan.FromMinutes(1);
                o.QueueProcessingOrder    = QueueProcessingOrder.OldestFirst;
                o.QueueLimit              = 5;
            });
        });

        return services;
    }

    public static IServiceCollection AddAppExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");
        return services;
    }
}

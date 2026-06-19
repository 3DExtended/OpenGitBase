using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenGitBase.Api.Middleware;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Swagger;
using OpenGitBase.Common;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api;

public class Startup
{
    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Configuration = configuration;
        Environment = environment;
    }

    public IConfiguration Configuration { get; }

    public IWebHostEnvironment Environment { get; }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1.0/swagger.json", "Versioned API v1.0");
            });
        }

        app.UseRouting();

        if (!env.IsEnvironment("E2ETest"))
        {
            app.UseMiddleware<InternalNetworkMiddleware>();
            app.UseRateLimiter();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // agentGenCli:auth-middleware
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    public void ConfigureServices(IServiceCollection services)
    {
        DependencyInjectionHelpers
            .ConfigureGlobalServices(
                services,
                Configuration,
                FeatureRegistration.GetFeatureAssemblies()
            )
            .GetAwaiter()
            .GetResult();

        services.AddControllers();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SchemaFilter<EnumSchemaFilter>();
            c.SchemaFilter<OptionStringSchemaFilter>();

            c.SwaggerDoc("v1.0", new OpenApiInfo { Title = "Main API v1.0", Version = "v1.0" });
            c.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description =
                        "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
                }
            );
            c.AddSecurityRequirement(
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer",
                            },
                        },
                        Array.Empty<string>()
                    },
                }
            );
        });
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISshKeyService, SshKeyService>();
        services.AddSingleton<ISendGridEmailSender, SendGridEmailSender>();
        var jwtOptions = Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        services.AddSingleton(jwtOptions);
        services.AddSingleton(
            Configuration.GetSection("Encryption").Get<EncryptionOptions>()
                ?? new EncryptionOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("Google").Get<GoogleAuthOptions>() ?? new GoogleAuthOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("Apple").Get<AppleAuthOptions>() ?? new AppleAuthOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("RepositoryStorageQuota").Get<RepositoryStorageQuotaOptions>()
                ?? new RepositoryStorageQuotaOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("Debug").Get<DebugFeaturesOptions>()
                ?? new DebugFeaturesOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("StorageNode").Get<StorageNodeOptions>()
                ?? new StorageNodeOptions()
        );
        var gitOptions =
            Configuration.GetSection("Git").Get<GitOptions>() ?? new GitOptions();
        if (bool.TryParse(Configuration["GIT_SSH_ENABLED"], out var sshEnabledFromEnv))
        {
            gitOptions.SshEnabled = sshEnabledFromEnv;
        }

        services.AddSingleton(gitOptions);
        services.Configure<InternalNetworkOptions>(Configuration.GetSection("InternalNetwork"));
        if (Environment.IsEnvironment("E2ETest"))
        {
            services.PostConfigure<InternalNetworkOptions>(options => options.Enabled = false);
        }

        if (!Environment.IsEnvironment("E2ETest"))
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(
                    "sensitive",
                    context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 60,
                                Window = TimeSpan.FromMinutes(1),
                            }
                        )
                );
            });
        }

        services.Configure<AdminSeedOptions>(Configuration.GetSection("AdminSeed"));
        services.AddHostedService<AdminUserSeedService>();
        services.AddTransient<
            IQueryHandler<GenerateFleetDispatcherSshKeysQuery, GenerateFleetDispatcherSshKeysResult>,
            GenerateFleetDispatcherSshKeysQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<GetFleetDispatcherSshPublicKeyQuery, string>,
            GetFleetDispatcherSshPublicKeyQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<GetFleetDispatcherSshPrivateKeyQuery, string>,
            GetFleetDispatcherSshPrivateKeyQueryHandler
        >();
        services.AddSingleton<IJWTTokenGenerator, JWTTokenGenerator>();
        services.AddSingleton<IGoogleIdentityTokenValidator, GoogleIdentityTokenValidator>();
        services.AddSingleton<IEmailProtectionService, EmailProtectionService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<IAuthCookieService, AuthCookieService>();
        services.AddScoped<IOrganizationAccessService, OrganizationAccessService>();
        services.AddScoped<IUserContext, UserContextProvider>();
        services.AddHttpClient<IStorageProvisionerClient, StorageProvisionerClient>();
        services.AddTransient<
            IQueryHandler<
                CreateRepositoryWithStorageQuery,
                CreateRepositoryWithStorageResult
            >,
            CreateRepositoryWithStorageQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<
                DeleteRepositoryWithStorageQuery,
                DeleteRepositoryWithStorageResult
            >,
            DeleteRepositoryWithStorageQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<UserDeleteAccountQuery, UserDeleteAccountResult>,
            UserDeleteAccountQueryHandler
        >();
        services.AddSingleton(
            Configuration.GetSection("RepositoryStorageQuota").Get<RepositoryStorageQuotaOptions>()
                ?? new RepositoryStorageQuotaOptions()
        );
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (
                            string.IsNullOrEmpty(context.Token)
                            && context.Request.Cookies.TryGetValue(
                                AuthCookieOptions.CookieName,
                                out var cookieToken
                            )
                        )
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtOptions.Issuer ?? "api",
                    ValidAudience = jwtOptions.Audience ?? "api",
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            string.IsNullOrEmpty(jwtOptions.Key)
                                ? string.Join(string.Empty, Enumerable.Repeat("dev-key", 32))
                                : jwtOptions.Key
                        )
                    ),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        // agentGenCli:auth-services
    }
}

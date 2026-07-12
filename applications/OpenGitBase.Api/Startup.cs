using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
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
using OpenGitBase.Features.Pipeline.Services;
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

        if (!env.IsEnvironment("E2ETest"))
        {
            app.UseForwardedHeaders();
        }

        app.UseRouting();

        if (!env.IsEnvironment("E2ETest"))
        {
            app.UseMiddleware<InternalNetworkMiddleware>();
        }

        if (!env.IsEnvironment("E2ETest") && !Configuration.GetValue<bool>("E2E:CaptureEmail"))
        {
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
        ProductionSecretsValidator.Validate(Configuration, Environment);

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
        var redisUrl = Configuration["REDIS_URL"];
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            var redisConfiguration = redisUrl.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
                ? redisUrl["redis://".Length..]
                : redisUrl;
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConfiguration);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<RepositoryContentAuthorizationService>();
        services.AddScoped<ComputeNodeIdentityService>();
        services.AddScoped<RepositoryResponseMapper>();
        services.AddScoped<DiscussionAuthorizationService>();
        services.AddScoped<MergeRequestAuthorizationService>();
        services.AddScoped<MergeRequestRefService>();
        services.AddScoped<MergeRequestCompareService>();
        services.AddScoped<MergeRequestMergeService>();
        services.AddScoped<GitPushEnforcementService>();
        services.AddSingleton<WebReadReplicaSelector>();
        services.AddScoped<RepositoryContentService>();
        services.AddScoped<IRepositoryDiskUsageProvider>(sp => sp.GetRequiredService<RepositoryContentService>());
        services.AddScoped<IRepositoryContentCache, RepositoryContentCacheService>();
        services.AddHttpClient<IStorageContentClient, StorageContentClient>();
        services.AddHttpClient<ILayerStoreClient, LayerStoreClient>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISshKeyService, SshKeyService>();
        var e2eCaptureEmail = string.Equals(Configuration["E2E:CaptureEmail"], "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Configuration["E2E__CaptureEmail"], "true", StringComparison.OrdinalIgnoreCase);
        if (!e2eCaptureEmail)
        {
            services.AddSingleton<ISendGridEmailSender, SendGridEmailSender>();
        }

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
            Configuration.GetSection("PlatformMergeIdentity").Get<PlatformMergeIdentityOptions>()
                ?? new PlatformMergeIdentityOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("Debug").Get<DebugFeaturesOptions>()
                ?? new DebugFeaturesOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("StorageNode").Get<StorageNodeOptions>()
                ?? new StorageNodeOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions()
        );
        services.AddSingleton(
            Configuration.GetSection("LayerStore").Get<LayerStoreOptions>()
                ?? new LayerStoreOptions()
        );
        services.Configure<FleetComponentOptions>(Configuration.GetSection("FleetComponent"));
        services.AddSingleton(
            sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FleetComponentOptions>>().Value
        );
        if (Environment.IsEnvironment("E2ETest"))
        {
            services.PostConfigure<FleetComponentOptions>(options =>
            {
                options.SelfRegistrationEnabled = false;
            });
        }

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
        else
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                var internalNetwork =
                    Configuration.GetSection("InternalNetwork").Get<InternalNetworkOptions>()
                    ?? new InternalNetworkOptions();
                ConfigureForwardedHeaders(options, internalNetwork);
            });
        }

        if (!Environment.IsEnvironment("E2ETest") && !Configuration.GetValue<bool>("E2E:CaptureEmail"))
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
                options.AddPolicy(
                    "content-browse-anonymous",
                    context =>
                    {
                        var partitionKey =
                            context.User.Identity?.IsAuthenticated == true
                                ? $"auth-content:{context.User.FindFirst("identityproviderid")?.Value ?? "user"}"
                                : $"anon-content:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
                        var permitLimit =
                            context.User.Identity?.IsAuthenticated == true ? 600 : 120;
                        return RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey,
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = permitLimit,
                                Window = TimeSpan.FromMinutes(1),
                            }
                        );
                    }
                );
            });
        }

        services.Configure<AdminSeedOptions>(Configuration.GetSection("AdminSeed"));
        services.Configure<HaStorageBackgroundOptions>(Configuration.GetSection("HaStorageBackground"));
        if (Environment.IsEnvironment("E2ETest"))
        {
            services.PostConfigure<HaStorageBackgroundOptions>(options => options.Enabled = false);
        }

        services.AddHostedService<AdminUserSeedService>();
        services.AddHostedService<HaStorageBackgroundService>();
        if (!Environment.IsEnvironment("E2ETest"))
        {
            services.AddHostedService<ApiFleetComponentRegistrationService>();
            services.AddHostedService<GitPushReceivedConsumer>();
            services.AddHostedService<JobDispatchCoordinator>();
            services.AddHostedService<DependencyLayerPromotionWorker>();
            services.AddHostedService<JobTimeoutEnforcerService>();
        }

        services.Configure<StatusProbeOptions>(Configuration.GetSection("StatusProbe"));
        services.AddSingleton(
            sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StatusProbeOptions>>().Value
        );
        if (Environment.IsEnvironment("E2ETest"))
        {
            services.PostConfigure<StatusProbeOptions>(options => options.Enabled = false);
        }

        services.AddHttpClient(
            nameof(OpenGitBase.Features.Status.Services.StatusProbeEngine)
        );
        services.AddSingleton<OpenGitBase.Features.Status.Services.StatusProbeEngine>();
        services.AddSingleton<OpenGitBase.Features.Status.Services.StorageGroupStatusBuilder>();
        services.AddSingleton<OpenGitBase.Features.Status.Services.StatusHistoryService>();
        services.AddScoped<OpenGitBase.Features.Status.Services.StatusAggregatorService>();
        services.AddHostedService<StatusAggregatorBackgroundService>();
        services.AddScoped<Rf1BackfillService>();
        services.AddScoped<Rf4BackfillService>();
        services.AddScoped<RebalanceService>();
        services.AddScoped<AntiEntropyReconcilerService>();
        services.AddScoped<IColdRecoveryService, ColdRecoveryService>();
        services.AddScoped<IRepositoryByteOverrideService, RepositoryByteOverrideService>();
        services.AddTransient<
            IQueryHandler<
                GetRepositoryByteOverrideEligibilityQuery,
                RepositoryByteOverrideEligibilityDto
            >,
            GetRepositoryByteOverrideEligibilityQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<UpdateRepositoryMaxBytesOverrideQuery, RepositoryDto>,
            UpdateRepositoryMaxBytesOverrideQueryHandler
        >();
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
        services.AddSingleton<IRepositoryKeyProtectionService, RepositoryKeyProtectionService>();
        services.AddSingleton<IEncryptedArtifactService, EncryptedArtifactService>();
        services.AddSingleton<IRepositoryKeyRotationService, RepositoryKeyRotationService>();
        services.AddSingleton<IRepositoryKeyService, RepositoryKeyService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<KafkaPipelineEventPublisher>();
        services.AddSingleton<IGitPushEventPublisher>(sp =>
            sp.GetRequiredService<KafkaPipelineEventPublisher>()
        );
        services.AddSingleton<IJobAvailableEventPublisher>(sp =>
            sp.GetRequiredService<KafkaPipelineEventPublisher>()
        );
        services.AddSingleton<IJobCancelledEventPublisher>(sp =>
            sp.GetRequiredService<KafkaPipelineEventPublisher>()
        );
        services.AddScoped<IAuthCookieService, AuthCookieService>();
        services.AddScoped<IOrganizationAccessService, OrganizationAccessService>();
        services.AddScoped<IUserContext, UserContextProvider>();
        services.AddHttpClient<IStorageProvisionerClient, StorageProvisionerClient>(client =>
            client.Timeout = TimeSpan.FromMinutes(5));
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
            IQueryHandler<
                CommitReplicationWatermarkQuery,
                CommitReplicationWatermarkResult
            >,
            CommitReplicationWatermarkQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<
                GetRepositoryReplicationContextQuery,
                RepositoryReplicationContextDto
            >,
            GetRepositoryReplicationContextQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<
                QuorumReplicateRepositoryQuery,
                QuorumReplicateRepositoryResult
            >,
            QuorumReplicateRepositoryQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<ApplyRepositoryWatermarksQuery, ApplyRepositoryWatermarksResult>,
            ApplyRepositoryWatermarksQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<
                RepositoryReplicationRoutingQuery,
                RepositoryReplicationRoutingDto
            >,
            RepositoryReplicationRoutingQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<
                ListAdminRepositoryReplicationQuery,
                ListAdminRepositoryReplicationResult
            >,
            ListAdminRepositoryReplicationQueryHandler
        >();
        services.AddTransient<
            IQueryHandler<PromotePrimaryReplicaQuery, PromotePrimaryReplicaResult>,
            PromotePrimaryReplicaQueryHandler
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

    private static void ConfigureForwardedHeaders(
        ForwardedHeadersOptions options,
        InternalNetworkOptions internalNetwork
    )
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var cidr in internalNetwork.TrustedProxyNetworks)
        {
            if (TryParseCidr(cidr, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }

        foreach (var address in internalNetwork.TrustedProxyAddresses)
        {
            if (IPAddress.TryParse(address, out var ip))
            {
                options.KnownProxies.Add(ip);
            }
        }
    }

    private static bool TryParseCidr(string cidr, out System.Net.IPNetwork network)
    {
        network = default;
        var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var prefix))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        var maxPrefix = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            return false;
        }

        network = new System.Net.IPNetwork(prefix, prefixLength);
        return true;
    }
}

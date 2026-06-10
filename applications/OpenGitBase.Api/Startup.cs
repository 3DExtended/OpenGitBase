using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Swagger;
using OpenGitBase.Common;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

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
        services.AddSingleton<IJWTTokenGenerator, JWTTokenGenerator>();
        services.AddSingleton<IGoogleIdentityTokenValidator, GoogleIdentityTokenValidator>();
        services.AddSingleton<IEmailProtectionService, EmailProtectionService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<IUserContext, UserContextProvider>();
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
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

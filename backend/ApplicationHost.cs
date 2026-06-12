using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.EventLog;
using Prometheus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using KasseAPI_Final.Hubs;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Auth;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.OpenApi;
using KasseAPI_Final.Swagger;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.FileProviders;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.LegalExportCompleteness;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.Backup.PgDump;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Services.OperationalRuns;
using KasseAPI_Final.Services.AdminProducts;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Sockets;

namespace KasseAPI_Final;

internal static class ApplicationHost
{
    /// <summary>
    /// Development-only CORS origin check: loopback (any port) and RFC1918 IPv4 (Expo on LAN).
    /// Avoids brittle hard-coded LAN IPs and IPv6 (::1) mismatches vs localhost.
    /// </summary>
    private static bool IsTrustedDevelopmentCorsOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (TenantHostNames.IsTrustedLocalDevCorsHost(uri.Host)) return true;
        if (!IPAddress.TryParse(uri.Host, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        if (b.Length != 4) return false;
        if (b[0] == 10) return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        return b[0] == 192 && b[1] == 168;
    }

    /// <summary>
    /// Development HTTP listen port: first non-default port from semicolon-separated configuration <c>Urls</c>, else 5184.
    /// </summary>
    private static int GetDevelopmentHttpListenPort(IConfiguration configuration)
    {
        const int defaultPort = 5184;
        var raw = configuration["Urls"];
        if (string.IsNullOrWhiteSpace(raw))
            return defaultPort;

        foreach (var segment in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(segment, UriKind.Absolute, out var uri))
                continue;
            if (uri.Port <= 0)
                continue;
            var isDefaultWellKnown =
                (uri.Scheme == Uri.UriSchemeHttp && uri.Port == 80)
                || (uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443);
            if (!isDefaultWellKnown)
                return uri.Port;
        }

        return defaultPort;
    }

    private static void ConfigureAppDbContextOptions(
        DbContextOptionsBuilder options,
        string connectionString,
        bool isDevelopment)
    {
        var inMemoryDatabaseName = Environment.GetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(inMemoryDatabaseName))
        {
            options.UseInMemoryDatabase(inMemoryDatabaseName);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            if (isDevelopment)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }

            return;
        }

        options.UseNpgsql(connectionString);
        options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        options.AddInterceptors(NpgsqlTimestamptzUtcParameterInterceptor.Instance);
        if (isDevelopment)
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    }

    public static WebApplication CreateWebApplication(string[] args)
    {
        WebApplicationBuilder builder;
        if (OpenApiExportMode.IsEnabled)
        {
            // Swashbuckle CLI runs as "dotnet exec" with the tool as entry assembly; MVC must use the API assembly for controller discovery.
            builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ApplicationName = typeof(SwaggerHostFactory).Assembly.GetName().Name,
            });
            builder.Configuration.AddInMemoryCollection(OpenApiExportConfiguration.BuildInMemoryDefaults());
        }
        else
        {
            builder = WebApplication.CreateBuilder(args);
        }

        var isDevelopment = builder.Environment.IsDevelopment();

        // On Windows, EventLog provider can throw ObjectDisposedException during shutdown races
        // when background services still emit logs. Keep other providers active and mute EventLog.
        if (OperatingSystem.IsWindows())
        {
            builder.Logging.AddFilter<EventLogLoggerProvider>(null, LogLevel.None);
        }

// Configuration Binding
builder.Services.AddScoped<ICompanyProfileProvider, CompanyProfileProvider>();
builder.Services.Configure<ProductMediaOptions>(builder.Configuration.GetSection(ProductMediaOptions.SectionName));
builder.Services.AddScoped<ProductImageThumbnailService>();
builder.Services.AddScoped<ICashRegisterSettingsService, CashRegisterSettingsService>();
builder.Services.Configure<CashRegisterComplianceOptions>(
    builder.Configuration.GetSection(CashRegisterComplianceOptions.SectionName));
builder.Services.Configure<InventoryOptions>(builder.Configuration.GetSection(InventoryOptions.SectionName));
builder.Services.Configure<TseOptions>(builder.Configuration.GetSection(TseOptions.SectionName));
builder.Services.Configure<FiskalyOptions>(builder.Configuration.GetSection(FiskalyOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<AuditRetentionOptions>(builder.Configuration.GetSection(AuditRetentionOptions.SectionName));
builder.Services.Configure<FinanzOnlineConnectivityOptions>(builder.Configuration.GetSection(FinanzOnlineConnectivityOptions.SectionName));
builder.Services.Configure<FinanzOnlineSessionOptions>(builder.Configuration.GetSection(FinanzOnlineSessionOptions.SectionName));
builder.Services.Configure<FinanzOnlineRegistrierkassenOptions>(builder.Configuration.GetSection(FinanzOnlineRegistrierkassenOptions.SectionName));
builder.Services.Configure<FinanzOnlineTransmissionQueryOptions>(builder.Configuration.GetSection(FinanzOnlineTransmissionQueryOptions.SectionName));
builder.Services.Configure<FinanzOnlineOutboxOptions>(builder.Configuration.GetSection(FinanzOnlineOutboxOptions.SectionName));
builder.Services.Configure<FinanzOnlineCutoverGuardOptions>(builder.Configuration.GetSection(FinanzOnlineCutoverGuardOptions.SectionName));
builder.Services.Configure<FinanzOnlineDevTestOptions>(builder.Configuration.GetSection(FinanzOnlineDevTestOptions.SectionName));
builder.Services.Configure<FinanzOnlineSimulationDeveloperOptions>(
    builder.Configuration.GetSection(FinanzOnlineSimulationDeveloperOptions.SectionName));
builder.Services.Configure<FinanzOnlineSimulationOptions>(
    builder.Configuration.GetSection(FinanzOnlineSimulationOptions.SectionName));
builder.Services.Configure<RksvFinanzOnlineSubmissionClientOptions>(
    builder.Configuration.GetSection(RksvFinanzOnlineSubmissionClientOptions.SectionName));
builder.Services.Configure<NtpSettings>(builder.Configuration.GetSection(NtpSettings.SectionName));
builder.Services.Configure<DevelopmentOptions>(builder.Configuration.GetSection(DevelopmentOptions.SectionName));
builder.Services.Configure<OfflineVoucherEncryptionOptions>(
    builder.Configuration.GetSection(OfflineVoucherEncryptionOptions.SectionName));
builder.Services.Configure<LicenseOptions>(builder.Configuration.GetSection(LicenseOptions.SectionName));
builder.Services.Configure<LicenseSettingsOptions>(builder.Configuration.GetSection(LicenseSettingsOptions.SectionName));
builder.Services.AddSingleton<IPostConfigureOptions<LicenseOptions>, LicenseOptionsFromFilesPostConfigure>();

if (isDevelopment)
{
    builder.Services.Configure<LicenseOptions>(options => options.Enabled = false);
    builder.Services.Configure<TseOptions>(options =>
    {
        options.OfflineModeEnabled = true;
        options.MaxOfflineTransactionsPerCashRegister = LicenseEnforcementPolicy.MaxOfflineTransactionsUnlimited;
    });
}
builder.Services.Configure<AppUpdateOptions>(builder.Configuration.GetSection(AppUpdateOptions.SectionName));
builder.Services.Configure<EmailSmtpOptions>(builder.Configuration.GetSection(EmailSmtpOptions.SectionName));
builder.Services.AddActivityServices(builder.Configuration);
builder.Services.AddHttpClient("LicenseRemote", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<ILicenseStorageService, LicenseStorageService>();
builder.Services.AddLicenseServices(builder.Environment);
builder.Services.AddScoped<DeploymentLicenseValidator>();
// Scoped: holds AppDbContext per request for the issuance audit row.
builder.Services.AddScoped<ILicenseSyncService, LicenseSyncService>();
builder.Services.AddScoped<ILicenseIssuanceService, LicenseIssuanceService>();
builder.Services.AddHostedService<LicenseComplianceHostedService>();
builder.Services.AddSingleton<ILicenseReminderNotificationStore, LicenseReminderNotificationStore>();
builder.Services.AddSingleton<ILicenseReminderEmailSender, LicenseReminderEmailSender>();
builder.Services.AddHostedService<LicenseReminderHostedService>();
builder.Services.Configure<LicenseReportEmailOptions>(builder.Configuration.GetSection(LicenseReportEmailOptions.SectionName));
builder.Services.AddScoped<ILicenseExportReportService, LicenseExportReportService>();
builder.Services.AddHostedService<LicenseScheduledReportsHostedService>();
builder.Services.AddSingleton<INtpTimeSyncStatus, NtpTimeSyncStatus>();
// Singleton + IDisposable: 30s timer refresh + immediate refresh on update (see DevelopmentModeService.CacheTtl).
builder.Services.AddSingleton<IDevelopmentModeService, DevelopmentModeService>();
// INtpEffectiveSettingsProvider: PaymentService fiscal NTP guard, SystemController, NtpTimeSyncService (when hosted).
builder.Services.AddScoped<INtpEffectiveSettingsProvider, NtpEffectiveSettingsProvider>();
builder.Services.AddScoped<NtpSynchronizationCoordinator>();
builder.Services.AddScoped<INtpSynchronizationCoordinator>(sp => sp.GetRequiredService<NtpSynchronizationCoordinator>());
builder.Services.AddSingleton<FinanzOnlineDeveloperSimulationEngine>();
if (!OpenApiExportMode.IsEnabled)
{
    builder.Services.AddSingleton<IValidateOptions<BackupOptions>, BackupOptionsValidator>();
    builder.Services.AddOptions<BackupOptions>()
        .Bind(builder.Configuration.GetSection(BackupOptions.SectionName))
        .ValidateOnStart();

    builder.Services.AddSingleton<IValidateOptions<RestoreVerificationOptions>, RestoreVerificationOptionsValidator>();
    builder.Services.AddOptions<RestoreVerificationOptions>()
        .Bind(builder.Configuration.GetSection(RestoreVerificationOptions.SectionName))
        .ValidateOnStart();
}
else
{
    builder.Services.AddOptions<BackupOptions>()
        .Bind(builder.Configuration.GetSection(BackupOptions.SectionName));
    builder.Services.AddOptions<RestoreVerificationOptions>()
        .Bind(builder.Configuration.GetSection(RestoreVerificationOptions.SectionName));
}

// Local development için explicit host binding; production host binding platform tarafından yönetilmelidir.
if (isDevelopment && !OpenApiExportMode.IsEnabled)
{
    var devListenPort = GetDevelopmentHttpListenPort(builder.Configuration);
    // Single listen: LAN and loopback share one port (from Urls / default 5184).
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(devListenPort);
    });

    Console.WriteLine($"🌐 Development host binding: 0.0.0.0:{devListenPort} (includes localhost and LAN; set Urls in appsettings.Development.json to change)");
}

// Entity Framework ve PostgreSQL bağlantısı
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    if (!OpenApiExportMode.IsEnabled)
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is not configured. Set ConnectionStrings__DefaultConnection or user secrets. See backend/CONFIGURATION.md.");
    }

    throw new InvalidOperationException(
        "OpenAPI export: DefaultConnection missing — OpenApiExportConfiguration should supply in-memory defaults.");
}

builder.Services.AddDbContext<AppDbContext>((_, options) =>
    ConfigureAppDbContextOptions(options, defaultConnection, isDevelopment),
    ServiceLifetime.Scoped,
    ServiceLifetime.Singleton);

builder.Services.AddDbContextFactory<AppDbContext>((_, options) =>
    ConfigureAppDbContextOptions(options, defaultConnection, isDevelopment));

builder.Services.AddDataProtection();

// Identity servisleri
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Şifre gereksinimleri
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    
    // Kullanıcı gereksinimleri
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var secretKey = RequireMandatoryNonEmpty(builder.Configuration, "JwtSettings:SecretKey", minLength: 32);
var jwtIssuer = RequireMandatoryNonEmpty(builder.Configuration, "JwtSettings:Issuer");
var jwtAudience = RequireMandatoryNonEmpty(builder.Configuration, "JwtSettings:Audience");
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Keep JWT short claim names (e.g. "role") on the principal; must match TokenClaimsService + RoleClaimType below.
    options.MapInboundClaims = false;
    options.RequireHttpsMetadata = !isDevelopment;
    options.SaveToken = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true,
        ValidateTokenReplay = false,
        // JWT payload uses short claim name "role"; [Authorize(Roles="...")] / User.IsInRole() require this mapping
        RoleClaimType = "role"
    };
    
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var path = context.HttpContext.Request.Path;
            if (path.StartsWithSegments("/hubs/demo-import-progress"))
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                    context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
            var userId = context.Principal?.GetActorUserId();
            var sidRaw = context.Principal?.FindFirst("sid")?.Value;
            if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(sidRaw, out var sessionId))
            {
                var refreshTokenService = context.HttpContext.RequestServices.GetRequiredService<IRefreshTokenService>();
                var isActive = await refreshTokenService.IsSessionActiveAsync(
                    userId,
                    sessionId,
                    context.HttpContext.RequestAborted).ConfigureAwait(false);
                if (!isActive)
                {
                    context.Fail("Session invalidated");
                    return;
                }
            }
            logger.LogInformation(
                "JWT token validated successfully: userId={UserId}, correlationId={CorrelationId}",
                userId,
                correlationId);
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                "JWT authentication failed: {ExceptionType}, message={Message}",
                context.Exception.GetType().Name,
                context.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            if (context.Response.HasStarted) return Task.CompletedTask;
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var message = context.AuthenticateFailure?.Message ?? "Token missing or invalid. Please sign in again.";
            var body = System.Text.Json.JsonSerializer.Serialize(new { message, code = "UNAUTHORIZED" });
            return context.Response.WriteAsync(body);
        },
        OnForbidden = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
            var userId = context.HttpContext.User.GetActorUserId() ?? "unknown";
            logger.LogWarning(
                "403 Forbidden (auth handler): correlationId={CorrelationId}, userId={UserId}, path={Path}",
                correlationId, userId, context.HttpContext.Request.Path);
            return Task.CompletedTask;
        }
    };
});

// Authorization: role-based policies (e.g. PosSales) and permission-based policies. Use AddAppAuthorization() (AuthorizationExtensions).
// New endpoints prefer [HasPermission(AppPermissions.X)].
// Admin/SuperAdmin permission set in RolePermissionMatrix.
builder.Services.AddAppAuthorization();

builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IUserSessionInvalidation>(sp => (RefreshTokenService)sp.GetRequiredService<IRefreshTokenService>());
builder.Services.AddScoped<IRolePermissionResolver, RolePermissionResolver>();
builder.Services.AddScoped<IEffectivePermissionResolver, EffectivePermissionResolver>();
builder.Services.AddScoped<IUserPermissionOverrideService, UserPermissionOverrideService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<ITokenClaimsService, TokenClaimsService>();
builder.Services.AddScoped<IJwtAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAdminTenantService, AdminTenantService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminTenantLicenseService, AdminTenantLicenseService>();
builder.Services.AddScoped<ILicenseRenewalService, LicenseRenewalService>();
builder.Services.AddScoped<IQuickUserGeneratorService, QuickUserGeneratorService>();
builder.Services.AddScoped<ITenantUserService, TenantUserService>();
builder.Services.AddSingleton<IBulkUserImportResultStore, BulkUserImportResultStore>();
builder.Services.AddSingleton<IBulkUserImportJobManager, BulkUserImportJobManager>();
builder.Services.AddSingleton<IDemoProductImportJobManager, DemoProductImportJobManager>();
builder.Services.AddSignalR();
builder.Services.AddScoped<IBulkUserImportService, BulkUserImportService>();
builder.Services.AddScoped<IDemoProductImportImageService, DemoProductImportImageService>();
builder.Services.AddScoped<IDemoProductImportService, DemoProductImportService>();
builder.Services.AddScoped<ICategoryDemoResetService, CategoryDemoResetService>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();
builder.Services.AddScoped<IWelcomeEmailService, WelcomeEmailService>();
builder.Services.AddScoped<IUsernameChangeEmailService, UsernameChangeEmailService>();
builder.Services.AddScoped<IForgotUsernameEmailService, ForgotUsernameEmailService>();
builder.Services.AddScoped<IUserUsernameHistoryService, UserUsernameHistoryService>();
builder.Services.AddScoped<IScopeCheckService, ScopeCheckService>();
// Wave 0–1 follow-through: JWT + /me tenant snapshot (claim when valid, else legacy default row).
builder.Services.AddScoped<IAuthTenantSnapshotProvider, AuthTenantSnapshotProvider>();
builder.Services.AddScoped<ILoginTenantResolver, LoginTenantResolver>();
builder.Services.AddScoped<IUserTenantMembershipProvisioner, UserTenantMembershipProvisioner>();
builder.Services.AddScoped<ISettingsTenantResolver, SettingsTenantResolver>();
builder.Services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
builder.Services.AddScoped<ITenantProvider, SubdomainTenantProvider>();
builder.Services.AddScoped<TenantLicenseValidator>();
builder.Services.AddScoped<CurrentTenantService>();

// CORS politikası
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        // Production: only explicitly configured origins.
        var configuredOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray() ?? Array.Empty<string>();

        if (isDevelopment)
        {
            policy.SetIsOriginAllowed(IsTrustedDevelopmentCorsOrigin)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
            return;
        }

        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
            return;
        }

        throw new InvalidOperationException("Cors:AllowedOrigins must be configured in non-development environments.");
    });
});

// Memory cache for QR image
builder.Services.AddMemoryCache();

// API servisleri
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // GET /api/UserManagement/{id} etc. return camelCase for frontend
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.WriteIndented = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Kasse API",
        Version = "v1",
        Description = "Registrierkasse API - RKSV uyumlu kasa sistemi"
    });
    
    c.SchemaFilter<KasseAPI_Final.Swagger.TaxTypeSchemaFilter>();
    c.SchemaFilter<KasseAPI_Final.Swagger.TagesabschlussSchemaRequiredFilter>();
    c.OperationFilter<SignatureDebugSwaggerExamples>();
    c.OperationFilter<KasseAPI_Final.Swagger.PosAdminTagsAndDeprecationFilter>();

    c.DocInclusionPredicate((_, api) => !KasseAPI_Final.Swagger.LegacySwaggerPathExclusions.ShouldExclude(api.RelativePath));
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// RKSV SignaturePipeline (Checklist 1-5)
// Use hardware TSE (fiskaly) in production; software TSE for dev/test.
if (builder.Environment.IsProduction())
{
    builder.Services.AddHttpClient<IFiskalyClient, FiskalyHttpClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<FiskalyOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });
    builder.Services.AddSingleton<ITseKeyProvider, FiskalyTseKeyProvider>();
}
else
{
    builder.Services.AddSingleton<ITseKeyProvider, SoftwareTseKeyProvider>();
}
builder.Services.AddScoped<IBelegdatenPayloadBuilder, ReceiptBelegdatenPayloadBuilder>();
builder.Services.AddScoped<SignaturePipeline>();

// Closing-flow signing: Fake (dev, no hardware) vs Real (SignaturePipeline + TSE device readiness)
var tseOpts = builder.Configuration.GetSection(TseOptions.SectionName).Get<TseOptions>() ?? new TseOptions();
if (tseOpts.IsFakeSigningMode)
    builder.Services.AddScoped<ITseProvider, FakeTseProvider>();
else
    builder.Services.AddScoped<ITseProvider, RealTseProvider>();

// Register services
builder.Services.AddScoped<ITseService, TseService>();
builder.Services.AddHttpClient<IFinanzOnlineSessionTransport, SoapFinanzOnlineSessionTransport>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FinanzOnlineSessionOptions>>().CurrentValue;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.RequestTimeoutSeconds));
});
builder.Services.AddScoped<IFinanzOnlineConnectivitySource, DbFinanzOnlineConnectivitySource>();
builder.Services.AddScoped<IFinanzOnlineCredentialProvider, OptionsFinanzOnlineCredentialProvider>();
builder.Services.AddScoped<CachedFinanzOnlineSessionClient>();
builder.Services.AddScoped<SimulatedFinanzOnlineSessionClient>();
builder.Services.AddScoped<IFinanzOnlineSessionClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FinanzOnlineSessionOptions>>().CurrentValue;
    return options.UseSimulation
        ? sp.GetRequiredService<SimulatedFinanzOnlineSessionClient>()
        : sp.GetRequiredService<CachedFinanzOnlineSessionClient>();
});
builder.Services.AddHttpClient<IFinanzOnlineRegistrierkassenTransport, SoapFinanzOnlineRegistrierkassenTransport>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FinanzOnlineRegistrierkassenOptions>>().CurrentValue;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.RequestTimeoutSeconds));
});
builder.Services.AddScoped<SimulatedFinanzOnlineRegistrierkassenClient>();
builder.Services.AddScoped<TestModeFinanzOnlineRegistrierkassenClient>();
builder.Services.AddScoped<IFinanzOnlineRegistrierkassenClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FinanzOnlineRegistrierkassenOptions>>().CurrentValue;
    return options.UseSimulation
        ? sp.GetRequiredService<SimulatedFinanzOnlineRegistrierkassenClient>()
        : sp.GetRequiredService<TestModeFinanzOnlineRegistrierkassenClient>();
});
builder.Services.AddHttpClient<IFinanzOnlineTransmissionQueryTransport, SoapFinanzOnlineTransmissionQueryTransport>((sp, client) =>
{
    var q = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FinanzOnlineTransmissionQueryOptions>>().CurrentValue;
    var rk = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FinanzOnlineRegistrierkassenOptions>>().CurrentValue;
    var seconds = Math.Max(5, Math.Max(q.RequestTimeoutSeconds, rk.RequestTimeoutSeconds));
    client.Timeout = TimeSpan.FromSeconds(seconds);
});
builder.Services.AddScoped<SimulatedFinanzOnlineTransmissionQueryClient>();
builder.Services.AddScoped<TestModeFinanzOnlineTransmissionQueryClient>();
builder.Services.AddScoped<IFinanzOnlineTransmissionQueryClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FinanzOnlineTransmissionQueryOptions>>().CurrentValue;
    return options.UseSimulation
        ? sp.GetRequiredService<SimulatedFinanzOnlineTransmissionQueryClient>()
        : sp.GetRequiredService<TestModeFinanzOnlineTransmissionQueryClient>();
});
builder.Services.AddScoped<IFinanzOnlineCommandMapper, DefaultFinanzOnlineCommandMapper>();
builder.Services.AddScoped<IFinanzOnlineSubmissionService, FinanzOnlineSubmissionService>();
builder.Services.AddScoped<IFinanzOnlineOutboxService, FinanzOnlineOutboxService>();
builder.Services.AddScoped<RksvSpecialReceiptFinanzOnlineOutboxHandler>();
builder.Services.AddScoped<FakeRksvFinanzOnlineSubmissionClient>();
builder.Services.AddScoped<NotImplementedRksvFinanzOnlineSubmissionClient>();
builder.Services.AddScoped<RksvFinanzOnlineSubmissionClient>();
builder.Services.AddScoped<IRksvFinanzOnlineSubmissionClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions>>().CurrentValue;
    return opts.ClientKind switch
    {
        RksvFinanzOnlineSubmissionClientKind.NotImplemented => sp.GetRequiredService<NotImplementedRksvFinanzOnlineSubmissionClient>(),
        RksvFinanzOnlineSubmissionClientKind.Real => sp.GetRequiredService<RksvFinanzOnlineSubmissionClient>(),
        _ => sp.GetRequiredService<FakeRksvFinanzOnlineSubmissionClient>(),
    };
});
builder.Services.AddScoped<IFinanzOnlineService, FinanzOnlineService>();
builder.Services.AddScoped<IFinanzOnlineAdminConnectivityService, FinanzOnlineAdminConnectivityService>();
builder.Services.AddScoped<ITagesabschlussService, TagesabschlussService>();
builder.Services.AddScoped<IDailyClosingService, DailyClosingService>();
builder.Services.AddScoped<IOperationalReportingService, OperationalReportingService>();
builder.Services.AddScoped<IComplianceOperationalReportingService, ComplianceOperationalReportingService>();
builder.Services.AddScoped<IPeakHoursAnalysisService, PeakHoursAnalysisService>();
builder.Services.AddScoped<IProductMovementAnalysisService, ProductMovementAnalysisService>();
builder.Services.AddScoped<IAdminOperationalReportExportService, AdminOperationalReportExportService>();
builder.Services.AddScoped<IOperationalReportScheduler, OperationalReportScheduler>();
builder.Services.AddHostedService<OperationalReportSchedulerHostedService>();
builder.Services.AddScoped<ITagesberichtService, TagesberichtService>();
builder.Services.AddScoped<IMonatsberichtService, MonatsberichtService>();
builder.Services.AddScoped<IJahresberichtService, JahresberichtService>();
builder.Services.AddScoped<IReportSubmissionCompatibilityService, ReportSubmissionCompatibilityService>();
builder.Services.AddScoped<IReportHistoryService, ReportHistoryService>();
builder.Services.AddScoped<IUserService, UserService>(); // Kullanıcı servisi eklendi
builder.Services.AddScoped<IReceiptService, ReceiptService>();
// Watermarked receipt reprint PDF (no new TSE signing); routes: GET api/admin/payments/{id}/reprint-pdf and .../reprint.
builder.Services.AddScoped<IReceiptPdfService, ReceiptPdfService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<IInvoiceEmailService, InvoiceEmailService>();
// builder.Services.AddScoped<IPrinterService, PrinterService>(); // Geçici olarak devre dışı - ReceiptService bağımlılığı nedeniyle
// builder.Services.AddScoped<ITestService, TestService>(); // Geçici olarak devre dışı - ReceiptService bağımlılığı nedeniyle
builder.Services.AddScoped<IProductModifierValidationService, NoOpProductModifierValidationService>();
builder.Services.AddScoped<IReceiptSequenceService, ReceiptSequenceService>();
builder.Services.AddScoped<ICashRegisterResolutionService, CashRegisterResolutionService>();
builder.Services.AddScoped<ICashRegisterShiftService, CashRegisterShiftService>();
builder.Services.AddScoped<ICashRegisterDecommissionService, CashRegisterDecommissionService>();
builder.Services.AddScoped<ICashRegisterManagementService, CashRegisterManagementService>();
builder.Services.AddScoped<ICashRegisterHealthService, CashRegisterHealthService>();
builder.Services.AddScoped<ICashRegisterListEnrichmentService, CashRegisterListEnrichmentService>();
builder.Services.AddScoped<IPosCashRegisterReadinessService, PosCashRegisterReadinessService>();
builder.Services.AddScoped<IPosStatusService, PosStatusService>();
builder.Services.AddScoped<IPosShiftService, PosShiftService>();
builder.Services.AddScoped<IPosDailyClosingService, PosDailyClosingService>();
builder.Services.AddScoped<IPosCartTableOpsService, PosCartTableOpsService>();
builder.Services.AddScoped<IPosSplitSessionService, PosSplitSessionService>();
builder.Services.AddScoped<IPosCustomerQrLookupService, PosCustomerQrLookupService>();
builder.Services.AddScoped<IPaymentHistoryService, PaymentHistoryService>();
builder.Services.AddScoped<IDailyClosingReportService, DailyClosingReportService>();
builder.Services.AddScoped<IAdminShiftOverviewService, AdminShiftOverviewService>();
builder.Services.AddScoped<IPaymentMethodCatalogService, PaymentMethodCatalogService>();
builder.Services.AddScoped<IPricingRuleResolver, PricingRuleResolver>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<IAdminVoucherService, AdminVoucherService>();
builder.Services.AddScoped<IVoucherIssuanceService, VoucherIssuanceService>();
builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection(PaymentGatewayOptions.SectionName));
builder.Services.AddStripePaymentGateway();
// Payment Gateway: Mock in Development, Stripe in non-Development hosts.
if (isDevelopment)
{
    builder.Services.AddSingleton<IPaymentGateway, MockCardGateway>();
}
else
{
    builder.Services.AddSingleton<IPaymentGateway, StripeCardGateway>();
}
builder.Services.AddScoped<ICardPaymentService, CardPaymentService>();
builder.Services.AddScoped<IAdminCardTransactionListService, AdminCardTransactionListService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IAdminPaymentListService, AdminPaymentListService>();
builder.Services.AddScoped<IAdminSuspiciousAlertService, AdminSuspiciousAlertService>();
builder.Services.AddScoped<IPaymentTrendAnalysisService, PaymentTrendAnalysisService>();
builder.Services.AddScoped<IAdminProductListService, AdminProductListService>();
builder.Services.AddScoped<IRksvSpecialReceiptFinanzOnlineSubmissionTracker, RksvSpecialReceiptFinanzOnlineSubmissionTracker>();
builder.Services.AddScoped<IRksvSpecialReceiptService, RksvSpecialReceiptService>();
builder.Services.AddScoped<IRksvStartbelegPolicy, RksvStartbelegPolicy>();
builder.Services.AddScoped<IRksvMonatsbelegPolicy, RksvMonatsbelegPolicy>();
builder.Services.AddScoped<IMonatsbelegReminderService, MonatsbelegReminderService>();
builder.Services.AddScoped<IRksvReminderService, RksvReminderService>();
builder.Services.AddSingleton<IRksvReceiptQrPayloadFormatValidator, RksvReceiptQrPayloadFormatValidator>();
builder.Services.AddScoped<IRksvComplianceReportService, RksvComplianceReportService>();
builder.Services.AddScoped<IRksvEvidenceBundleService, RksvEvidenceBundleService>();
builder.Services.AddScoped<IQrImageService, QrImageService>();
builder.Services.AddScoped<TableOrderService>(); // Masa siparişleri persistence servisi
builder.Services.AddScoped<LegacyRouteDeprecationFilter>();

// Register repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IGenericRepository<Customer>, GenericRepository<Customer>>();
builder.Services.AddScoped<IGenericRepository<Product>, TenantScopedProductRepository>();
builder.Services.AddScoped<IGenericRepository<Category>, GenericRepository<Category>>();
builder.Services.AddScoped<IGenericRepository<Invoice>, GenericRepository<Invoice>>();
builder.Services.AddScoped<IGenericRepository<PaymentDetails>, GenericRepository<PaymentDetails>>();

// Core metrics (Prometheus) for Grafana dashboards
builder.Services.AddSingleton<ICoreMetrics, CoreMetrics>();

// FinanzOnline retry job + metrics + alert sink
builder.Services.Configure<KasseAPI_Final.Configuration.FinanzOnlineRetryJobOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.FinanzOnlineRetryJobOptions.SectionName));
builder.Services.AddSingleton<IFinanzOnlineMetrics, FinanzOnlineMetrics>();
builder.Services.AddSingleton<IFinanzOnlineAlertSink, NoOpFinanzOnlineAlertSink>();
builder.Services.AddHostedService<FinanzOnlineRetryHostedService>();
builder.Services.AddHostedService<FinanzOnlineOutboxHostedService>();
builder.Services.AddHostedService<NtpTimeSyncService>();

// 🚀 Akıllı Sepet Yaşam Döngüsü Service'i
builder.Services.AddHostedService<CartLifecycleService>();
// CartLifecycleService'i ayrıca Scoped olarak da kayıt et (logout için)
builder.Services.AddScoped<CartLifecycleService>();

// Audit log service
builder.Services.AddScoped<ITenantSessionPolicyService, TenantSessionPolicyService>();
builder.Services.AddScoped<IUserSessionService, UserSessionService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IUserActivityReportService, UserActivityReportService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuditExportService, AuditExportService>();
builder.Services.AddSingleton<IAuditExportJobManager, AuditExportJobManager>();
builder.Services.AddScoped<IAuditReportScheduler, AuditReportScheduler>();
builder.Services.AddScoped<IAuditReportEmailService, AuditReportEmailService>();
builder.Services.AddHostedService<AuditReportSchedulerHostedService>();
builder.Services.AddScoped<IFiscalExportAuditLogReader, FiscalExportAuditLogReader>();
builder.Services.AddScoped<IPosCriticalActionAuditService, PosCriticalActionAuditService>();

// Phase 1: backup orchestration (execution off HTTP thread; see BackupOrchestratorHostedService)
builder.Services.AddSingleton<IBackupChecksumService, BackupChecksumService>();
builder.Services.AddSingleton<IBackupManifestService, BackupManifestService>();
// External archive: filesystem copy + post-copy SHA-256 only. WORM/object-lock is not API-enforced; see BackupExternalArchiveBackendDescriptors / admin readiness DTOs.
builder.Services.AddSingleton<IBackupArtifactExternalArchive, FilesystemBackupArtifactExternalArchive>();
builder.Services.AddSingleton<FakeBackupExecutionAdapter>();
builder.Services.AddSingleton<PostgreSqlBackupExecutionAdapterStub>();
builder.Services.AddSingleton<IPgDumpProcessRunner, PgDumpProcessRunner>();
builder.Services.AddSingleton<PostgreSqlPgDumpBackupExecutionAdapter>();
builder.Services.Configure<OperationalDrAlertOptions>(
    builder.Configuration.GetSection(OperationalDrAlertOptions.SectionName));
builder.Services.Configure<OperationalDrObservabilityOptions>(
    builder.Configuration.GetSection(OperationalDrObservabilityOptions.SectionName));
builder.Services
    .AddHttpClient(WebhookBackupAlertPublisher.HttpClientName)
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddSingleton<LoggingBackupAlertPublisher>();
builder.Services.AddSingleton<WebhookBackupAlertPublisher>();
builder.Services.AddSingleton<IBackupAlertPublisher>(sp =>
    new CompositeBackupAlertPublisher(
        new IBackupAlertPublisher[]
        {
            sp.GetRequiredService<LoggingBackupAlertPublisher>(),
            sp.GetRequiredService<WebhookBackupAlertPublisher>(),
            sp.GetRequiredService<ActivityBackupAlertPublisher>(),
        }));
builder.Services.AddSingleton<IDrOperationalObservabilityMetrics, PrometheusDrOperationalObservabilityMetrics>();
builder.Services.AddSingleton<DrStaleRunRecoveryAlertingObserver>();
builder.Services.AddSingleton<IDrStaleRunRecoveryObserver>(sp =>
    sp.GetRequiredService<DrStaleRunRecoveryAlertingObserver>());
builder.Services.AddHostedService<DrOperationalObservabilityHostedService>();
builder.Services.AddScoped<IBackupManualTriggerService, BackupManualTriggerService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBackupRunQueryService, BackupRunQueryService>();
builder.Services.AddScoped<IBackupRunService, BackupRunService>();
builder.Services.AddScoped<IPitrService, PitrService>();
builder.Services.AddScoped<IBackupArtifactDownloadService, BackupArtifactDownloadService>();
builder.Services.AddScoped<IBackupRecoverabilitySummaryService, BackupRecoverabilitySummaryService>();
builder.Services.AddScoped<IBackupDashboardStatsService, BackupDashboardStatsService>();
builder.Services.AddScoped<IBackupVerificationService, BackupVerificationService>();
builder.Services.AddScoped<IBackupVerificationReportService, BackupVerificationReportService>();
builder.Services.AddSingleton<IRestoreOrchestrationBoundary, DeferredRestoreOrchestrationBoundary>();
builder.Services.AddSingleton<IBackupPostgresClientToolingProbeState, BackupPostgresClientToolingProbeState>();
builder.Services.AddSingleton<IBackupOperationalReadiness, BackupOperationalReadinessService>();
builder.Services.AddSingleton<IBackupScheduledEnqueueService, BackupScheduledEnqueueService>();
builder.Services.AddSingleton<IBackupOrchestratorMetrics, PrometheusBackupOrchestratorMetrics>();
builder.Services.AddSingleton<IBackupOrchestratorDistributedLock, BackupOrchestratorPostgreSqlAdvisoryLock>();
builder.Services.AddScoped<BackupPostSuccessOrchestrationHook>();
builder.Services.AddScoped<IBackupPostSuccessOrchestrationHook, BackupPostSuccessActivityHook>();
builder.Services.AddScoped<IBackupSettingsAdminService, BackupSettingsAdminService>();
builder.Services.AddHostedService<BackupOrchestratorHostedService>();
builder.Services.AddHostedService<BackupSchedulerService>();
builder.Services.AddHostedService<BackupRetentionPolicyDiagnosticsHostedService>();

builder.Services.AddSingleton<IPgRestoreListInspector, PgRestoreListInspector>();
builder.Services.AddSingleton<IPgRestoreIsolatedRestoreRunner, PgRestoreIsolatedRestoreRunner>();
builder.Services.AddSingleton<IPostRestoreDrillSqlChecker, PostRestoreDrillSqlChecker>();
builder.Services.AddSingleton<IRestoredDatabaseApplicationSmokeRunner, RestoredDatabaseApplicationSmokeRunner>();
builder.Services.AddSingleton<IFiscalGoLiveValidationRunner, FiscalGoLiveValidationRunner>();
builder.Services.AddHttpClient(ApplicationRecoverySmokeProbe.HttpClientName, c =>
{
    c.Timeout = TimeSpan.FromMinutes(3);
});
builder.Services.AddSingleton<IApplicationRecoverySmokeProbe, ApplicationRecoverySmokeProbe>();
builder.Services.AddSingleton<IExternalDependencyRecoveryEvidenceBuilder, ExternalDependencyRecoveryEvidenceBuilder>();
builder.Services.AddSingleton<IRestoreVerificationOrchestratorMetrics, PrometheusRestoreVerificationOrchestratorMetrics>();
builder.Services.AddSingleton<IRestoreVerificationOrchestratorDistributedLock, RestoreVerificationOrchestratorPostgreSqlAdvisoryLock>();
builder.Services.AddSingleton<IRestoreVerificationOperationalReadiness, RestoreVerificationOperationalReadinessService>();
builder.Services.AddScoped<IRestoreVerificationManualTriggerService, RestoreVerificationManualTriggerService>();
builder.Services.Configure<ManualRestoreApprovalOptions>(
    builder.Configuration.GetSection(ManualRestoreApprovalOptions.SectionName));
builder.Services.AddSingleton<ManualRestoreTargetDatabaseGuard>();
builder.Services.AddScoped<IManualRestoreApprovalEmailService, ManualRestoreApprovalEmailService>();
builder.Services.AddScoped<IManualRestoreApprovalNotificationService, ManualRestoreApprovalNotificationService>();
builder.Services.AddScoped<IManualRestoreTriggerService, ManualRestoreTriggerService>();
builder.Services.Configure<PaymentReversalApprovalOptions>(
    builder.Configuration.GetSection(PaymentReversalApprovalOptions.SectionName));
builder.Services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
builder.Services.AddScoped<IPaymentReversalApprovalEmailService, PaymentReversalApprovalEmailService>();
builder.Services.AddScoped<IPaymentReversalApprovalService, PaymentReversalApprovalService>();
builder.Services.AddScoped<IValidationRestoreExecutionService, ValidationRestoreExecutionService>();
        builder.Services.AddScoped<IRestoreVerificationSchedulingQueryService, RestoreVerificationSchedulingQueryService>();
        builder.Services.AddScoped<IRestoreVerificationRunQueryService, RestoreVerificationRunQueryService>();
        builder.Services.AddScoped<IRestoreProofMilestonesQueryService, RestoreProofMilestonesQueryService>();
builder.Services.AddHostedService<RestoreVerificationOrchestratorHostedService>();
builder.Services.AddHostedService<StaleRunReaperHostedService>();
builder.Services.Configure<KasseAPI_Final.Configuration.OfflineReplayOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.OfflineReplayOptions.SectionName));
builder.Services.AddScoped<IOfflineTransactionService, OfflineTransactionService>();
builder.Services.AddSingleton<TseHealthStateStore>();
builder.Services.AddSingleton<ITseHealthMonitor>(sp => sp.GetRequiredService<TseHealthStateStore>());
builder.Services.AddSingleton<IOfflineReplayCompletionNotifier, LoggingOfflineReplayCompletionNotifier>();
builder.Services.AddHostedService<TseHealthCheckService>();
builder.Services.AddHostedService<OfflineReplayHostedService>();
builder.Services.Configure<KasseAPI_Final.Configuration.PayloadHashGuardOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.PayloadHashGuardOptions.SectionName));
builder.Services.Configure<KasseAPI_Final.Configuration.CoverageGuardOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.CoverageGuardOptions.SectionName));
builder.Services.Configure<KasseAPI_Final.Configuration.PayloadHashRepairJobOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.PayloadHashRepairJobOptions.SectionName));
builder.Services.AddScoped<IOfflinePayloadHashMaintenanceService, OfflinePayloadHashMaintenanceService>();
builder.Services.AddHostedService<KasseAPI_Final.Services.PayloadHashGuardStartupCheck>();
builder.Services.AddHostedService<KasseAPI_Final.Services.PayloadHashRepairHostedService>();
builder.Services.AddScoped<ILegalHoldService, LegalHoldService>();
builder.Services.AddScoped<IIntegrityCheckService, IntegrityCheckService>();
builder.Services.Configure<KasseAPI_Final.Configuration.FiscalExportOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.FiscalExportOptions.SectionName));
builder.Services.AddScoped<KasseAPI_Final.Filters.RequireDisclaimerAcknowledgmentFilter>();
builder.Services.AddSingleton<IFiscalExportDownloadTicketStore, FiscalExportDownloadTicketStore>();
builder.Services.AddSingleton<IDisclaimerService, DisclaimerService>();
builder.Services.AddScoped<IFiscalExportService, FiscalExportService>();
builder.Services.AddScoped<IRksvDepExportService, RksvDepExportService>();
builder.Services.AddScoped<IRksvSignatureVerifyService, RksvSignatureVerifyService>();
builder.Services.AddScoped<IDepExportHistoryService, DepExportHistoryService>();
builder.Services.AddScoped<IDepExportScheduler, DepExportScheduler>();
builder.Services.AddHostedService<DepExportSchedulerHostedService>();
builder.Services.AddScoped<ILegalExportCompletenessService, LegalExportCompletenessService>();
builder.Services.AddScoped<IActorDisplayNameResolver, ActorDisplayNameResolver>();
builder.Services.AddScoped<IUserUniquenessValidationService, UserUniquenessValidationService>();
builder.Services.AddScoped<IUserCreationService, UserCreationService>();

// HttpContext accessor for audit logging
builder.Services.AddHttpContextAccessor();

        var app = builder.Build();

        // Swashbuckle CLI calls SwaggerHostFactory.CreateHost() and never runs RunAsync; without mapped endpoints
        // the API explorer yields an empty document. Only the tooling path registers the minimal endpoint graph.
        if (OpenApiExportMode.IsEnabled)
        {
            app.MapControllers();
            app.MapMetrics();
            app.MapGet("/", () => "Kasse API is running!");
            app.MapGet("/health", () => "OK");
            app.MapGet("/api/health", () => "OK");
        }

        return app;
    }

    private static string RequireMandatoryNonEmpty(IConfiguration configuration, string key, int minLength = 1)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minLength)
        {
            var envKey = key.Replace(":", "__", StringComparison.Ordinal);
            throw new InvalidOperationException(
                $"Missing or invalid required configuration: {key}. Set user secrets or environment variable {envKey}. See backend/CONFIGURATION.md.");
        }

        return value.Trim();
    }

    public static async Task RunAsync(WebApplication app)
    {
FinanzOnlineTransportStartupDiagnostics.LogTransportModesAtStartup(app.Services);

// Diagnostic: log which database the API actually uses (runtime resolution; env vars can override appsettings)
var connStr = app.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connStr))
{
    var masked = ConnectionStringMasking.Mask(connStr);
    Console.WriteLine($"[DbDiagnostic] Resolved DefaultConnection (masked): {masked}");
}
else
{
    Console.WriteLine("[DbDiagnostic] DefaultConnection is null or empty.");
}

// Startup bootstrap: migration, migration gate, roles, users, demo data, product seed, guest customer
if (!OpenApiExportMode.IsEnabled)
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            await StartupBootstrapRunner.RunAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal startup bootstrap error: {ex.Message}");
            throw;
        }
    }
}

// Port ayarını sadece development ortamında zorla.
if (app.Environment.IsDevelopment())
{
    var devListenPort = GetDevelopmentHttpListenPort(app.Configuration);
    app.Urls.Clear();
    app.Urls.Add($"http://localhost:{devListenPort}");
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kasse API v1");
        c.RoutePrefix = string.Empty; // Swagger UI'ı root'ta göster
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    app.UseHsts();
    app.UseHttpsRedirection();

    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        await next();
    });
}
app.UseCors("AllowAll");

// CorrelationId: propagate from request (X-Correlation-Id) or generate; required for audit traceability
app.UseMiddleware<KasseAPI_Final.Middleware.CorrelationIdMiddleware>();

// Subdomain → tenant slug → Guid on ICurrentTenantAccessor (before auth; JWT may override later).
app.UseMiddleware<KasseAPI_Final.Middleware.TenantResolutionMiddleware>();
// TenantValidationMiddleware is registered after TenantContextMiddleware (below) so JWT tenant_id
// and Development X-Tenant-Id overrides are applied before fail-closed validation runs.

// Public GET for product images saved by admin upload (anonymous; unguessable file names).
var productMediaOpts = app.Services.GetRequiredService<IOptions<ProductMediaOptions>>().Value;
var webHostEnv = app.Services.GetRequiredService<IWebHostEnvironment>();
var productMediaRoot = Path.Combine(webHostEnv.ContentRootPath, productMediaOpts.RootRelativeDirectory);
Directory.CreateDirectory(productMediaRoot);
var productMediaRequestPath = productMediaOpts.PublicUrlPathPrefix.TrimEnd('/');
var normalizedMediaRequestPath = productMediaRequestPath.StartsWith('/')
    ? productMediaRequestPath
    : "/" + productMediaRequestPath;
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(productMediaRoot),
    RequestPath = normalizedMediaRequestPath,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=86400";
    }
});

// Authentication ve Authorization middleware
app.UseAuthentication();
app.UseMiddleware<KasseAPI_Final.Middleware.TenantContextMiddleware>();
// Fail-closed: tenant-scoped API paths return 404 when ICurrentTenantAccessor.TenantId is unset.
app.UseMiddleware<KasseAPI_Final.Middleware.TenantValidationMiddleware>();
app.UseMiddleware<KasseAPI_Final.Middleware.SessionActivityMiddleware>();
app.UseMiddleware<KasseAPI_Final.Middleware.TenantOperationalGateMiddleware>();
app.UseMiddleware<KasseAPI_Final.Middleware.LicenseMiddleware>();
app.UseAuthorization();
app.UseMiddleware<KasseAPI_Final.Middleware.MustChangePasswordMiddleware>();

// Payment Security Middleware
app.UseMiddleware<KasseAPI_Final.Middleware.PaymentSecurityMiddleware>();

if (OpenApiExportMode.IsEnabled)
{
    // CreateWebApplication (OpenAPI / integration-test host) already mapped controllers, metrics, and liveness probes.
    app.MapHub<DemoImportProgressHub>("/hubs/demo-import-progress");
}
else
{
    app.MapControllers();
    app.MapHub<DemoImportProgressHub>("/hubs/demo-import-progress");

    // Prometheus /metrics endpoint for scraping (Grafana dashboards)
    app.MapMetrics();

    // Test endpoint
    app.MapGet("/", () => "Kasse API is running!");
    app.MapGet("/health", () => "OK");
    // Alias for scripts/docs that probe /api/health (same liveness as /health).
    app.MapGet("/api/health", () => "OK");
}

app.MapGet("/health/finanzonline", (IServiceProvider services) =>
    Results.Json(FinanzOnlineReadinessEvaluator.EvaluateFromRootServices(services)))
    .AllowAnonymous();
app.MapGet("/api/health/license", (ILicenseService lic, ILicenseReminderNotificationStore licenseReminders) =>
{
    var status = lic.GetStatus();
    var headerStatus = LicenseMiddleware.ResolveLicenseHeaderStatus(status, lic.IsLicenseSnapshotInitialized);
    var reminders = licenseReminders.GetReminders();
    return Results.Json(new
    {
        headerStatus,
        isValid = status.IsValid,
        isTrial = status.IsTrial,
        isExpired = status.IsExpired,
        daysRemaining = status.DaysRemaining,
        expiryDate = status.ExpiryDate,
        machineHash = status.MachineHash,
        reminders,
    });
}).AllowAnonymous();
app.MapGet("/health/auth-schema", async (AppDbContext db) =>
{
    static async Task<bool> TableExistsAsync(AppDbContext context, string tableName)
    {
        var conn = context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select to_regclass(@t) is not null";
        var p = cmd.CreateParameter();
        p.ParameterName = "@t";
        p.Value = tableName;
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync();
        return result is bool b && b;
    }

    var hasAuthSessions = await TableExistsAsync(db, "public.auth_sessions");
    var hasRefreshTokens = await TableExistsAsync(db, "public.refresh_tokens");
    if (!hasAuthSessions || !hasRefreshTokens)
    {
        return Results.Problem(
            title: "Auth schema not ready",
            detail: $"auth_sessions={hasAuthSessions}, refresh_tokens={hasRefreshTokens}",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "ok",
        authSessions = hasAuthSessions,
        refreshTokens = hasRefreshTokens
    });
});

var devPortForBanner = GetDevelopmentHttpListenPort(app.Configuration);
Console.WriteLine("=== KASSE API STARTED ===");
Console.WriteLine($"=== LOCALHOST: http://localhost:{devPortForBanner} ===");
Console.WriteLine($"=== NETWORK: http://0.0.0.0:{devPortForBanner} ===");
Console.WriteLine($"=== LOCAL IP: http://192.168.1.2:{devPortForBanner} ===");
Console.WriteLine($"=== SWAGGER: http://localhost:{devPortForBanner}/ ===");

BackupStartupDiagnostics.LogAtStartup(app.Services);

        await app.RunAsync();
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
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
using Npgsql;
using KasseAPI_Final.Hubs;
using KasseAPI_Final.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
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
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataExport;
using KasseAPI_Final.Services.DataRetention;
using KasseAPI_Final.Services.App;
using KasseAPI_Final.Services.DigitalServices;
using KasseAPI_Final.Services.Website;
using KasseAPI_Final.Sites;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Services.OperationalRuns;
using KasseAPI_Final.Services.AdminProducts;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.Loyalty;
using KasseAPI_Final.Services.LicenseTest;
using KasseAPI_Final.Services.Hosted;
using KasseAPI_Final.Services.Cache;
using KasseAPI_Final.Services.Security;
using KasseAPI_Final.Services.Metrics;
using StackExchange.Redis;
using IAdminTenantLicenseKeyService = KasseAPI_Final.Services.AdminTenants.ITenantLicenseService;
using AdminTenantLicenseKeyService = KasseAPI_Final.Services.AdminTenants.TenantLicenseService;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;
using BillingTenantLicenseService = KasseAPI_Final.Services.Billing.TenantLicenseService;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Sockets;
using QuestPDF.Infrastructure;
using ElmahCore.Mvc;
using ElmahCore.Postgresql;

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

    /// <summary>
    /// Production Npgsql pool defaults. Explicit connection-string values win; missing keys get safe defaults.
    /// (Npgsql pools by default; this sets Min/Max/Lifetime for predictable production capacity.)
    /// </summary>
    private static string ApplyProductionNpgsqlPooling(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = true
        };

        // Apply only when the operator did not set the key (keyword absent from the raw string).
        if (!ContainsConnectionKeyword(connectionString, "Minimum Pool Size")
            && !ContainsConnectionKeyword(connectionString, "MinPoolSize"))
            builder.MinPoolSize = 5;

        if (!ContainsConnectionKeyword(connectionString, "Maximum Pool Size")
            && !ContainsConnectionKeyword(connectionString, "MaxPoolSize"))
            builder.MaxPoolSize = 20;

        if (!ContainsConnectionKeyword(connectionString, "Connection Lifetime")
            && !ContainsConnectionKeyword(connectionString, "ConnectionLifetime"))
            builder.ConnectionLifetime = 300;

        return builder.ConnectionString;
    }

    private static bool ContainsConnectionKeyword(string connectionString, string keyword) =>
        connectionString.Contains(keyword + "=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains(keyword + " =", StringComparison.OrdinalIgnoreCase);

    private static void ConfigureAppDbContextOptions(
        DbContextOptionsBuilder options,
        string connectionString,
        bool isDevelopment,
        DbQueryDurationInterceptor? dbQueryDurationInterceptor = null,
        DbConnectionMetricsInterceptor? dbConnectionMetricsInterceptor = null)
    {
        void AddDbMetricsInterceptors()
        {
            if (dbQueryDurationInterceptor is not null)
                options.AddInterceptors(dbQueryDurationInterceptor);
            if (dbConnectionMetricsInterceptor is not null)
                options.AddInterceptors(dbConnectionMetricsInterceptor);
        }

        var inMemoryDatabaseName = Environment.GetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(inMemoryDatabaseName))
        {
            options.UseInMemoryDatabase(inMemoryDatabaseName);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            AddDbMetricsInterceptors();
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
        AddDbMetricsInterceptors();
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
builder.Services.Configure<WebsiteGeneratorOptions>(builder.Configuration.GetSection(WebsiteGeneratorOptions.SectionName));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(MonitoringOptions.SectionName));
builder.Services.Configure<CsrfOptions>(builder.Configuration.GetSection(CsrfOptions.SectionName));
// CSRF double-submit tokens (cache-backed; IMemoryCache is singleton — Scoped service is fine).
builder.Services.AddScoped<ICsrfTokenService, CsrfTokenService>();
builder.Services.AddScoped<WebsiteGeneratorService>();
builder.Services.AddScoped<IWebsiteGeneratorService>(sp => sp.GetRequiredService<WebsiteGeneratorService>());
builder.Services.AddScoped<ITenantWebsiteGenerator, TenantWebsiteGenerator>();
builder.Services.AddHttpClient(nameof(TenantWebsiteGenerator));
builder.Services.AddScoped<ITenantCustomizationService, TenantCustomizationService>();
builder.Services.AddScoped<AppGeneratorService>();
builder.Services.AddScoped<IAppGeneratorService>(sp => sp.GetRequiredService<AppGeneratorService>());
builder.Services.AddScoped<ITenantAppGenerator, TenantAppGenerator>();
builder.Services.AddScoped<IPublicTenantCatalogService, PublicTenantCatalogService>();
builder.Services.AddScoped<ITenantWebsiteService, TenantWebsiteService>();
builder.Services.AddScoped<ITenantDomainService, TenantDomainService>();
builder.Services.AddSingleton<IDigitalServicePricingService, DigitalServicePricingService>();
builder.Services.AddScoped<ITenantServiceStatusService, TenantServiceStatusService>();
builder.Services.AddScoped<IDigitalServiceRequestService, DigitalServiceRequestService>();
builder.Services.AddScoped<ProductImageThumbnailService>();
builder.Services.AddScoped<ICashRegisterSettingsService, CashRegisterSettingsService>();
builder.Services.Configure<CashRegisterComplianceOptions>(
    builder.Configuration.GetSection(CashRegisterComplianceOptions.SectionName));
builder.Services.Configure<TenantDeletionOptions>(
    builder.Configuration.GetSection(TenantDeletionOptions.SectionName));
builder.Services.Configure<InventoryOptions>(builder.Configuration.GetSection(InventoryOptions.SectionName));
builder.Services.Configure<TseOptions>(builder.Configuration.GetSection(TseOptions.SectionName));
builder.Services.Configure<RksvOptions>(builder.Configuration.GetSection(RksvOptions.SectionName));
builder.Services.AddScoped<IRksvEnvironmentService, RksvEnvironmentService>();
builder.Services.Configure<FiskalyOptions>(builder.Configuration.GetSection(FiskalyOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<TwoFactorAuthOptions>(builder.Configuration.GetSection(TwoFactorAuthOptions.SectionName));
builder.Services.Configure<AccountLockoutOptions>(builder.Configuration.GetSection(AccountLockoutOptions.SectionName));
builder.Services.AddSingleton<IAccountLockoutService, AccountLockoutService>();
builder.Services.AddSingleton<KasseAPI_Final.Services.Token.ITokenBlacklistService, KasseAPI_Final.Services.Token.TokenBlacklistService>();
builder.Services.AddSingleton<ITwoFactorChallengeService, TwoFactorChallengeService>();
builder.Services.AddScoped<KasseAPI_Final.Services.TwoFactor.ITwoFactorService, KasseAPI_Final.Services.TwoFactor.TwoFactorService>();
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
builder.Services.Configure<ElmahOptions>(builder.Configuration.GetSection(ElmahOptions.SectionName));
builder.Services.AddHealthChecks()
    .AddCheck<FinanzOnlineHealthCheck>("finanzonline")
    .AddCheck<BackupHealthCheck>("backup")
    .AddCheck<ElmahHealthCheck>("elmah");
builder.Services.AddScoped<IElmahErrorQueryService, ElmahErrorQueryService>();

builder.Services.Configure<NtpSettings>(builder.Configuration.GetSection(NtpSettings.SectionName));
builder.Services.Configure<DevelopmentOptions>(builder.Configuration.GetSection(DevelopmentOptions.SectionName));
builder.Services.Configure<OfflineVoucherEncryptionOptions>(
    builder.Configuration.GetSection(OfflineVoucherEncryptionOptions.SectionName));
builder.Services.Configure<LicenseOptions>(builder.Configuration.GetSection(LicenseOptions.SectionName));
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection(BillingOptions.SectionName));
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
builder.Services.Configure<EmailDevCaptureOptions>(builder.Configuration.GetSection(EmailDevCaptureOptions.SectionName));
builder.Services.AddSingleton<DevEmailOutboxWriter>();
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
builder.Services.AddScoped<ILicenseReminderService, LicenseReminderService>();
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

// Production: ensure Npgsql connection pooling knobs (env CS often omits them).
if (builder.Environment.IsProduction())
    defaultConnection = ApplyProductionNpgsqlPooling(defaultConnection);

// AppDbContext depends on scoped ICurrentTenantAccessor. Keep context + options + factory
// all Scoped so bootstrap (CreateScope) and request paths can resolve them; a Singleton
// factory cannot resolve ICurrentTenantAccessor from the root provider (HTTP 500).
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    ConfigureAppDbContextOptions(
        options,
        defaultConnection,
        isDevelopment,
        sp.GetRequiredService<DbQueryDurationInterceptor>(),
        sp.GetRequiredService<DbConnectionMetricsInterceptor>()),
    ServiceLifetime.Scoped,
    ServiceLifetime.Scoped);

builder.Services.AddDbContextFactory<AppDbContext>((sp, options) =>
    ConfigureAppDbContextOptions(
        options,
        defaultConnection,
        isDevelopment,
        sp.GetRequiredService<DbQueryDurationInterceptor>(),
        sp.GetRequiredService<DbConnectionMetricsInterceptor>()),
    ServiceLifetime.Scoped);

if (!isDevelopment && !OpenApiExportMode.IsEnabled)
{
    var elmahOptions = builder.Configuration.GetSection(ElmahOptions.SectionName).Get<ElmahOptions>() ?? new ElmahOptions();
    var elmahApplicationName = string.IsNullOrWhiteSpace(elmahOptions.ApplicationName)
        ? "Regkasse"
        : elmahOptions.ApplicationName.Trim();

    builder.Services.AddElmah<PgsqlErrorLog>(options =>
    {
        options.ConnectionString = defaultConnection;
        options.ApplicationName = elmahApplicationName;
        options.OnPermissionCheck = context =>
            context.User.Identity?.IsAuthenticated == true
            && context.User.HasPermissionClaim(AppPermissions.SystemCritical);
    });
    builder.Services.AddHostedService<ElmahRetentionHostedService>();
}

builder.Services.AddDataProtection();

// Identity servisleri
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Şifre gereksinimleri (enforced by RegkassePasswordValidator; options remain the source of truth)
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
.AddDefaultTokenProviders()
.AddPasswordValidator<KasseAPI_Final.Validators.RegkassePasswordValidator<ApplicationUser>>();

// Replace default Identity PasswordValidator so rules are not evaluated twice.
ReplaceDefaultPasswordValidator(builder.Services);

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
builder.Services.AddScoped<IUserRoleChangeService, UserRoleChangeService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<ITokenClaimsService, TokenClaimsService>();
builder.Services.AddScoped<IJwtAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<ITenantHardDeletePolicy, TenantHardDeletePolicy>();
builder.Services.AddScoped<ITenantDeletionService, TenantDeletionService>();
builder.Services.AddScoped<ILicenseLifecycleResolver, LicenseLifecycleResolver>();
builder.Services.Configure<KasseAPI_Final.Configuration.DataExportOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.DataExportOptions.SectionName));
builder.Services.AddScoped<IDataExportService, DataExportService>();
builder.Services.AddScoped<ITenantDataDeletionRequestService, TenantDataDeletionRequestService>();
builder.Services.AddScoped<IDataDeletionNotificationSender, DataDeletionNotificationSender>();
builder.Services.AddScoped<IDataDeletionService, DataDeletionService>();
builder.Services.AddSingleton<KasseAPI_Final.Services.DataRights.IDataRightsArtifactStore, KasseAPI_Final.Services.DataRights.DataRightsArtifactStore>();
builder.Services.AddScoped<KasseAPI_Final.Services.DataRights.ICustomerDataRightsService, KasseAPI_Final.Services.DataRights.CustomerDataRightsService>();
builder.Services.AddScoped<KasseAPI_Final.Services.DataAccess.IDataAccessNotificationService, KasseAPI_Final.Services.DataAccess.DataAccessNotificationService>();
builder.Services.AddScoped<KasseAPI_Final.Services.DataAccess.IDataAccessService, KasseAPI_Final.Services.DataAccess.DataAccessService>();
builder.Services.AddHostedService<KasseAPI_Final.Services.DataRights.DataRightsExportProcessorService>();
builder.Services.AddScoped<IRksvDataRetentionService, RksvDataRetentionService>();
builder.Services.AddScoped<ITenantDataManagementOverviewService, TenantDataManagementOverviewService>();
builder.Services.Configure<KasseAPI_Final.Configuration.RksvDataCleanupOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.RksvDataCleanupOptions.SectionName));
builder.Services.AddScoped<IRksvDataCleanupService, RksvDataCleanupService>();
builder.Services.AddHostedService<RksvDataCleanupHostedService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAdminTenantService, AdminTenantService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminTenantLicenseKeyService, AdminTenantLicenseKeyService>();
builder.Services.AddScoped<IAdminTenantLicenseService, AdminTenantLicenseService>();
builder.Services.AddSingleton<ITenantLicenseExtensionRateLimiter, TenantLicenseExtensionRateLimiter>();

// Billing services (scoped — injected AppDbContext is request-scoped; do not use Singleton)
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IBillingTenantLicenseService, BillingTenantLicenseService>();
builder.Services.AddScoped<IBillingAuditService, BillingAuditService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IBillingBackupService, BillingBackupService>();
// License invoice PDF (QuestPDF — no Chrome/Chromium dependency)
builder.Services.AddScoped<IInvoicePdfGenerator, InvoicePdfGenerator>();
// No additional registration needed for QuestPDF
builder.Services.AddScoped<ILicenseKeyGenerator, LicenseKeyGenerator>();
builder.Services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IBillingReminderService>(sp => (IBillingReminderService)sp.GetRequiredService<IReminderService>());
builder.Services.Configure<BillingBackupConfig>(builder.Configuration.GetSection("BillingBackup"));
builder.Services.AddHostedService<BillingReminderHostedService>();
builder.Services.AddHostedService<BillingBackupHostedService>();
builder.Services.AddScoped<ILicenseRenewalService, LicenseRenewalService>();
builder.Services.AddScoped<ILicenseTestService, LicenseTestService>();
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
builder.Services.AddScoped<IOnlineOrderCustomerEmailService, OnlineOrderCustomerEmailService>();
builder.Services.AddScoped<IUsernameChangeEmailService, UsernameChangeEmailService>();
builder.Services.AddScoped<ForgotUsernameEmailService>();
builder.Services.AddScoped<ForgotPasswordEmailService>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IForgotUsernameEmailService, DevCapturingForgotUsernameEmailService>();
    builder.Services.AddScoped<IForgotPasswordEmailService, DevCapturingForgotPasswordEmailService>();
}
else
{
    builder.Services.AddScoped<IForgotUsernameEmailService, ForgotUsernameEmailService>();
    builder.Services.AddScoped<IForgotPasswordEmailService, ForgotPasswordEmailService>();
}
builder.Services.AddScoped<IUserUsernameHistoryService, UserUsernameHistoryService>();
builder.Services.AddScoped<IScopeCheckService, ScopeCheckService>();
// Wave 0–1 follow-through: JWT + /me tenant snapshot (claim when valid, else legacy default row).
builder.Services.AddScoped<IAuthTenantSnapshotProvider, AuthTenantSnapshotProvider>();
builder.Services.AddScoped<ILoginTenantResolver, LoginTenantResolver>();
builder.Services.AddScoped<IUserTenantMembershipProvisioner, UserTenantMembershipProvisioner>();
builder.Services.AddScoped<ISettingsTenantResolver, SettingsTenantResolver>();
builder.Services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
builder.Services.AddScoped<ITenantProvider, SubdomainTenantProvider>();
// Request tenant resolution: JWT → dev header/query → host slug → admin fallback.
builder.Services.AddScoped<ITenantContextService, TenantContextService>();
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

// Memory cache for QR image, lockout, rate limits (process-local; not ICacheService)
builder.Services.AddMemoryCache();

// Redis connection for distributed ICacheService read-through caching
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
    var connectionString = string.IsNullOrWhiteSpace(options.ConnectionString)
        ? "localhost:6379"
        : options.ConnectionString;
    return ConnectionMultiplexer.Connect(connectionString);
});
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// API servisleri
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // GET /api/UserManagement/{id} etc. return camelCase for frontend
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.WriteIndented = true;
});

// Response compression (non-breaking): Gzip + Brotli for JSON/API payloads over HTTPS.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/json",
        "text/json",
        "application/problem+json"
    ]);
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
    c.OperationFilter<KasseAPI_Final.Swagger.SingleJsonContentTypeOperationFilter>();

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
builder.Services.AddScoped<ITseVerificationService, TseVerificationService>();
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
builder.Services.AddScoped<IMonatsbelegClosingService, MonatsbelegClosingService>();
builder.Services.AddScoped<IJahresbelegClosingService, JahresbelegClosingService>();
builder.Services.AddScoped<IMonatsbelegService, MonatsbelegService>();
builder.Services.AddScoped<IJahresbelegService, JahresbelegService>();
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
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<II18nErrorService, I18nErrorService>();
builder.Services.AddScoped<KasseAPI_Final.Services.Localization.IApiMessageLocalizer, KasseAPI_Final.Services.Localization.ApiMessageLocalizer>();
builder.Services.AddScoped<KasseAPI_Final.Services.PasswordErrorTranslator>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
// Watermarked receipt reprint PDF (no new TSE signing); routes: GET api/admin/payments/{id}/reprint-pdf and .../reprint.
builder.Services.AddScoped<IReceiptPdfService, ReceiptPdfService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
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
builder.Services.AddScoped<IMonatsbelegClosingService, MonatsbelegClosingService>();
builder.Services.AddScoped<IDailyClosingReportService, DailyClosingReportService>();
builder.Services.AddScoped<IReportPdfStorageService, ReportPdfStorageService>();
builder.Services.AddScoped<IReportPdfService, ReportPdfService>();
builder.Services.AddScoped<IReportPdfCaptureService, ReportPdfCaptureService>();
builder.Services.AddScoped<ITagesabschlussReportService, TagesabschlussReportService>();
builder.Services.AddScoped<ITagesabschlussReportEnricher, TagesabschlussReportEnricher>();
builder.Services.AddScoped<IRksvReportTextService, RksvReportTextService>();
builder.Services.AddScoped<IMonatsbelegReportService, MonatsbelegReportService>();
builder.Services.AddScoped<IJahresbelegReportService, JahresbelegReportService>();
builder.Services.AddScoped<INullbelegReportService, NullbelegReportService>();
builder.Services.AddScoped<IStartbelegReportService, StartbelegReportService>();
builder.Services.AddScoped<ISchlussbelegReportService, SchlussbelegReportService>();
builder.Services.AddScoped<IAdminShiftOverviewService, AdminShiftOverviewService>();
builder.Services.AddScoped<IAdminShiftManagementService, AdminShiftManagementService>();
builder.Services.AddScoped<IShiftAutoCloseService, ShiftAutoCloseService>();
builder.Services.Configure<ShiftAutoCloseOptions>(
    builder.Configuration.GetSection(ShiftAutoCloseOptions.SectionName));
builder.Services.Configure<TagesabschlussReminderOptions>(
    builder.Configuration.GetSection(TagesabschlussReminderOptions.SectionName));
builder.Services.AddScoped<IPaymentMethodCatalogService, PaymentMethodCatalogService>();
builder.Services.AddScoped<IPaymentMethodDefinitionBootstrapService, PaymentMethodDefinitionBootstrapService>();
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
builder.Services.AddScoped<IProductService, ProductService>();
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
builder.Services.AddScoped<IOrderIntegrationService, OrderIntegrationService>();
builder.Services.AddScoped<IOnlineOrderQueryService, OnlineOrderQueryService>();
builder.Services.AddScoped<IOnlineOrderNotificationService, OnlineOrderNotificationService>();
builder.Services.AddScoped<IOnlineOrderPushSender, LoggingOnlineOrderPushSender>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<IOnlineOrderLoyaltyService, OnlineOrderLoyaltyService>();
builder.Services.AddScoped<IOnlineOrderTrackingService, OnlineOrderTrackingService>();
builder.Services.AddScoped<IOnlineOrderStatusService, OnlineOrderStatusService>();
builder.Services.AddScoped<IOnlineOrderPaymentService, OnlineOrderPaymentService>();
builder.Services.AddScoped<IOnlineOrderIntakeService, OnlineOrderIntakeService>();
builder.Services.AddScoped<IPublicCustomerDashboardService, PublicCustomerDashboardService>();
builder.Services.AddScoped<LegacyRouteDeprecationFilter>();

// Register repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IGenericRepository<Customer>, GenericRepository<Customer>>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IGenericRepository<Product>, TenantScopedProductRepository>();
builder.Services.AddScoped<IGenericRepository<Category>, GenericRepository<Category>>();
builder.Services.AddScoped<IGenericRepository<Invoice>, GenericRepository<Invoice>>();
builder.Services.AddScoped<IGenericRepository<PaymentDetails>, GenericRepository<PaymentDetails>>();

// Core metrics (Prometheus) for Grafana dashboards
builder.Services.AddSingleton<ICoreMetrics, CoreMetrics>();
builder.Services.AddSingleton<ICacheMetricsService, CacheMetricsService>();
builder.Services.AddSingleton<IDbMetricsService, DbMetricsService>();
builder.Services.AddSingleton<IBusinessMetricsService, BusinessMetricsService>();
builder.Services.AddSingleton<ApiRequestMetricsAccumulator>();
builder.Services.AddSingleton<DbQueryDurationInterceptor>();
builder.Services.AddSingleton<DbConnectionMetricsInterceptor>();
if (!OpenApiExportMode.IsEnabled)
    builder.Services.AddHostedService<BusinessMetricsRefreshHostedService>();

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
builder.Services.AddScoped<KasseAPI_Final.Services.Session.IDeviceSessionService, KasseAPI_Final.Services.Session.DeviceSessionService>();
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
builder.Services.AddSingleton<ICompressionService, CompressionService>();
builder.Services.AddSingleton<ITenantScopedBackupExporter, TenantScopedBackupExporter>();
builder.Services.AddSingleton<TenantScopedLogicalBackupExecutionAdapter>();
builder.Services.AddSingleton<ISystemScopedBackupExporter, SystemScopedBackupExporter>();
builder.Services.AddSingleton<CompositeSystemBackupExecutionAdapter>();
builder.Services.Configure<OperationalDrAlertOptions>(
    builder.Configuration.GetSection(OperationalDrAlertOptions.SectionName));
builder.Services.Configure<OperationalDrObservabilityOptions>(
    builder.Configuration.GetSection(OperationalDrObservabilityOptions.SectionName));
builder.Services
    .AddHttpClient(WebhookBackupAlertPublisher.HttpClientName)
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddSingleton<LoggingBackupAlertPublisher>();
builder.Services.AddSingleton<WebhookBackupAlertPublisher>();
builder.Services.AddSingleton<IBackupFailureEmailAlertService, BackupFailureEmailAlertService>();
builder.Services.AddSingleton<EmailBackupAlertPublisher>();
builder.Services.AddSingleton<IBackupAlertPublisher>(sp =>
    new CompositeBackupAlertPublisher(
        new IBackupAlertPublisher[]
        {
            sp.GetRequiredService<LoggingBackupAlertPublisher>(),
            sp.GetRequiredService<WebhookBackupAlertPublisher>(),
            sp.GetRequiredService<ActivityBackupAlertPublisher>(),
            sp.GetRequiredService<EmailBackupAlertPublisher>(),
        }));
builder.Services.AddSingleton<IDrOperationalObservabilityMetrics, PrometheusDrOperationalObservabilityMetrics>();
builder.Services.AddSingleton<DrStaleRunRecoveryAlertingObserver>();
builder.Services.AddSingleton<IDrStaleRunRecoveryObserver>(sp =>
    sp.GetRequiredService<DrStaleRunRecoveryAlertingObserver>());
builder.Services.AddHostedService<DrOperationalObservabilityHostedService>();
builder.Services.AddSingleton<IBackupTimeEstimator, BackupTimeEstimator>();
builder.Services.AddSingleton<ISmartRetentionService, SmartRetentionService>();
builder.Services.AddSingleton<IStorageTierService, StorageTierService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IIncrementalBackupService, IncrementalBackupService>();
builder.Services.AddScoped<IBackupManualTriggerService, BackupManualTriggerService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBackupRunQueryService, BackupRunQueryService>();
builder.Services.AddScoped<IBackupRunService, BackupRunService>();
builder.Services.AddScoped<IPitrService, PitrService>();
builder.Services.AddScoped<IBackupArtifactDownloadService, BackupArtifactDownloadService>();
builder.Services.AddScoped<IBackupRunTenantAccessService, BackupRunTenantAccessService>();
builder.Services.AddScoped<IBackupArtifactImportService, BackupArtifactImportService>();
builder.Services.AddScoped<IBackupRecoverabilitySummaryService, BackupRecoverabilitySummaryService>();
builder.Services.AddSingleton<IBackupStagingDiskMonitor, BackupStagingDiskMonitor>();
builder.Services.AddSingleton<IBackupEncryptionService, BackupEncryptionService>();
builder.Services.AddScoped<IBackupDashboardStatsService, BackupDashboardStatsService>();
builder.Services.AddScoped<IBackupComplianceStatusService, BackupComplianceStatusService>();
builder.Services.AddScoped<IBackupStorageCostService, BackupStorageCostService>();
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
builder.Services.AddHostedService<StorageAlertService>();
builder.Services.AddHostedService<AutomaticCleanupService>();

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
builder.Services.AddScoped<IRestoreService, RestoreService>();
builder.Services.AddScoped<IComplianceCheckService, ComplianceCheckService>();
builder.Services.AddScoped<IRestoreReportService, RestoreReportService>();
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
builder.Services.AddScoped<KasseAPI_Final.Services.Offline.IOfflineOrderService, KasseAPI_Final.Services.Offline.OfflineOrderService>();
builder.Services.AddScoped<KasseAPI_Final.Services.Offline.ISequenceReservationService, KasseAPI_Final.Services.Offline.SequenceReservationService>();
builder.Services.AddScoped<KasseAPI_Final.Services.Offline.IOfflineMonitoringService, KasseAPI_Final.Services.Offline.OfflineMonitoringService>();
builder.Services.Configure<KasseAPI_Final.Configuration.OfflineMonitoringOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.OfflineMonitoringOptions.SectionName));
builder.Services.Configure<KasseAPI_Final.Configuration.OfflineAlertRules>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.OfflineAlertRules.SectionName));
builder.Services.AddHostedService<KasseAPI_Final.Services.Offline.OfflineAlertService>();
builder.Services.AddHostedService<KasseAPI_Final.Services.Hosted.OfflineOrderCleanupHostedService>();
builder.Services.AddHostedService<KasseAPI_Final.Services.DataDeletion.AutoPurgeService>();
builder.Services.AddHostedService<KasseAPI_Final.Services.Hosted.ShiftAutoCloseHostedService>();
builder.Services.AddHostedService<KasseAPI_Final.Services.Reminder.TagesabschlussReminderService>();
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
builder.Services.AddSingleton<IRksvDepPrueftoolRunner, RksvDepPrueftoolRunner>();
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

        QuestPDF.Settings.License = LicenseType.Community;

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

    /// <summary>
    /// <see cref="IdentityBuilder"/> registers <see cref="PasswordValidator{TUser}"/> via TryAdd;
    /// remove it so only <see cref="Validators.RegkassePasswordValidator{TUser}"/> runs (no duplicate errors).
    /// </summary>
    private static void ReplaceDefaultPasswordValidator(IServiceCollection services)
    {
        var defaults = services
            .Where(d =>
                d.ServiceType == typeof(IPasswordValidator<ApplicationUser>)
                && d.ImplementationType == typeof(PasswordValidator<ApplicationUser>))
            .ToList();
        foreach (var descriptor in defaults)
            services.Remove(descriptor);
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

    // HSTS is set by SecurityHeadersMiddleware (Production only; skipped in Staging).
    app.UseHttpsRedirection();
}

// Compress JSON/API responses when the client sends Accept-Encoding (before body-writing middleware).
// No explicit UseRouting() in this host (ASP.NET Core 6+ endpoint routing); place early in the pipeline.
app.UseResponseCompression();

// Security headers on every response (HSTS only outside Development/Staging).
app.UseMiddleware<KasseAPI_Final.Middleware.SecurityHeadersMiddleware>();
app.UseCors("AllowAll");

// API request metrics (Prometheus): early so duration/active include most of the pipeline.
// Skips /metrics, health, and swagger to keep scrape/liveness noise out of api_* series.
var monitoringOptions = app.Configuration.GetSection(MonitoringOptions.SectionName).Get<MonitoringOptions>()
    ?? new MonitoringOptions();
var prometheusEnabled = monitoringOptions.Enabled && monitoringOptions.Prometheus.Enabled;
if (monitoringOptions.Enabled)
    app.UseMiddleware<KasseAPI_Final.Middleware.MetricsMiddleware>();
if (prometheusEnabled)
{
    var metricsPath = string.IsNullOrWhiteSpace(monitoringOptions.MetricsEndpoint)
        ? "/metrics"
        : monitoringOptions.MetricsEndpoint.Trim();
    app.UseMetricServer(url: metricsPath);
}

app.UseMiddleware<KasseAPI_Final.Middleware.LanguageMiddleware>();
// CorrelationId: propagate from request (X-Correlation-Id) or generate; required for audit traceability
app.UseMiddleware<KasseAPI_Final.Middleware.CorrelationIdMiddleware>();

// Global rate limiting (non-breaking): Production-only when RateLimiting:Enabled; skips health/swagger/metrics.
// Placed before auth/tenant work so excess traffic is rejected early (after ForwardedHeaders for real client IP).
app.UseMiddleware<KasseAPI_Final.Middleware.RateLimitingMiddleware>();

// CSRF (double-submit XSRF-TOKEN / X-XSRF-TOKEN): registered before authentication.
// When Security:Csrf:Enabled, validates state-changing requests (Dev bypass via BypassInDevelopment).
app.UseMiddleware<CsrfMiddleware>();

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

// Public GET for one-click generated tenant websites / PWAs (anonymous; slug-scoped paths).
var websiteOpts = app.Services.GetRequiredService<IOptions<WebsiteGeneratorOptions>>().Value;
var websiteRoot = Path.Combine(webHostEnv.ContentRootPath, websiteOpts.RootRelativeDirectory);
Directory.CreateDirectory(websiteRoot);
var websiteRequestPath = websiteOpts.PublicUrlPathPrefix.TrimEnd('/');
var normalizedWebsiteRequestPath = websiteRequestPath.StartsWith('/')
    ? websiteRequestPath
    : "/" + websiteRequestPath;
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(websiteRoot),
    RequestPath = normalizedWebsiteRequestPath,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=300";
    }
});

// Authentication ve Authorization middleware
// Revoked access tokens (logout blacklist) — before JWT principal is established.
app.UseMiddleware<KasseAPI_Final.Middleware.TokenValidationMiddleware>();
app.UseAuthentication();
app.UseMiddleware<KasseAPI_Final.Middleware.TenantContextMiddleware>();
// Fail-closed: tenant-scoped API paths return 404 when ICurrentTenantAccessor.TenantId is unset.
app.UseMiddleware<KasseAPI_Final.Middleware.TenantValidationMiddleware>();
app.UseMiddleware<KasseAPI_Final.Middleware.SessionActivityMiddleware>();
app.UseMiddleware<KasseAPI_Final.Middleware.TenantOperationalGateMiddleware>();
app.UseMiddleware<KasseAPI_Final.Middleware.LicenseMiddleware>();
app.UseAuthorization();
if (!app.Environment.IsDevelopment() && !OpenApiExportMode.IsEnabled)
{
    app.UseElmah();
}
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
    if (prometheusEnabled)
        app.MapMetrics(string.IsNullOrWhiteSpace(monitoringOptions.MetricsEndpoint)
            ? "/metrics"
            : monitoringOptions.MetricsEndpoint.Trim());

    // Test endpoint
    app.MapGet("/", () => "Kasse API is running!");
    app.MapGet("/health", () => "OK");
    // Alias for scripts/docs that probe /api/health (same liveness as /health).
    app.MapGet("/api/health", () => "OK");
}

app.MapGet("/health/finanzonline", (IServiceProvider services) =>
    Results.Json(FinanzOnlineReadinessEvaluator.EvaluateFromRootServices(services)))
    .AllowAnonymous();
app.MapHealthChecks("/health/finanzonline/mode", new HealthCheckOptions
{
    Predicate = check => check.Name == "finanzonline",
    AllowCachingResponses = false,
}).AllowAnonymous();
app.MapHealthChecks("/health/backup/mode", new HealthCheckOptions
{
    Predicate = check => check.Name == "backup",
    AllowCachingResponses = false,
}).AllowAnonymous();
app.MapHealthChecks("/health/elmah", new HealthCheckOptions
{
    Predicate = check => check.Name == "elmah",
    AllowCachingResponses = false,
}).AllowAnonymous();
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

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Swagger;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Security;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

static string RequireConfigValue(IConfiguration configuration, string key, bool isDevelopmentEnvironment, int minLength = 1)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < minLength)
    {
        if (!isDevelopmentEnvironment)
        {
            throw new InvalidOperationException($"Missing or invalid required configuration: {key}");
        }
    }
    return value?.Trim() ?? string.Empty;
}

// Configuration Binding
builder.Services.Configure<CompanyProfileOptions>(builder.Configuration.GetSection(CompanyProfileOptions.SectionName));
builder.Services.Configure<PosCashRegisterFeatureOptions>(
    builder.Configuration.GetSection(PosCashRegisterFeatureOptions.SectionName));
builder.Services.Configure<TseOptions>(builder.Configuration.GetSection(TseOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<AuditRetentionOptions>(builder.Configuration.GetSection(AuditRetentionOptions.SectionName));

// Local development için explicit host binding; production host binding platform tarafından yönetilmelidir.
if (isDevelopment)
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(5183); // 0.0.0.0:5183
        serverOptions.ListenLocalhost(5183); // 127.0.0.1:5183 (backward compatibility)
    });

    Console.WriteLine("🌐 Development host binding: 0.0.0.0:5183 and localhost:5183");
}

// Entity Framework ve PostgreSQL bağlantısı
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")!);
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    options.AddInterceptors(NpgsqlTimestamptzUtcParameterInterceptor.Instance);
    // Dev: InvalidCastException (Guid vs text) teşhisi için kolon/veri loglama
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

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
var secretKey = RequireConfigValue(builder.Configuration, "JwtSettings:SecretKey", isDevelopment, minLength: 32);
var jwtIssuer = RequireConfigValue(builder.Configuration, "JwtSettings:Issuer", isDevelopment);
var jwtAudience = RequireConfigValue(builder.Configuration, "JwtSettings:Audience", isDevelopment);
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
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
    
    // Debug için JWT events ekle
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
            var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sidRaw = context.Principal?.FindFirst("sid")?.Value;
            if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(sidRaw, out var sessionId))
            {
                var refreshTokenService = context.HttpContext.RequestServices.GetRequiredService<IRefreshTokenService>();
                var isActive = refreshTokenService.IsSessionActiveAsync(userId, sessionId, context.HttpContext.RequestAborted)
                    .GetAwaiter()
                    .GetResult();
                if (!isActive)
                {
                    context.Fail("Session invalidated");
                    return Task.CompletedTask;
                }
            }
            logger.LogInformation(
                "JWT token validated successfully: userId={UserId}, correlationId={CorrelationId}",
                userId,
                correlationId);
            return Task.CompletedTask;
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
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<ITokenClaimsService, TokenClaimsService>();
builder.Services.AddScoped<IScopeCheckService, ScopeCheckService>();

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
            policy.WithOrigins(
                      "http://localhost:8081",     // Frontend Expo dev server
                      "http://localhost:3000",     // Frontend web dev server
                      "http://localhost:19006",    // Expo web
                      "http://192.168.1.2:8081",  // iOS Expo client
                      "http://192.168.1.2:3000",  // iOS Web client
                      "http://192.168.1.2:19006", // iOS Expo web
                      "http://localhost:5173",     // Vite dev server
                      "http://127.0.0.1:8081",    // Localhost alternative
                      "http://127.0.0.1:3000"     // Localhost alternative
                  )
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
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "Kasse API", 
        Version = "v1",
        Description = "Registrierkasse API - RKSV uyumlu kasa sistemi"
    });
    
    c.SchemaFilter<KasseAPI_Final.Swagger.TaxTypeSchemaFilter>();
    c.SchemaFilter<KasseAPI_Final.Swagger.TagesabschlussSchemaRequiredFilter>();
    c.OperationFilter<SignatureDebugSwaggerExamples>();
    c.OperationFilter<KasseAPI_Final.Swagger.PosAdminTagsAndDeprecationFilter>();
    
    // JWT authentication için Swagger konfigürasyonu
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// RKSV SignaturePipeline (Checklist 1-5)
builder.Services.AddSingleton<ITseKeyProvider, SoftwareTseKeyProvider>();
builder.Services.AddScoped<SignaturePipeline>();

// Closing-flow signing: Fake (dev, no hardware) vs Real (SignaturePipeline + TSE device readiness)
var tseOpts = builder.Configuration.GetSection(TseOptions.SectionName).Get<TseOptions>() ?? new TseOptions();
if (tseOpts.IsFakeSigningMode)
    builder.Services.AddScoped<ITseProvider, FakeTseProvider>();
else
    builder.Services.AddScoped<ITseProvider, RealTseProvider>();

// Register services
builder.Services.AddScoped<ITseService, TseService>();
builder.Services.AddScoped<IFinanzOnlineService, FinanzOnlineService>();
builder.Services.AddScoped<ITagesabschlussService, TagesabschlussService>();
builder.Services.AddScoped<IUserService, UserService>(); // Kullanıcı servisi eklendi
builder.Services.AddScoped<IReceiptService, ReceiptService>();
// builder.Services.AddScoped<IPrinterService, PrinterService>(); // Geçici olarak devre dışı - ReceiptService bağımlılığı nedeniyle
// builder.Services.AddScoped<ITestService, TestService>(); // Geçici olarak devre dışı - ReceiptService bağımlılığı nedeniyle
builder.Services.AddScoped<IProductModifierValidationService, NoOpProductModifierValidationService>();
builder.Services.AddScoped<IReceiptSequenceService, ReceiptSequenceService>();
builder.Services.AddScoped<ICashRegisterResolutionService, CashRegisterResolutionService>();
builder.Services.AddScoped<ICashRegisterShiftService, CashRegisterShiftService>();
builder.Services.AddScoped<IPosCashRegisterReadinessService, PosCashRegisterReadinessService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IQrImageService, QrImageService>();
builder.Services.AddScoped<TableOrderService>(); // Masa siparişleri persistence servisi
builder.Services.AddScoped<LegacyRouteDeprecationFilter>();

// Register repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IGenericRepository<Customer>, GenericRepository<Customer>>();
builder.Services.AddScoped<IGenericRepository<Product>, GenericRepository<Product>>();
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

// 🚀 Akıllı Sepet Yaşam Döngüsü Service'i
builder.Services.AddHostedService<CartLifecycleService>();
// CartLifecycleService'i ayrıca Scoped olarak da kayıt et (logout için)
builder.Services.AddScoped<CartLifecycleService>();

// Audit log service
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.Configure<KasseAPI_Final.Configuration.OfflineReplayOptions>(
    builder.Configuration.GetSection(KasseAPI_Final.Configuration.OfflineReplayOptions.SectionName));
builder.Services.AddScoped<IOfflineTransactionService, OfflineTransactionService>();
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
builder.Services.AddScoped<IFiscalExportService, FiscalExportService>();
builder.Services.AddScoped<IActorDisplayNameResolver, ActorDisplayNameResolver>();
builder.Services.AddScoped<IUserUniquenessValidationService, UserUniquenessValidationService>();

// HttpContext accessor for audit logging
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Diagnostic: log which database the API actually uses (runtime resolution; env vars can override appsettings)
var connStr = app.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connStr))
{
    var masked = System.Text.RegularExpressions.Regex.Replace(connStr, @"Password=[^;]*", "Password=***");
    Console.WriteLine($"[DbDiagnostic] Resolved DefaultConnection (masked): {masked}");
}
else
{
    Console.WriteLine("[DbDiagnostic] DefaultConnection is null or empty.");
}

// Startup bootstrap: migration, migration gate, roles, users, demo data, product seed, guest customer
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

// Port ayarını sadece development ortamında zorla.
if (app.Environment.IsDevelopment())
{
    app.Urls.Clear();
    app.Urls.Add("http://localhost:5183");
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

// Authentication ve Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Payment Security Middleware
app.UseMiddleware<KasseAPI_Final.Middleware.PaymentSecurityMiddleware>();

app.MapControllers();

// Prometheus /metrics endpoint for scraping (Grafana dashboards)
app.MapMetrics();

// Test endpoint
app.MapGet("/", () => "Kasse API is running!");
app.MapGet("/health", () => "OK");
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

Console.WriteLine("=== KASSE API STARTED ===");
Console.WriteLine("=== LOCALHOST: http://localhost:5183 ===");
Console.WriteLine("=== NETWORK: http://0.0.0.0:5183 ===");
Console.WriteLine("=== LOCAL IP: http://192.168.1.2:5183 ===");
Console.WriteLine("=== SWAGGER: http://localhost:5183/ ===");

app.Run();

// Expose for WebApplicationFactory in integration tests.
public partial class Program { }

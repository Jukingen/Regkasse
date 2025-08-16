using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final;

var builder = WebApplication.CreateBuilder(args);

// Entity Framework ve PostgreSQL bağlantısı
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT SecretKey is not configured in appsettings.json");
}
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        // Debug için ek ayarlar
        RequireExpirationTime = true,
        ValidateTokenReplay = false
    };
    
    // Debug için JWT events ekle
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("JWT token validated successfully for user: {UserId}", 
                context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("JWT authentication failed: {Exception}", context.Exception);
            return Task.CompletedTask;
        }
    };
});

// CORS politikası
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:8081", "http://localhost:3000", "http://localhost:19006")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// API servisleri
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "Kasse API", 
        Version = "v1",
        Description = "Registrierkasse API - RKSV uyumlu kasa sistemi"
    });
    
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

// Register services
builder.Services.AddScoped<ITseService, TseService>();
builder.Services.AddScoped<IFinanzOnlineService, FinanzOnlineService>();
builder.Services.AddScoped<ITagesabschlussService, TagesabschlussService>();
builder.Services.AddScoped<IUserService, UserService>(); // Kullanıcı servisi eklendi
// builder.Services.AddScoped<IReceiptService, ReceiptService>(); // Geçici olarak devre dışı - PaymentDetails model değişikliği nedeniyle
// builder.Services.AddScoped<IPrinterService, PrinterService>(); // Geçici olarak devre dışı - ReceiptService bağımlılığı nedeniyle
// builder.Services.AddScoped<ITestService, TestService>(); // Geçici olarak devre dışı - ReceiptService bağımlılığı nedeniyle
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Register repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IGenericRepository<Customer>, GenericRepository<Customer>>();
builder.Services.AddScoped<IGenericRepository<Product>, GenericRepository<Product>>();
builder.Services.AddScoped<IGenericRepository<Category>, GenericRepository<Category>>();
builder.Services.AddScoped<IGenericRepository<Invoice>, GenericRepository<Invoice>>();
builder.Services.AddScoped<IGenericRepository<PaymentDetails>, GenericRepository<PaymentDetails>>();
builder.Services.AddScoped<IGenericRepository<PaymentItem>, GenericRepository<PaymentItem>>();

// 🚀 Akıllı Sepet Yaşam Döngüsü Service'i
builder.Services.AddHostedService<CartLifecycleService>();
// CartLifecycleService'i ayrıca Scoped olarak da kayıt et (logout için)
builder.Services.AddScoped<CartLifecycleService>();

// Audit log service
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// HttpContext accessor for audit logging
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Veritabanı seed işlemleri
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        
        // Rolleri oluştur
        await RoleSeedData.SeedRolesAsync(roleManager);
        
        // Kullanıcıları oluştur
        await UserSeedData.SeedUsersAsync(userManager);
        
        // Demo verileri ekle
        await AddDemoData.AddDemoDataAsync();
        
        Console.WriteLine("Database seeding completed successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while seeding the database: {ex.Message}");
    }
}

// Port ayarını zorla yap
app.Urls.Clear();
app.Urls.Add("http://localhost:5183");

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

// app.UseHttpsRedirection(); // HTTPS redirect'i devre dışı bırak
app.UseCors("AllowAll");

// Authentication ve Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Payment Security Middleware
app.UseMiddleware<KasseAPI_Final.Middleware.PaymentSecurityMiddleware>();

app.MapControllers();

// Test endpoint
app.MapGet("/", () => "Kasse API is running!");
app.MapGet("/health", () => "OK");

Console.WriteLine("=== KASSE API STARTED ===");
Console.WriteLine("=== PORT: http://localhost:5183 ===");
Console.WriteLine("=== SWAGGER: http://localhost:5183/ ===");

app.Run();

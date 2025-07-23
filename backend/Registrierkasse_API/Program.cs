using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Registrierkasse_API.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Registrierkasse API", Version = "v1" });
    
    // JWT authentication için Swagger konfigürasyonu
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});


// Add services to the container.
builder.Services.AddControllers();

// Configure PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    // Basic authorization policies
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CashierOnly", policy => policy.RequireRole("Cashier"));
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager"));
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"]);

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
        ValidateAudience = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"]
    };
});
// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.WithOrigins("http://localhost:8081")
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
        });
});

// Register AppDbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))); // PostgreSQL için

// Add services to the container
builder.Services.AddMemoryCache();
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<MultilingualReceiptService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ITseHardwareService, TseHardwareService>();
builder.Services.AddScoped<ITseService, TseService>();
builder.Services.AddScoped<IPrinterService, PrinterService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAdvancedReportService, AdvancedReportService>();
builder.Services.AddScoped<IDailyReportService, DailyReportService>();
builder.Services.AddScoped<IFinanzOnlineService, FinanzOnlineService>();
builder.Services.AddHttpClient<IFinanzOnlineService, FinanzOnlineService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
builder.Services.AddScoped<INetworkConnectivityService, NetworkConnectivityService>();
builder.Services.AddHttpClient<INetworkConnectivityService, NetworkConnectivityService>();
builder.Services.AddScoped<IPendingInvoicesService, PendingInvoicesService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<DemoUserService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<DemoUserLogService>();
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped<OperationLogService>();
builder.Services.AddHttpContextAccessor();

// Background services
builder.Services.AddHostedService<BackgroundInvoiceService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// Use CORS
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Automatically apply migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
        context.Database.Migrate();
        await SeedData.Initialize(context, userManager, roleManager); 
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program
{
    // ... existing code ...
}

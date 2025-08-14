using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Minimal services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Port ayarını zorla yap
app.Urls.Clear();
app.Urls.Add("http://localhost:5183");

// Minimal middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Test endpoint
app.MapGet("/", () => "Kasse API is running!");
app.MapGet("/health", () => "OK");

Console.WriteLine("=== MINIMAL KASSE API STARTED ===");
Console.WriteLine("=== PORT: http://localhost:5183 ===");

app.Run();

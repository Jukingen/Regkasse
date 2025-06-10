using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Registrierkasse_API.Data;

namespace Registrierkasse.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // In-memory veritabanı kullan
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });

            // TSE servisini mock'la
            var tseDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ITseService));

            if (tseDescriptor != null)
            {
                services.Remove(tseDescriptor);
            }

            services.AddScoped<ITseService, MockTseService>();
        });
    }
} 
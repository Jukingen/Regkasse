using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemConfigController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SystemConfigController> _logger;

        public SystemConfigController(ApplicationDbContext context, ILogger<SystemConfigController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("config")]
        public async Task<ActionResult<SystemConfigDto>> GetConfiguration()
        {
            try
            {
                var config = await _context.SystemConfigurations.FirstOrDefaultAsync();
                
                if (config == null)
                {
                    // Varsayılan konfigürasyon oluştur
                    config = new SystemConfiguration
                    {
                        OperationMode = "online-only",
                        OfflineSettings = new OfflineSettings
                        {
                            Enabled = false,
                            SyncInterval = 5,
                            MaxOfflineDays = 7,
                            AutoSync = false
                        },
                        TseSettings = new TseSettings
                        {
                            Required = true,
                            OfflineAllowed = false,
                            MaxOfflineTransactions = 100
                        },
                        PrinterSettings = new PrinterSettings
                        {
                            Required = true,
                            OfflineQueue = false,
                            MaxQueueSize = 50
                        }
                    };

                    _context.SystemConfigurations.Add(config);
                    await _context.SaveChangesAsync();
                }

                return Ok(new SystemConfigDto
                {
                    OperationMode = config.OperationMode,
                    OfflineSettings = new OfflineSettingsDto
                    {
                        Enabled = config.OfflineSettings.Enabled,
                        SyncInterval = config.OfflineSettings.SyncInterval,
                        MaxOfflineDays = config.OfflineSettings.MaxOfflineDays,
                        AutoSync = config.OfflineSettings.AutoSync
                    },
                    TseSettings = new TseSettingsDto
                    {
                        Required = config.TseSettings.Required,
                        OfflineAllowed = config.TseSettings.OfflineAllowed,
                        MaxOfflineTransactions = config.TseSettings.MaxOfflineTransactions
                    },
                    PrinterSettings = new PrinterSettingsDto
                    {
                        Required = config.PrinterSettings.Required,
                        OfflineQueue = config.PrinterSettings.OfflineQueue,
                        MaxQueueSize = config.PrinterSettings.MaxQueueSize
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system configuration");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("config")]
        public async Task<ActionResult> UpdateConfiguration([FromBody] SystemConfigDto configDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var config = await _context.SystemConfigurations.FirstOrDefaultAsync();
                
                if (config == null)
                {
                    config = new SystemConfiguration();
                    _context.SystemConfigurations.Add(config);
                }

                // Konfigürasyonu güncelle
                config.OperationMode = configDto.OperationMode;
                
                config.OfflineSettings.Enabled = configDto.OfflineSettings.Enabled;
                config.OfflineSettings.SyncInterval = configDto.OfflineSettings.SyncInterval;
                config.OfflineSettings.MaxOfflineDays = configDto.OfflineSettings.MaxOfflineDays;
                config.OfflineSettings.AutoSync = configDto.OfflineSettings.AutoSync;
                
                config.TseSettings.Required = configDto.TseSettings.Required;
                config.TseSettings.OfflineAllowed = configDto.TseSettings.OfflineAllowed;
                config.TseSettings.MaxOfflineTransactions = configDto.TseSettings.MaxOfflineTransactions;
                
                config.PrinterSettings.Required = configDto.PrinterSettings.Required;
                config.PrinterSettings.OfflineQueue = configDto.PrinterSettings.OfflineQueue;
                config.PrinterSettings.MaxQueueSize = configDto.PrinterSettings.MaxQueueSize;

                await _context.SaveChangesAsync();

                _logger.LogInformation("System configuration updated: {Mode}", configDto.OperationMode);
                
                return Ok(new { message = "Configuration updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system configuration");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("health")]
        public ActionResult HealthCheck()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }

    public class SystemConfigDto
    {
        [Required]
        public string OperationMode { get; set; } = "online-only";
        
        public OfflineSettingsDto OfflineSettings { get; set; } = new();
        public TseSettingsDto TseSettings { get; set; } = new();
        public PrinterSettingsDto PrinterSettings { get; set; } = new();
    }

    public class OfflineSettingsDto
    {
        public bool Enabled { get; set; }
        public int SyncInterval { get; set; } = 5;
        public int MaxOfflineDays { get; set; } = 7;
        public bool AutoSync { get; set; }
    }

    public class TseSettingsDto
    {
        public bool Required { get; set; } = true;
        public bool OfflineAllowed { get; set; }
        public int MaxOfflineTransactions { get; set; } = 100;
    }

    public class PrinterSettingsDto
    {
        public bool Required { get; set; } = true;
        public bool OfflineQueue { get; set; }
        public int MaxQueueSize { get; set; } = 50;
    }
} 
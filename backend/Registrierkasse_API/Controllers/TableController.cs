using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse.Data;
using Registrierkasse.Models;
using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Registrierkasse.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TableController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TableController> _logger;

        public TableController(AppDbContext context, ILogger<TableController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetTables()
        {
            try
            {
                var tables = await _context.Tables
                    .Include(t => t.CurrentOrder)
                    .Include(t => t.Reservations.Where(r => r.ReservationTime.Date == DateTime.Today))
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Masalar başarıyla getirildi", tables });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Masalar getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Masalar getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> CreateTable(CreateTableModel model)
        {
            try
            {
                var table = new Table
                {
                    Number = model.Number,
                    Name = model.Name,
                    Capacity = model.Capacity,
                    Location = model.Location,
                    IsActive = model.IsActive
                };
                
                _context.Tables.Add(table);
                await _context.SaveChangesAsync();
                
                return CreatedAtAction(nameof(GetTable), new { id = table.Id }, table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating table");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTable(Guid id)
        {
            try
            {
                var table = await _context.Tables.FindAsync(id);
                if (table == null)
                {
                    return NotFound();
                }
                return Ok(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting table {TableId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateTableStatus(Guid id, [FromBody] UpdateTableStatusModel model)
        {
            try
            {
                var table = await _context.Tables.FindAsync(id);
                if (table == null)
                {
                    return NotFound();
                }
                
                table.IsActive = model.IsActive;
                await _context.SaveChangesAsync();
                
                return Ok(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating table status for table {TableId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/reservations")]
        public async Task<IActionResult> CreateReservation(Guid id, CreateReservationModel model)
        {
            try
            {
                var table = await _context.Tables.FindAsync(id);
                if (table == null)
                {
                    return NotFound();
                }
                
                var reservation = new TableReservation
                {
                    TableId = id,
                    CustomerName = model.CustomerName,
                    CustomerPhone = model.CustomerPhone,
                    GuestCount = model.NumberOfGuests,
                    ReservationTime = model.ReservationTime,
                    Notes = model.Notes
                };
                
                _context.TableReservations.Add(reservation);
                await _context.SaveChangesAsync();
                
                return CreatedAtAction(nameof(GetTable), new { id = reservation.Id }, reservation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reservation for table {TableId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("reservation/{id}/status")]
        public async Task<IActionResult> UpdateReservationStatus(Guid id, [FromBody] UpdateReservationStatusModel model)
        {
            try
            {
                var reservation = await _context.TableReservations
                    .Include(r => r.Table)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (reservation == null)
                {
                    return NotFound(new { message = "Rezervasyon bulunamadı" });
                }

                reservation.Status = model.Status;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Rezervasyon durumu güncellendi: Masa {reservation.Table.Name} - {model.Status}");

                return Ok(new { message = "Rezervasyon durumu başarıyla güncellendi", reservation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan rezervasyon durumu güncellenirken bir hata oluştu");
                return StatusCode(500, new { message = "Rezervasyon durumu güncellenirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("reservations/today")]
        public async Task<IActionResult> GetTodayReservations()
        {
            try
            {
                var today = DateTime.Today;
                var reservations = await _context.TableReservations
                    .Include(r => r.Table)
                    .Where(r => r.ReservationTime.Date == today)
                    .OrderBy(r => r.ReservationTime)
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(new { message = "Bugünkü rezervasyonlar başarıyla getirildi", reservations });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bugünkü rezervasyonlar getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "Bugünkü rezervasyonlar getirilirken bir hata oluştu", error = ex.Message });
            }
        }
    }

    public class CreateTableModel
    {
        [Required]
        public int Number { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public int Capacity { get; set; }
        
        [Required]
        public string Location { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
    }

    public class UpdateTableStatusModel
    {
        [Required]
        public bool IsActive { get; set; }
    }

    public class CreateReservationModel
    {
        [Required]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        public string CustomerPhone { get; set; } = string.Empty;
        
        [Required]
        public DateTime ReservationTime { get; set; }
        
        [Required]
        public int NumberOfGuests { get; set; }
        
        public string Notes { get; set; } = string.Empty;
    }

    public class UpdateReservationStatusModel
    {
        public ReservationStatus Status { get; set; }
    }
} 
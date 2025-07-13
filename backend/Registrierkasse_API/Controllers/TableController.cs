using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Models;
using Registrierkasse.Services;

namespace Registrierkasse.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TableController : ControllerBase
    {
        private readonly ITableService _tableService;

        public TableController(ITableService tableService)
        {
            _tableService = tableService;
        }

        [HttpGet]
        public async Task<ActionResult<List<Table>>> GetAllTables()
        {
            try
            {
                var tables = await _tableService.GetAllTablesAsync();
                return Ok(tables);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve tables", details = ex.Message });
            }
        }

        [HttpGet("{tableNumber}")]
        public async Task<ActionResult<Table>> GetTable(int tableNumber)
        {
            try
            {
                var table = await _tableService.GetTableByNumberAsync(tableNumber);
                if (table == null)
                {
                    return NotFound(new { error = $"Table {tableNumber} not found" });
                }
                return Ok(table);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve table", details = ex.Message });
            }
        }

        [HttpPut("{tableNumber}/status")]
        public async Task<ActionResult<Table>> UpdateTableStatus(int tableNumber, [FromBody] UpdateTableStatusRequest request)
        {
            try
            {
                var table = await _tableService.UpdateTableStatusAsync(tableNumber, request.Status, request.CustomerName);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update table status", details = ex.Message });
            }
        }

        [HttpPut("{tableNumber}/order")]
        public async Task<ActionResult<Table>> UpdateTableOrder(int tableNumber, [FromBody] UpdateTableOrderRequest request)
        {
            try
            {
                var table = await _tableService.UpdateTableOrderAsync(tableNumber, request.OrderId, request.Total);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update table order", details = ex.Message });
            }
        }

        [HttpPost("{tableNumber}/complete")]
        public async Task<ActionResult<Table>> CompleteTableOrder(int tableNumber, [FromBody] CompleteTableOrderRequest request)
        {
            try
            {
                var table = await _tableService.CompleteTableOrderAsync(tableNumber, request.PaidAmount);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to complete table order", details = ex.Message });
            }
        }

        [HttpPost("{tableNumber}/clear")]
        public async Task<ActionResult<Table>> ClearTable(int tableNumber)
        {
            try
            {
                var table = await _tableService.ClearTableAsync(tableNumber);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to clear table", details = ex.Message });
            }
        }

        [HttpPost("{tableNumber}/reserve")]
        public async Task<ActionResult<Table>> ReserveTable(int tableNumber, [FromBody] ReserveTableRequest request)
        {
            try
            {
                var table = await _tableService.ReserveTableAsync(tableNumber, request.CustomerName);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to reserve table", details = ex.Message });
            }
        }

        [HttpGet("{tableNumber}/history")]
        public async Task<ActionResult<List<Order>>> GetTableOrderHistory(int tableNumber)
        {
            try
            {
                var orders = await _tableService.GetTableOrderHistoryAsync(tableNumber);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve table order history", details = ex.Message });
            }
        }

        [HttpPut("{tableNumber}/customer")]
        public async Task<ActionResult<Table>> UpdateTableCustomer(int tableNumber, [FromBody] UpdateTableCustomerRequest request)
        {
            try
            {
                var table = await _tableService.UpdateTableCustomerAsync(tableNumber, request.CustomerName);
                return Ok(table);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update table customer", details = ex.Message });
            }
        }
    }

    public class UpdateTableStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
    }

    public class UpdateTableOrderRequest
    {
        public Guid OrderId { get; set; }
        public decimal Total { get; set; }
    }

    public class CompleteTableOrderRequest
    {
        public decimal PaidAmount { get; set; }
    }

    public class ReserveTableRequest
    {
        public string CustomerName { get; set; } = string.Empty;
    }

    public class UpdateTableCustomerRequest
    {
        public string CustomerName { get; set; } = string.Empty;
    }
} 
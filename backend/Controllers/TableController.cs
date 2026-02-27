using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace KasseAPI_Final.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TableController : ControllerBase
    {
        // Basit masa listesi - gerçek uygulamada veritabanından gelir
        private static readonly List<TableInfo> _tables = new List<TableInfo>
        {
            new TableInfo { Id = 1, Number = 1, Status = "Available", Capacity = 4 },
            new TableInfo { Id = 2, Number = 2, Status = "Available", Capacity = 4 },
            new TableInfo { Id = 3, Number = 3, Status = "Available", Capacity = 6 },
            new TableInfo { Id = 4, Number = 4, Status = "Available", Capacity = 4 },
            new TableInfo { Id = 5, Number = 5, Status = "Available", Capacity = 8 },
            new TableInfo { Id = 6, Number = 6, Status = "Available", Capacity = 4 },
            new TableInfo { Id = 7, Number = 7, Status = "Available", Capacity = 6 },
            new TableInfo { Id = 8, Number = 8, Status = "Available", Capacity = 4 },
            new TableInfo { Id = 9, Number = 9, Status = "Available", Capacity = 4 },
            new TableInfo { Id = 10, Number = 10, Status = "Available", Capacity = 6 }
        };

        [HttpGet]
        public ActionResult<IEnumerable<TableInfo>> GetTables()
        {
            return Ok(_tables);
        }

        [HttpGet("{id}")]
        public ActionResult<TableInfo> GetTable(int id)
        {
            var table = _tables.Find(t => t.Id == id);
            if (table == null)
            {
                return NotFound($"Table {id} not found");
            }
            return Ok(table);
        }

        [HttpPost("{id}/status")]
        public ActionResult UpdateTableStatus(int id, [FromBody] UpdateTableStatusRequest request)
        {
            var table = _tables.Find(t => t.Id == id);
            if (table == null)
            {
                return NotFound($"Table {id} not found");
            }

            table.Status = request.Status;
            return Ok(table);
        }
    }

    public class TableInfo
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string Status { get; set; } = "Available";
        public int Capacity { get; set; }
    }

    public class UpdateTableStatusRequest
    {
        public string Status { get; set; } = "Available";
    }
}

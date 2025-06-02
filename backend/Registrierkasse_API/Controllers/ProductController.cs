using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Registrierkasse.Data;
using Registrierkasse.Models;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(Guid id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null || !product.IsActive)
            {
                return NotFound();
            }

            return Ok(product);
        }
    }
} 
using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using System.Linq; using System.Threading.Tasks;
namespace UIStore.Controllers.Api {
    [ApiController] [Route("api/v1/products")]
    public class ProductsApiController : ControllerBase {
        private readonly ApplicationDbContext _ctx; public ProductsApiController(ApplicationDbContext ctx) { _ctx = ctx; }
        [HttpGet] public async Task<IActionResult> GetProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 20) {
            var products = await _ctx.Products.Where(p => !p.IsDeleted).OrderByDescending(p => p.ID).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new { p.ID, p.Title, p.Price, p.ImageUrl, Category = p.Category.Name, p.AverageRating }).ToListAsync();
            return Ok(new { success = true, data = products });
        }
        [HttpGet("{id}")] public async Task<IActionResult> GetProduct(int id) {
            var p = await _ctx.Products.Include(x => x.Category).Where(x => !x.IsDeleted && x.ID == id)
                .Select(x => new { x.ID, x.Title, x.Description, x.Price, x.ImageUrl, Category = x.Category.Name, x.AverageRating, x.SalesCount }).FirstOrDefaultAsync();
            if (p == null) return NotFound(new { success = false, message = "Product not found" });
            return Ok(new { success = true, data = p });
        }
    }
}

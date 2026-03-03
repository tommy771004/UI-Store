using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using System.Linq; using System.Threading.Tasks;
namespace UIStore.Controllers.Api {
    [ApiController] [Route("api/v1/products")]
    public class ProductsApiController : ControllerBase {
        private readonly ApplicationDbContext _ctx; public ProductsApiController(ApplicationDbContext ctx) { _ctx = ctx; }

        /// <summary>取得商品列表（支援搜尋、分類、排序、分頁）</summary>
        [HttpGet]
        public async Task<IActionResult> GetProducts(
            [FromQuery] string keyword = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] string sortBy = "newest",
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) {
            if (pageSize > 100) pageSize = 100;
            var query = _ctx.Products.Where(p => !p.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(p => p.Title.Contains(keyword) || p.Subtitle.Contains(keyword));
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);
            query = sortBy switch {
                "price_asc"  => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "rating"     => query.OrderByDescending(p => p.AverageRating),
                "popular"    => query.OrderByDescending(p => p.SalesCount),
                _            => query.OrderByDescending(p => p.ID)
            };
            var total = await query.CountAsync();
            var products = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new { p.ID, p.Title, p.Subtitle, p.Price, p.ImageUrl, Category = p.Category.Name, p.AverageRating, p.ReviewCount, p.SalesCount, p.IsNew }).ToListAsync();
            return Ok(new { success = true, data = products, page, pageSize, total, totalPages = (int)System.Math.Ceiling(total / (double)pageSize) });
        }

        /// <summary>取得單一商品詳細資訊（含圖片 Gallery）</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id) {
            var p = await _ctx.Products.Include(x => x.Category).Include(x => x.Images).Where(x => !x.IsDeleted && x.ID == id)
                .Select(x => new { x.ID, x.Title, x.Subtitle, x.Description, x.Price, x.ImageUrl, Category = x.Category.Name, x.AverageRating, x.ReviewCount, x.SalesCount, x.IsNew, Gallery = x.Images.OrderBy(i=>i.SortOrder).Select(i=>i.ImageUrl).ToList() }).FirstOrDefaultAsync();
            if (p == null) return NotFound(new { success = false, message = "Product not found" });
            return Ok(new { success = true, data = p });
        }

        /// <summary>取得商品評價列表</summary>
        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetReviews(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10) {
            if (!await _ctx.Products.AnyAsync(p => p.ID == id && !p.IsDeleted)) return NotFound(new { success = false, message = "Product not found" });
            var reviews = await _ctx.ProductReviews.Where(r => r.ProductId == id).OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new { r.Id, r.Rating, r.Comment, r.CreatedAt, User = r.User.FullName }).ToListAsync();
            return Ok(new { success = true, data = reviews });
        }

        /// <summary>取得分類列表</summary>
        [HttpGet("/api/v1/categories")]
        public async Task<IActionResult> GetCategories() {
            var cats = await _ctx.Categories.Select(c => new { c.Id, c.Name, ProductCount = c.Products.Count(p => !p.IsDeleted) }).ToListAsync();
            return Ok(new { success = true, data = cats });
        }
    }
}

using System; using System.Linq; using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using UIStore.Models; using UIStore.Services;
namespace UIStore.Controllers {
    public class HomeController : BaseController {
        private readonly ApplicationDbContext _ctx; private readonly CacheService _cs;
        public HomeController(ApplicationDbContext ctx, CacheService cs) { _ctx = ctx; _cs = cs; }

        public async Task<IActionResult> Index(string keyword, int? categoryId, string sortBy = "newest", decimal? minPrice = null, decimal? maxPrice = null, int? minRating = null, int page = 1) {
            var query = _ctx.Products.Include(p => p.Category).Where(p => !p.IsDeleted).AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(p => p.Title.Contains(keyword) || p.Subtitle.Contains(keyword));
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);
            if (minRating.HasValue) query = query.Where(p => p.AverageRating >= minRating.Value);
            query = sortBy switch {
                "price_asc"  => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "rating"     => query.OrderByDescending(p => p.AverageRating),
                "popular"    => query.OrderByDescending(p => p.SalesCount),
                _            => query.OrderByDescending(p => p.ID)
            };
            int ps = 12;
            ViewBag.TotalPages = (int)Math.Ceiling(await query.CountAsync() / (double)ps);
            ViewBag.CurrentPage = page;
            ViewBag.Categories = await _cs.GetOrSetAsync("MenuCategories", () => _ctx.Categories.AsNoTracking().ToListAsync(), TimeSpan.FromHours(1));
            ViewBag.Keyword = keyword; ViewBag.CategoryId = categoryId; ViewBag.SortBy = sortBy;
            ViewBag.MinPrice = minPrice; ViewBag.MaxPrice = maxPrice; ViewBag.MinRating = minRating;
            return View(await query.Skip((page - 1) * ps).Take(ps).ToListAsync());
        }

        public async Task<IActionResult> Details(int id) {
            var p = await _ctx.Products.Include(x => x.Category).Include(x => x.Uploader).Include(x => x.Reviews).ThenInclude(r => r.User).Include(x => x.Images).AsNoTracking().FirstOrDefaultAsync(x => x.ID == id && !x.IsDeleted);
            if (p == null) return NotFound();
            ViewBag.HasPurchased = User.Identity.IsAuthenticated && await _ctx.OrderItems.Include(o=>o.Order).AnyAsync(o => o.ProductId == id && o.Order.UserId == CurrentUserId && o.Order.PaymentStatus == "Paid");
            ViewBag.IsInWishlist = User.Identity.IsAuthenticated && await _ctx.Wishlists.AnyAsync(w => w.ProductId == id && w.UserId == CurrentUserId);
            // 相關商品（同分類，排除自己，取4件）
            ViewBag.RelatedProducts = await _ctx.Products.Where(x => x.CategoryId == p.CategoryId && x.ID != id && !x.IsDeleted).OrderByDescending(x => x.SalesCount).Take(4).AsNoTracking().ToListAsync();
            return View(p);
        }

        [HttpPost] [Authorize] [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int productId, int rating, string comment) {
            if (!await _ctx.OrderItems.Include(o=>o.Order).AnyAsync(o => o.ProductId == productId && o.Order.UserId == CurrentUserId && o.Order.PaymentStatus == "Paid")) return Forbid();
            if (await _ctx.ProductReviews.AnyAsync(r => r.ProductId == productId && r.UserId == CurrentUserId)) { TempData["MessageType"]="warning"; TempData["Message"]="已評價過"; return RedirectToAction("Details", new { id = productId }); }
            _ctx.ProductReviews.Add(new ProductReview { ProductId = productId, UserId = CurrentUserId, Rating = Math.Clamp(rating, 1, 5), Comment = System.Net.WebUtility.HtmlEncode(comment) }); await _ctx.SaveChangesAsync(); TempData["MessageType"]="success"; TempData["Message"]="評價成功"; return RedirectToAction("Details", new { id = productId });
        }

        public IActionResult Support() => View();
        public IActionResult Privacy() => View();
        public IActionResult Terms() => View();
    }
}

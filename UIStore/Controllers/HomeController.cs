using System; using System.Linq; using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using UIStore.Models; using UIStore.Services;
namespace UIStore.Controllers {
    public class HomeController : BaseController {
        private readonly ApplicationDbContext _ctx; private readonly CacheService _cs; 
        public HomeController(ApplicationDbContext ctx, CacheService cs) { _ctx = ctx; _cs = cs; }
        public async Task<IActionResult> Index(string keyword, int? categoryId, int page = 1) { 
            var query = _ctx.Products.Include(p => p.Category).Where(p => !p.IsDeleted).AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(p => p.Title.Contains(keyword) || p.Subtitle.Contains(keyword));
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
            int ps = 12; ViewBag.TotalPages = (int)Math.Ceiling(await query.CountAsync() / (double)ps); ViewBag.CurrentPage = page;
            ViewBag.Categories = await _cs.GetOrSetAsync("MenuCategories", () => _ctx.Categories.AsNoTracking().ToListAsync(), TimeSpan.FromHours(1)); 
            ViewBag.Keyword = keyword; ViewBag.CategoryId = categoryId;
            return View(await query.OrderByDescending(p => p.ID).Skip((page - 1) * ps).Take(ps).ToListAsync()); 
        }
        public async Task<IActionResult> Details(int id) {
            var p = await _ctx.Products.Include(x => x.Category).Include(x => x.Uploader).Include(x => x.Reviews).ThenInclude(r => r.User).AsNoTracking().FirstOrDefaultAsync(x => x.ID == id && !x.IsDeleted);
            if (p == null) return NotFound();
            ViewBag.HasPurchased = User.Identity.IsAuthenticated && await _ctx.OrderItems.Include(o=>o.Order).AnyAsync(o => o.ProductId == id && o.Order.UserId == CurrentUserId && o.Order.PaymentStatus == "Paid");
            ViewBag.IsInWishlist = User.Identity.IsAuthenticated && await _ctx.Wishlists.AnyAsync(w => w.ProductId == id && w.UserId == CurrentUserId);
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

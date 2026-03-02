using System.Linq; using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using UIStore.Models;
namespace UIStore.Controllers {
    [Authorize] public class WishlistController : BaseController {
        private readonly ApplicationDbContext _ctx; public WishlistController(ApplicationDbContext ctx) { _ctx = ctx; }
        [HttpGet] public async Task<IActionResult> Index() => View(await _ctx.Wishlists.Include(w => w.Product).ThenInclude(p => p.Category).Where(w => w.UserId == CurrentUserId).ToListAsync());
        [HttpPost] public async Task<IActionResult> Toggle(int productId) {
            var existing = await _ctx.Wishlists.FirstOrDefaultAsync(w => w.UserId == CurrentUserId && w.ProductId == productId);
            if (existing != null) { _ctx.Wishlists.Remove(existing); TempData["MessageType"] = "info"; TempData["Message"] = "已從願望清單移除"; } 
            else { _ctx.Wishlists.Add(new WishlistItem { UserId = CurrentUserId, ProductId = productId }); TempData["MessageType"] = "success"; TempData["Message"] = "已加入願望清單！"; }
            await _ctx.SaveChangesAsync(); return RedirectToAction("Details", "Home", new { id = productId });
        }
    }
}

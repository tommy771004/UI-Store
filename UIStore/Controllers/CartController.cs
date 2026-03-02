using System.Linq; using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using UIStore.Models; using UIStore.Services;
namespace UIStore.Controllers {
    [Authorize] public class CartController : BaseController {
        private readonly ApplicationDbContext _ctx; private readonly CacheService _cs; public CartController(ApplicationDbContext ctx, CacheService cs) { _ctx = ctx; _cs = cs; }
        [HttpGet] public async Task<IActionResult> Index() => View(await _ctx.CartItems.Include(c=>c.Product).Where(c=>c.UserId==CurrentUserId).ToListAsync());
        [HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> AddToCart(int productId) { if(await _ctx.CartItems.CountAsync(c=>c.UserId==CurrentUserId) >= 20) return RedirectToAction("Index"); if (!await _ctx.CartItems.AnyAsync(c=>c.UserId==CurrentUserId && c.ProductId==productId)) { _ctx.CartItems.Add(new CartItem { UserId = CurrentUserId, ProductId = productId, Quantity = 1 }); await _ctx.SaveChangesAsync(); _cs.Remove($"CartCount_{CurrentUserId}"); TempData["MessageType"]="success"; TempData["Message"]="成功加入購物袋"; } return RedirectToAction("Index"); }
        [HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> Remove(int cartItemId) { var item = await _ctx.CartItems.FirstOrDefaultAsync(c=>c.Id==cartItemId && c.UserId==CurrentUserId); if(item!=null) { _ctx.CartItems.Remove(item); await _ctx.SaveChangesAsync(); _cs.Remove($"CartCount_{CurrentUserId}"); } return RedirectToAction("Index"); }
    }
}

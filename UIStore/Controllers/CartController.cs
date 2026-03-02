using System.Linq; using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using UIStore.Models; using UIStore.Services;
namespace UIStore.Controllers {
    [Authorize] public class CartController : BaseController {
        private readonly ApplicationDbContext _ctx; private readonly CacheService _cs; public CartController(ApplicationDbContext ctx, CacheService cs) { _ctx = ctx; _cs = cs; }
        [HttpGet] public async Task<IActionResult> Index() => View(await _ctx.CartItems.Include(c=>c.Product).Where(c=>c.UserId==CurrentUserId).ToListAsync());
        [HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> AddToCart(int productId, int quantity = 1) { if(await _ctx.CartItems.CountAsync(c=>c.UserId==CurrentUserId) >= 20) return RedirectToAction("Index"); var existing = await _ctx.CartItems.FirstOrDefaultAsync(c=>c.UserId==CurrentUserId && c.ProductId==productId);
            if (existing == null) {
                _ctx.CartItems.Add(new CartItem { UserId = CurrentUserId, ProductId = productId, Quantity = Math.Clamp(quantity,1,20) });
            } else {
                existing.Quantity = Math.Clamp(existing.Quantity + quantity, 1, 20);
            }
            await _ctx.SaveChangesAsync(); _cs.Remove($"CartCount_{CurrentUserId}"); TempData["MessageType"]="success"; TempData["Message"]="成功加入購物袋"; 
            return RedirectToAction("Index"); }
        [HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> Remove(int cartItemId) { var item = await _ctx.CartItems.FirstOrDefaultAsync(c=>c.Id==cartItemId && c.UserId==CurrentUserId); if(item!=null) { _ctx.CartItems.Remove(item); await _ctx.SaveChangesAsync(); _cs.Remove($"CartCount_{CurrentUserId}"); } return RedirectToAction("Index"); }
        [HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity) {
            var item = await _ctx.CartItems.FirstOrDefaultAsync(c=>c.Id==cartItemId && c.UserId==CurrentUserId);
            if(item != null) {
                if(quantity <= 0) {
                    _ctx.CartItems.Remove(item);
                } else {
                    item.Quantity = Math.Clamp(quantity, 1, 99);
                }
                await _ctx.SaveChangesAsync();
                _cs.Remove($"CartCount_{CurrentUserId}");
            }
            return RedirectToAction("Index");
        }
        [HttpPost] public async Task<JsonResult> UpdateQuantityAjax(int cartItemId, int quantity) {
            var item = await _ctx.CartItems.Include(c=>c.Product).FirstOrDefaultAsync(c=>c.Id==cartItemId && c.UserId==CurrentUserId);
            if(item == null) return Json(new { success = false });
            if(quantity <= 0) { _ctx.CartItems.Remove(item); await _ctx.SaveChangesAsync(); _cs.Remove($"CartCount_{CurrentUserId}"); return Json(new { success = true, removed = true }); }
            item.Quantity = Math.Clamp(quantity,1,99); await _ctx.SaveChangesAsync(); _cs.Remove($"CartCount_{CurrentUserId}");
            var total = await _ctx.CartItems.Include(c=>c.Product).Where(c=>c.UserId==CurrentUserId).SumAsync(c=>c.Product.Price * c.Quantity);
            return Json(new { success = true, subtotal = total, itemTotal = item.Product.Price * item.Quantity });
        }
    }
}

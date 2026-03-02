using System.IO; using System.Linq; using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Hosting; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data;
namespace UIStore.Controllers {
    [Authorize] public class DownloadsController : BaseController {
        private readonly ApplicationDbContext _ctx; private readonly IWebHostEnvironment _env; public DownloadsController(ApplicationDbContext ctx, IWebHostEnvironment env) { _ctx = ctx; _env = env; }
        [HttpGet] public async Task<IActionResult> MyPurchases() => View(await _ctx.OrderItems.Include(o=>o.Order).Include(o=>o.Product).Where(o=>o.Order.UserId==CurrentUserId && o.Order.PaymentStatus=="Paid").Select(o=>o.Product).Distinct().ToListAsync());
        [HttpGet] public async Task<IActionResult> MyOrders() => View(await _ctx.Orders.Include(o=>o.OrderItems).ThenInclude(i=>i.Product).Where(o=>o.UserId==CurrentUserId).OrderByDescending(o=>o.OrderDate).ToListAsync());
        [HttpGet] public async Task<IActionResult> GetTemplate(int productId) {
            if(!await _ctx.OrderItems.Include(o=>o.Order).AnyAsync(o=>o.ProductId==productId && o.Order.UserId==CurrentUserId && o.Order.PaymentStatus=="Paid")) return Forbid();
            var p = await _ctx.Products.FindAsync(productId); var path = Path.Combine(_env.ContentRootPath, "SecureTemplates", p.TemplateFileName ?? "x");
            if(!System.IO.File.Exists(path)) return NotFound(); return PhysicalFile(path, "application/zip", $"{p.Title}.zip");
        }
    }
}

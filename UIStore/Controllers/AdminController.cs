using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using UIStore.Models; using System.Linq;
namespace UIStore.Controllers {
    [Authorize(Roles = "Admin")] 
    public class AdminController : Controller {
        private readonly ApplicationDbContext _ctx; public AdminController(ApplicationDbContext ctx) { _ctx = ctx; }
        public async Task<IActionResult> Index() { ViewBag.TotalUsers = await _ctx.Users.CountAsync(); ViewBag.TotalOrders = await _ctx.Orders.CountAsync(o => o.PaymentStatus == "Paid"); ViewBag.TotalRevenue = await _ctx.Orders.Where(o => o.PaymentStatus == "Paid").SumAsync(o => o.TotalAmount); return View(); }
        public async Task<IActionResult> Categories() => View(await _ctx.Categories.Include(c => c.Products.Where(p => !p.IsDeleted)).ToListAsync());
        [HttpPost] public async Task<IActionResult> CreateCategory(string name) { if (!string.IsNullOrWhiteSpace(name)) { _ctx.Categories.Add(new Category { Name = name }); await _ctx.SaveChangesAsync(); TempData["MessageType"]="success"; TempData["Message"]="分類新增成功"; } return RedirectToAction("Categories"); }
        [HttpPost] public async Task<IActionResult> EditCategory(int id, string name) {
            var c = await _ctx.Categories.FindAsync(id);
            if(c!=null && !string.IsNullOrWhiteSpace(name)) {
                c.Name = name;
                await _ctx.SaveChangesAsync();
                TempData["MessageType"] = "success";
                TempData["Message"] = "分類名稱已更新";
            }
            return RedirectToAction("Categories");
        }
        [HttpPost] public async Task<IActionResult> DeleteCategory(int id) {
            var c = await _ctx.Categories.Include(cat=>cat.Products).FirstOrDefaultAsync(cat=>cat.Id==id);
            if(c!=null) {
                if(c.Products.Any(p=>!p.IsDeleted)) {
                    TempData["MessageType"]="warning";
                    TempData["Message"]="請先將此分類下的商品下架或移除後再刪除";
                } else {
                    _ctx.Categories.Remove(c);
                    await _ctx.SaveChangesAsync();
                    TempData["MessageType"]="success";
                    TempData["Message"]="分類已刪除";
                }
            }
            return RedirectToAction("Categories");
        }
        public async Task<IActionResult> Products() => View(await _ctx.Products.Include(p => p.Category).Include(p => p.Uploader).ToListAsync());
        [HttpPost] public async Task<IActionResult> ToggleProductStatus(int id) { var p = await _ctx.Products.FindAsync(id); if (p != null) { p.IsDeleted = !p.IsDeleted; await _ctx.SaveChangesAsync(); TempData["MessageType"]="success"; TempData["Message"]= p.IsDeleted ? "已下架" : "已上架"; } return RedirectToAction("Products"); }
        public async Task<IActionResult> Orders() => View(await _ctx.Orders.Include(o => o.User).OrderByDescending(o => o.OrderDate).ToListAsync());
        [HttpGet]
        public async Task<IActionResult> Settings() {
            var tax = await _ctx.SystemSettings.FindAsync("TaxRate");
            var ship = await _ctx.SystemSettings.FindAsync("ShippingFee");
            ViewBag.Tax = tax?.Value ?? "0";
            ViewBag.Shipping = ship?.Value ?? "0";
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Settings(string taxRate, string shippingFee) {
            var t = await _ctx.SystemSettings.FindAsync("TaxRate");
            if(t==null) { _ctx.SystemSettings.Add(new SystemSetting{ Key="TaxRate", Value = taxRate ?? "0", Description = "稅率（百分比）"}); }
            else { t.Value = taxRate ?? "0"; }
            var s = await _ctx.SystemSettings.FindAsync("ShippingFee");
            if(s==null) { _ctx.SystemSettings.Add(new SystemSetting{ Key="ShippingFee", Value = shippingFee ?? "0", Description = "運費（固定金額）"}); }
            else { s.Value = shippingFee ?? "0"; }
            await _ctx.SaveChangesAsync();
            TempData["MessageType"] = "success"; TempData["Message"] = "設定已儲存";
            return RedirectToAction("Settings");
        }
    }
}

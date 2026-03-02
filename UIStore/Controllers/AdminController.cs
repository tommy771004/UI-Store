using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using UIStore.Models; using System.Linq;
namespace UIStore.Controllers {
    [Authorize(Roles = "Admin")] 
    public class AdminController : Controller {
        private readonly ApplicationDbContext _ctx; public AdminController(ApplicationDbContext ctx) { _ctx = ctx; }
        public async Task<IActionResult> Index() { ViewBag.TotalUsers = await _ctx.Users.CountAsync(); ViewBag.TotalOrders = await _ctx.Orders.CountAsync(o => o.PaymentStatus == "Paid"); ViewBag.TotalRevenue = await _ctx.Orders.Where(o => o.PaymentStatus == "Paid").SumAsync(o => o.TotalAmount); return View(); }
        public async Task<IActionResult> Categories() => View(await _ctx.Categories.Include(c => c.Products.Where(p => !p.IsDeleted)).ToListAsync());
        [HttpPost] public async Task<IActionResult> CreateCategory(string name) { if (!string.IsNullOrWhiteSpace(name)) { _ctx.Categories.Add(new Category { Name = name }); await _ctx.SaveChangesAsync(); TempData["MessageType"]="success"; TempData["Message"]="分類新增成功"; } return RedirectToAction("Categories"); }
        public async Task<IActionResult> Products() => View(await _ctx.Products.Include(p => p.Category).Include(p => p.Uploader).ToListAsync());
        [HttpPost] public async Task<IActionResult> ToggleProductStatus(int id) { var p = await _ctx.Products.FindAsync(id); if (p != null) { p.IsDeleted = !p.IsDeleted; await _ctx.SaveChangesAsync(); TempData["MessageType"]="success"; TempData["Message"]= p.IsDeleted ? "已下架" : "已上架"; } return RedirectToAction("Products"); }
        public async Task<IActionResult> Orders() => View(await _ctx.Orders.Include(o => o.User).OrderByDescending(o => o.OrderDate).ToListAsync());
    }
}

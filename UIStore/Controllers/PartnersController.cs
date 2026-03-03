using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Hosting; using Microsoft.AspNetCore.Identity; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.RateLimiting; using Microsoft.EntityFrameworkCore; using Microsoft.Extensions.Logging; using SixLabors.ImageSharp; using SixLabors.ImageSharp.Formats.Jpeg; using UIStore.Data; using UIStore.Models; using UIStore.Services;
namespace UIStore.Controllers {
    [Authorize(Roles = "Partner,Admin")]
    public class PartnersController : BaseController {
        private readonly ApplicationDbContext _ctx; private readonly IWebHostEnvironment _env; private readonly FileSecurityService _sec; private readonly UserManager<ApplicationUser> _um; private readonly SignInManager<ApplicationUser> _sm;
        public PartnersController(ApplicationDbContext c, IWebHostEnvironment e, FileSecurityService s, UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm) { _ctx=c; _env=e; _sec=s; _um=um; _sm=sm; }

        [HttpGet] public async Task<IActionResult> Index() {
            var prods = await _ctx.Products.Include(p=>p.Category).Where(p=>p.UploaderId==CurrentUserId && !p.IsDeleted).ToListAsync();
            var items = new List<PartnerDashboardViewModel>();
            foreach(var p in prods) { var sales = await _ctx.OrderItems.Include(o=>o.Order).Where(o=>o.ProductId==p.ID && o.Order.PaymentStatus=="Paid").ToListAsync(); items.Add(new PartnerDashboardViewModel{ Product=p, SalesCount=sales.Sum(s=>s.Quantity), TotalRevenue=sales.Sum(s=>s.UnitPrice*s.Quantity) }); }
            return View(items);
        }

        // 銷售圖表資料 API（最近 6 個月）
        [HttpGet] public async Task<IActionResult> SalesChartData() {
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var productIds = await _ctx.Products.Where(p=>p.UploaderId==CurrentUserId && !p.IsDeleted).Select(p=>p.ID).ToListAsync();
            var data = await _ctx.OrderItems.Include(o=>o.Order)
                .Where(oi => productIds.Contains(oi.ProductId) && oi.Order.PaymentStatus=="Paid" && oi.Order.PaidAt >= sixMonthsAgo)
                .GroupBy(oi => new { oi.Order.PaidAt.Value.Year, oi.Order.PaidAt.Value.Month })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Revenue = g.Sum(x => x.UnitPrice * x.Quantity), Sales = g.Sum(x => x.Quantity) })
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync();
            var result = data.Select(d => new { label = $"{d.Year}/{d.Month:D2}", revenue = d.Revenue, sales = d.Sales });
            return Json(result);
        }

        [HttpGet] public IActionResult Upload() { ViewBag.Categories = _ctx.Categories.ToList(); return View(new UploadUIViewModel()); }

        [HttpPost] [EnableRateLimiting("upload")]
        public async Task<IActionResult> Upload(UploadUIViewModel model) {
            if(!ModelState.IsValid) { ViewBag.Categories = _ctx.Categories.ToList(); return View(model); }
            var imgName = Guid.NewGuid().ToString("N") + ".jpg";
            using (var image = await Image.LoadAsync(model.CoverImage.OpenReadStream())) { await image.SaveAsJpegAsync(Path.Combine(_env.WebRootPath, "uploads", "images", imgName), new JpegEncoder { Quality = 80 }); }
            var res = await _sec.ScanZipContentAsync(model.TemplateFile);
            if(!res.IsSafe) { if(res.IsMalicious) { var u = await _um.FindByIdAsync(CurrentUserId); await _um.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddDays(5)); await _sm.SignOutAsync(); return RedirectToAction("Lockout", "Account"); } ModelState.AddModelError("TemplateFile", res.ErrorMessage); ViewBag.Categories = _ctx.Categories.ToList(); return View(model); }
            var tName = Guid.NewGuid().ToString("N")+".zip";
            using var fs = new FileStream(Path.Combine(_env.ContentRootPath,"SecureTemplates",tName), FileMode.Create); await model.TemplateFile.CopyToAsync(fs);
            var product = new ProductViewModel { Title=model.Title, Subtitle=model.Subtitle, Description=model.Description, Price=model.Price, CategoryId=model.CategoryId, ImageUrl=$"/uploads/images/{imgName}", TemplateFileName=tName, UploaderId=CurrentUserId, IsNew=true };
            _ctx.Products.Add(product); await _ctx.SaveChangesAsync();
            // 上傳額外圖片
            if (model.GalleryImages != null && model.GalleryImages.Count > 0) {
                int sort = 0;
                foreach (var img in model.GalleryImages.Take(5)) {
                    if (img == null || img.Length == 0) continue;
                    var gName = Guid.NewGuid().ToString("N") + ".jpg";
                    using var gi = await Image.LoadAsync(img.OpenReadStream()); await gi.SaveAsJpegAsync(Path.Combine(_env.WebRootPath, "uploads", "images", gName), new JpegEncoder { Quality = 80 });
                    _ctx.ProductImages.Add(new ProductImage { ProductId = product.ID, ImageUrl = $"/uploads/images/{gName}", SortOrder = sort++ });
                }
                await _ctx.SaveChangesAsync();
            }
            TempData["MessageType"]="success"; TempData["Message"]="上傳成功"; return RedirectToAction("Index");
        }

        [HttpGet] public async Task<IActionResult> Edit(int id) {
            var p = await _ctx.Products.Include(x=>x.Images).FirstOrDefaultAsync(x=>x.ID==id && x.UploaderId==CurrentUserId);
            if(p==null) return NotFound(); ViewBag.Categories = _ctx.Categories.ToList();
            return View(new EditUIViewModel { ID = p.ID, Title = p.Title, Subtitle = p.Subtitle, Description = p.Description, Price = p.Price, CategoryId = p.CategoryId });
        }

        [HttpPost] public async Task<IActionResult> Edit(EditUIViewModel model) {
            var p = await _ctx.Products.Include(x=>x.Images).FirstOrDefaultAsync(x=>x.ID==model.ID && x.UploaderId==CurrentUserId); if(p==null) return NotFound();
            if(!ModelState.IsValid) { ViewBag.Categories = _ctx.Categories.ToList(); return View(model); }
            p.Title = model.Title; p.Subtitle = model.Subtitle; p.Description = model.Description; p.Price = model.Price; p.CategoryId = model.CategoryId;
            if(model.CoverImage!=null) { var imgName = Guid.NewGuid().ToString("N") + ".jpg"; using (var img = await Image.LoadAsync(model.CoverImage.OpenReadStream())) { await img.SaveAsJpegAsync(Path.Combine(_env.WebRootPath, "uploads", "images", imgName), new JpegEncoder { Quality=80 }); } p.ImageUrl = $"/uploads/images/{imgName}"; }
            if(model.TemplateFile!=null) { var res = await _sec.ScanZipContentAsync(model.TemplateFile); if(res.IsSafe) { var tName = Guid.NewGuid().ToString("N")+".zip"; using var fs = new FileStream(Path.Combine(_env.ContentRootPath,"SecureTemplates",tName), FileMode.Create); await model.TemplateFile.CopyToAsync(fs); p.TemplateFileName = tName; } }
            // 新增額外圖片
            if (model.GalleryImages != null && model.GalleryImages.Count > 0) {
                int sort = p.Images?.Count ?? 0;
                foreach (var img in model.GalleryImages.Take(5)) {
                    if (img == null || img.Length == 0) continue;
                    var gName = Guid.NewGuid().ToString("N") + ".jpg";
                    using var gi = await Image.LoadAsync(img.OpenReadStream()); await gi.SaveAsJpegAsync(Path.Combine(_env.WebRootPath, "uploads", "images", gName), new JpegEncoder { Quality = 80 });
                    _ctx.ProductImages.Add(new ProductImage { ProductId = p.ID, ImageUrl = $"/uploads/images/{gName}", SortOrder = sort++ });
                }
            }
            await _ctx.SaveChangesAsync(); TempData["MessageType"]="success"; TempData["Message"]="更新成功"; return RedirectToAction("Index");
        }

        // 刪除 gallery 圖片
        [HttpPost] public async Task<IActionResult> DeleteGalleryImage(int imageId, int productId) {
            var img = await _ctx.ProductImages.Include(i=>i.Product).FirstOrDefaultAsync(i=>i.Id==imageId && i.Product.UploaderId==CurrentUserId);
            if (img != null) { _ctx.ProductImages.Remove(img); await _ctx.SaveChangesAsync(); }
            return RedirectToAction("Edit", new { id = productId });
        }
    }
}

using System.Security.Claims; using System.Threading.Tasks; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using UIStore.Data; using System.Linq; using UIStore.Services; using System;
namespace UIStore.ViewComponents {
    public class CartCountViewComponent : ViewComponent {
        private readonly ApplicationDbContext _c; private readonly CacheService _cs;
        public CartCountViewComponent(ApplicationDbContext c, CacheService cs) { _c = c; _cs = cs; }
        public async Task<IViewComponentResult> InvokeAsync() { if (!User.Identity.IsAuthenticated) return View(0); var u = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier); return View(await _cs.GetOrSetAsync($"CartCount_{u}", () => _c.CartItems.Where(x => x.UserId == u).SumAsync(x => x.Quantity), TimeSpan.FromMinutes(5))); }
    }
}

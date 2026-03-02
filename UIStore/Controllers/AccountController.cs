using System; using System.Security.Claims; using System.Threading.Tasks; using Microsoft.AspNetCore.Authentication; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Identity; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.RateLimiting; using Microsoft.Extensions.Logging; using UIStore.Models;
namespace UIStore.Controllers {
    [Authorize]
    public class AccountController : BaseController {
        private readonly SignInManager<ApplicationUser> _sm; private readonly UserManager<ApplicationUser> _um; private readonly ILogger<AccountController> _logger;
        public AccountController(SignInManager<ApplicationUser> s, UserManager<ApplicationUser> u, ILogger<AccountController> l) { _sm = s; _um = u; _logger = l; }
        private string MaskEmail(string email) { if (string.IsNullOrEmpty(email) || !email.Contains("@")) return "***"; var parts = email.Split('@'); var name = parts[0]; if (name.Length <= 2) return $"***@{parts[1]}"; return $"{name[0]}***{name[^1]}@{parts[1]}"; }
        [AllowAnonymous] [HttpGet] [EnableRateLimiting("login")] public IActionResult Login(string returnUrl = null) { ViewData["ReturnUrl"] = returnUrl; return View(); }
        [AllowAnonymous] [HttpPost] [EnableRateLimiting("login")] public IActionResult ExternalLogin(string provider, string returnUrl = null) => Challenge(_sm.ConfigureExternalAuthenticationProperties(provider, Url.Action("ExternalLoginCallback", new { returnUrl })), provider);
        [AllowAnonymous] [HttpGet] [EnableRateLimiting("login")] public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null) {
            var safe = Url.IsLocalUrl(returnUrl) ? returnUrl : "/"; if (remoteError != null) return RedirectToAction("Login"); var info = await _sm.GetExternalLoginInfoAsync(); if (info == null) return RedirectToAction("Login");
            var res = await _sm.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false, true); if (res.Succeeded) return LocalRedirect(safe);
            var email = info.Principal.FindFirstValue(ClaimTypes.Email); if(string.IsNullOrEmpty(email)) { _logger.LogWarning($"無效登入: {MaskEmail(email)}"); return RedirectToAction("Login"); }
            var fullName = System.Net.WebUtility.HtmlEncode(info.Principal.FindFirstValue(ClaimTypes.Name) ?? "User");
            var user = await _um.FindByEmailAsync(email) ?? new ApplicationUser { UserName = email, Email = email, FullName = fullName };
            if (user.Id == null) { await _um.CreateAsync(user); await _um.AddToRoleAsync(user, "User"); }
            await _um.AddLoginAsync(user, info); await _sm.SignInAsync(user, false); return LocalRedirect(safe);
        }
        [HttpPost] public async Task<IActionResult> BecomePartner() { var u = await _um.FindByIdAsync(CurrentUserId); if(u!=null && !await _um.IsInRoleAsync(u, "Partner")) { await _um.AddToRoleAsync(u, "Partner"); await _sm.RefreshSignInAsync(u); TempData["MessageType"]="success"; TempData["Message"]="您現在是 UI 夥伴了"; } return RedirectToAction("Index", "Home"); }
        [HttpGet] public async Task<IActionResult> Profile() { return View(await _um.FindByIdAsync(CurrentUserId)); }
        [HttpPost] public async Task<IActionResult> UpdateProfile(string fullName) { var u = await _um.FindByIdAsync(CurrentUserId); if(u!=null) { u.FullName = System.Net.WebUtility.HtmlEncode(fullName); await _um.UpdateAsync(u); TempData["MessageType"]="success"; TempData["Message"]="資料已更新"; } return RedirectToAction("Profile"); }
        [HttpPost] public async Task<IActionResult> Logout() { await _sm.SignOutAsync(); return RedirectToAction("Index", "Home"); }
        [AllowAnonymous] [HttpGet] public IActionResult Lockout() => View();
    }
}

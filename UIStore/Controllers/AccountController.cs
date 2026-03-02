using System; using System.Security.Claims; using System.Threading.Tasks; using System.Net.Mail; using System.Collections.Concurrent; using Microsoft.AspNetCore.Authentication; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Identity; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.RateLimiting; using Microsoft.Extensions.Logging; using UIStore.Models; using DnsClient;
namespace UIStore.Controllers {
    [Authorize]
    public class AccountController : BaseController {
        private readonly SignInManager<ApplicationUser> _sm; private readonly UserManager<ApplicationUser> _um; private readonly ILogger<AccountController> _logger; private readonly IEmailService _email;
        // simple in-memory rate limiter for CheckEmail: tracks requests per IP
        private static readonly ConcurrentDictionary<string, (DateTime windowStart, int count)> _emailCheckRate = new();
        private static readonly TimeSpan _emailCheckWindow = TimeSpan.FromMinutes(1);
        private const int _emailCheckMaxPerWindow = 6;
        public AccountController(SignInManager<ApplicationUser> s, UserManager<ApplicationUser> u, ILogger<AccountController> l, IEmailService email) { _sm = s; _um = u; _logger = l; _email = email; }
        private string MaskEmail(string email) { if (string.IsNullOrEmpty(email) || !email.Contains("@")) return "***"; var parts = email.Split('@'); var name = parts[0]; if (name.Length <= 2) return $"***@{parts[1]}"; return $"{name[0]}***{name[^1]}@{parts[1]}"; }
        [AllowAnonymous] [HttpGet] [EnableRateLimiting("login")] public IActionResult Login(string returnUrl = null) { ViewData["ReturnUrl"] = returnUrl; return View(new Models.LoginViewModel()); }
        [AllowAnonymous] [HttpPost] [ValidateAntiForgeryToken] [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(Models.LoginViewModel model, string returnUrl = null) {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);
            var safe = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
            var result = await _sm.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded) {
                return LocalRedirect(safe);
            }
            if (result.IsLockedOut) {
                return RedirectToAction("Lockout");
            }
            ModelState.AddModelError(string.Empty, "登入失敗：請檢查帳號或密碼");
            return View(model);
        }
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

        [AllowAnonymous] [HttpGet] public IActionResult Register() {
            return View();
        }
        [AllowAnonymous] [HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> Register(Models.RegisterViewModel model) {
            if (!ModelState.IsValid) return View(model);
            var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FullName = System.Net.WebUtility.HtmlEncode(model.FullName ?? "") };
            var res = await _um.CreateAsync(user, model.Password);
            if (res.Succeeded) {
                await _um.AddToRoleAsync(user, "User");
                // generate email confirmation
                var token = await _um.GenerateEmailConfirmationTokenAsync(user);
                var callback = Url.Action("ConfirmEmail","Account", new { userId = user.Id, token = System.Net.WebUtility.UrlEncode(token) }, Request.Scheme);
                await _email.SendEmailAsync(user.Email, "歡迎來到 UI Store - 請驗證您的電子郵件", $"請點擊以下連結以完成註冊：{callback}");
                return View("RegisterSuccess");
            }
            foreach (var err in res.Errors) ModelState.AddModelError(string.Empty, err.Description);
            return View(model);
        }

        [AllowAnonymous] [HttpGet] public IActionResult Lockout() => View();
        [AllowAnonymous] [HttpGet] public IActionResult ForgotPassword() => View();
        [AllowAnonymous] [HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> ForgotPassword(Models.ForgotPasswordViewModel model) {
            if(!ModelState.IsValid) return View(model);
            var user = await _um.FindByEmailAsync(model.Email);
            if(user == null) { TempData["MessageType"]="info"; TempData["Message"]="若該電子郵件已註冊，已收到重設密碼郵件。"; return RedirectToAction("Login"); }
            var token = await _um.GeneratePasswordResetTokenAsync(user);
            var callback = Url.Action("ResetPassword","Account", new { email = user.Email, token = System.Net.WebUtility.UrlEncode(token) }, Request.Scheme);
            await _email.SendEmailAsync(user.Email, "重設密碼", $"請點擊以下連結重設密碼：{callback}");
            TempData["MessageType"]="success"; TempData["Message"]="已寄出重設密碼信件，請檢查您的收件匣。"; return RedirectToAction("Login");
        }

        [AllowAnonymous] [HttpGet] public IActionResult ResetPassword(string email = null, string token = null) {
            if(string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token)) return RedirectToAction("Login");
            return View(new Models.ResetPasswordViewModel { Email = email, Token = token });
        }
        [AllowAnonymous] [HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> ResetPassword(Models.ResetPasswordViewModel model) {
            if(!ModelState.IsValid) return View(model);
            var user = await _um.FindByEmailAsync(model.Email);
            if(user == null) { TempData["MessageType"]="warning"; TempData["Message"]="無此使用者。"; return RedirectToAction("Login"); }
            var res = await _um.ResetPasswordAsync(user, System.Net.WebUtility.UrlDecode(model.Token), model.Password);
            if(res.Succeeded) { TempData["MessageType"]="success"; TempData["Message"]="密碼已重設，請使用新密碼登入。"; return RedirectToAction("Login"); }
            foreach(var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        [AllowAnonymous] [HttpGet] public async Task<IActionResult> ConfirmEmail(string userId, string token) {
            if(string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)) return RedirectToAction("Index","Home");
            var user = await _um.FindByIdAsync(userId);
            if(user == null) return RedirectToAction("Index","Home");
            var res = await _um.ConfirmEmailAsync(user, System.Net.WebUtility.UrlDecode(token));
            if(res.Succeeded) { return View("ConfirmEmail"); }
            return RedirectToAction("Index","Home");
        }
        [AllowAnonymous] [HttpGet]
        public async Task<JsonResult> CheckEmail(string email) {
            if (string.IsNullOrWhiteSpace(email)) return Json(new { available = false });

            // Rate limit by remote IP
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;
            var entry = _emailCheckRate.GetOrAdd(ip, _ => (now, 0));
            if (now - entry.windowStart > _emailCheckWindow) {
                entry = (now, 0);
            }
            if (entry.count >= _emailCheckMaxPerWindow) {
                return Json(new { available = false });
            }
            _emailCheckRate[ip] = (entry.windowStart, entry.count + 1);

            // basic syntactic validation
            try {
                var m = new MailAddress(email);
            } catch {
                return Json(new { available = false });
            }

            // DNS MX lookup for domain
            var domain = email.Split('@')[1];
            bool hasMx = false;
            try {
                var lookup = new LookupClient();
                var result = await lookup.QueryAsync(domain, QueryType.MX);
                if (result.Answers.MxRecords().Count > 0) hasMx = true;
                else {
                    // fallback: check A record
                    var a = await lookup.QueryAsync(domain, QueryType.A);
                    if (a.Answers.ARecords().Count > 0) hasMx = true;
                }
            } catch {
                // if DNS lookup fails, be conservative and disallow
                hasMx = false;
            }

            if (!hasMx) return Json(new { available = false });

            var u = await _um.FindByEmailAsync(email);
            return Json(new { available = u == null });
        }
    }
}

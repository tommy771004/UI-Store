using System; using System.Linq; using System.Security.Cryptography; using System.Text; using System.Threading.Tasks; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Http; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.RateLimiting; using Microsoft.EntityFrameworkCore; using Microsoft.Extensions.Logging; using UIStore.Data; using UIStore.Models; using UIStore.Services; using UIStore.Filters;
namespace UIStore.Controllers {
    [Authorize] public class CheckoutController : BaseController {
        private readonly ApplicationDbContext _ctx; private readonly ECPayService _ec; private readonly LinePayService _lp; private readonly IEmailService _email; private readonly ILogger<CheckoutController> _logger; private readonly CacheService _cs; private readonly CouponService _coupon;
        public CheckoutController(ApplicationDbContext ctx, ECPayService ec, LinePayService lp, IEmailService em, ILogger<CheckoutController> l, CacheService cs, CouponService coupon) { _ctx = ctx; _ec = ec; _lp = lp; _email = em; _logger = l; _cs = cs; _coupon = coupon; }

        [HttpGet] public async Task<IActionResult> Review(string couponCode = null) {
            var cart = await _ctx.CartItems.Include(c=>c.Product).Where(c=>c.UserId==CurrentUserId).ToListAsync();
            if(!cart.Any()) return RedirectToAction("Index", "Cart");
            decimal subtotal = cart.Sum(c=>c.Product.Price * c.Quantity);
            var vm = new OrderReviewViewModel { Items = cart, Subtotal = subtotal, Total = subtotal };
            if (!string.IsNullOrWhiteSpace(couponCode)) {
                var result = await _coupon.ValidateAsync(couponCode, CurrentUserId, subtotal);
                vm.CouponCode = couponCode; vm.CouponMessage = result.Message;
                if (result.IsValid) { vm.DiscountAmount = result.DiscountAmount; vm.Total = subtotal - result.DiscountAmount; }
            }
            return View(vm);
        }

        [HttpPost] public async Task<IActionResult> ValidateCoupon([FromBody] CouponValidateRequest req) {
            var cart = await _ctx.CartItems.Include(c=>c.Product).Where(c=>c.UserId==CurrentUserId).ToListAsync();
            decimal subtotal = cart.Sum(c=>c.Product.Price * c.Quantity);
            var result = await _coupon.ValidateAsync(req?.Code, CurrentUserId, subtotal);
            return Json(result);
        }

        [HttpPost] [EnableRateLimiting("payment")] public async Task<IActionResult> ProcessPayment(string paymentMethod, string couponCode) {
            var cart = await _ctx.CartItems.Include(c=>c.Product).Where(c=>c.UserId==CurrentUserId).ToListAsync(); if(!cart.Any()) return BadRequest();
            decimal subtotal = cart.Sum(c=>c.Product.Price * c.Quantity);
            decimal discount = 0; int? couponId = null;
            if (!string.IsNullOrWhiteSpace(couponCode)) {
                var couponResult = await _coupon.ValidateAsync(couponCode, CurrentUserId, subtotal);
                if (couponResult.IsValid) { discount = couponResult.DiscountAmount; couponId = couponResult.CouponId; }
            }
            decimal total = subtotal - discount;
            var order = new Order { MerchantTradeNo = $"UI{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(100,999)}", UserId = CurrentUserId, TotalAmount = total, PaymentMethod = paymentMethod, PaymentStatus = "Pending", DiscountAmount = discount, CouponId = couponId, OrderItems = cart.Select(c=>new OrderItem { ProductId=c.ProductId, UnitPrice=c.Product.Price, Quantity=c.Quantity }).ToList() };
            _ctx.Orders.Add(order); _ctx.CartItems.RemoveRange(cart); await _ctx.SaveChangesAsync(); _cs.Remove($"CartCount_{CurrentUserId}");
            if (couponId.HasValue) await _coupon.RecordUsageAsync(couponId.Value, CurrentUserId, order.OrderId);
            if(paymentMethod=="LinePay") return Redirect(await _lp.RequestPaymentAsync(order.OrderId, total, "UI Store", Url.Action("LinePayConfirm","Checkout",new{id=order.OrderId},Request.Scheme), Url.Action("PaymentFailed","Checkout",null,Request.Scheme)));
            return Content(_ec.GenerateCheckoutHtml(order.MerchantTradeNo, total, "UI Store", Url.Action("ECPayWebhook","Checkout",null,Request.Scheme), Url.Action("PaymentSuccess","Checkout",null,Request.Scheme)), "text/html");
        }

        [HttpGet] public async Task<IActionResult> LinePayConfirm(string id, string transactionId) {
            var o = await _ctx.Orders.Include(x=>x.User).FirstOrDefaultAsync(x => x.OrderId == id && x.UserId == CurrentUserId); if(o == null) return NotFound(); if (o.PaymentStatus == "Paid") return RedirectToAction("PaymentSuccess");
            if(await _lp.ConfirmPaymentAsync(transactionId, o.TotalAmount)) { o.PaymentStatus = "Paid"; o.TransactionId = transactionId; o.PaidAt = DateTime.UtcNow; await _ctx.SaveChangesAsync(); await _email.SendEmailAsync(o.User.Email, "購買成功通知", $"<h2>感謝您的購買！</h2><p>訂單編號：{o.MerchantTradeNo}</p><p>總金額：NT$ {o.TotalAmount:N0}</p>"); return RedirectToAction("PaymentSuccess"); } return RedirectToAction("PaymentFailed");
        }

        [IgnoreAntiforgeryToken] [AllowAnonymous] [HttpPost] [ValidateIPWhitelist]
        public async Task<IActionResult> ECPayWebhook([FromForm] IFormCollection form) {
            var d = form.Keys.ToDictionary(k => k, k => form[k].ToString()); if (!d.ContainsKey("CheckMacValue")) return Content("0|Error");
            var rMac = d["CheckMacValue"]; d.Remove("CheckMacValue"); var cMac = _ec.ComputeCheckMacValue(d);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(rMac??""), Encoding.UTF8.GetBytes(cMac??""))) return Content("0|Error");
            var mtn = d.ContainsKey("MerchantTradeNo") ? d["MerchantTradeNo"] : ""; var o = await _ctx.Orders.Include(x=>x.User).FirstOrDefaultAsync(x => x.MerchantTradeNo == mtn);
            if (o == null || o.PaymentStatus == "Paid" || decimal.Parse(d["TradeAmt"]) != o.TotalAmount) return Content("1|OK");
            if (d["RtnCode"] == "1") { o.PaymentStatus = "Paid"; o.PaidAt = DateTime.UtcNow; await _ctx.SaveChangesAsync(); await _email.SendEmailAsync(o.User.Email, "購買成功通知", $"<h2>感謝您的購買！</h2><p>訂單編號：{o.MerchantTradeNo}</p><p>總金額：NT$ {o.TotalAmount:N0}</p>"); }
            return Content("1|OK");
        }

        [HttpGet] public IActionResult PaymentSuccess() => View();
        [HttpGet] public IActionResult PaymentFailed() => View();
    }

    public class CouponValidateRequest { public string Code { get; set; } }
}

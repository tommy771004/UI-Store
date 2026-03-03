using System; using System.Linq; using System.Threading.Tasks; using Microsoft.EntityFrameworkCore; using UIStore.Data; using UIStore.Models;
namespace UIStore.Services {
    public class CouponService {
        private readonly ApplicationDbContext _ctx;
        public CouponService(ApplicationDbContext ctx) { _ctx = ctx; }

        public async Task<CouponValidationResult> ValidateAsync(string code, string userId, decimal subtotal) {
            if (string.IsNullOrWhiteSpace(code)) return new CouponValidationResult { IsValid = false, Message = "請輸入優惠碼" };
            var coupon = await _ctx.Coupons.FirstOrDefaultAsync(c => c.Code == code.ToUpper() && c.IsActive);
            if (coupon == null) return new CouponValidationResult { IsValid = false, Message = "優惠碼不存在或已停用" };
            if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTime.UtcNow) return new CouponValidationResult { IsValid = false, Message = "優惠碼已過期" };
            if (coupon.MaxUses.HasValue && coupon.UsedCount >= coupon.MaxUses.Value) return new CouponValidationResult { IsValid = false, Message = "優惠碼已達使用上限" };
            if (subtotal < coupon.MinOrderAmount) return new CouponValidationResult { IsValid = false, Message = $"此優惠碼需訂單金額達 NT${coupon.MinOrderAmount:N0} 才可使用" };
            // 每位用戶只能使用一次
            var alreadyUsed = await _ctx.CouponUsages.AnyAsync(u => u.CouponId == coupon.Id && u.UserId == userId);
            if (alreadyUsed) return new CouponValidationResult { IsValid = false, Message = "您已使用過此優惠碼" };

            decimal discount = coupon.DiscountType == "Percentage"
                ? Math.Round(subtotal * coupon.DiscountValue / 100, 0)
                : coupon.DiscountValue;
            discount = Math.Min(discount, subtotal); // 不能超過訂單金額

            return new CouponValidationResult { IsValid = true, Message = $"折扣 NT${discount:N0}", DiscountAmount = discount, CouponId = coupon.Id };
        }

        public async Task RecordUsageAsync(int couponId, string userId, string orderId) {
            _ctx.CouponUsages.Add(new CouponUsage { CouponId = couponId, UserId = userId, OrderId = orderId });
            var coupon = await _ctx.Coupons.FindAsync(couponId);
            if (coupon != null) coupon.UsedCount++;
            await _ctx.SaveChangesAsync();
        }
    }
}

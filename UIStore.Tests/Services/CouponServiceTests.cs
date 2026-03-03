using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UIStore.Data;
using UIStore.Models;
using UIStore.Services;
using Xunit;

namespace UIStore.Tests.Services {
    public class CouponServiceTests {
        private ApplicationDbContext CreateDb() {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            return new ApplicationDbContext(opts);
        }

        [Fact]
        public async Task ValidateAsync_EmptyCode_ReturnsInvalid() {
            var db = CreateDb();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("", "user1", 500);
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateAsync_NonExistentCode_ReturnsInvalid() {
            var db = CreateDb();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("NOTEXIST", "user1", 500);
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateAsync_ExpiredCoupon_ReturnsInvalid() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "EXPIRED", DiscountType = "Fixed", DiscountValue = 100, IsActive = true, ExpiryDate = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("EXPIRED", "user1", 500);
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateAsync_ValidFixedCoupon_ReturnsCorrectDiscount() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "SAVE100", DiscountType = "Fixed", DiscountValue = 100, IsActive = true });
            await db.SaveChangesAsync();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("SAVE100", "user1", 500);
            Assert.True(result.IsValid);
            Assert.Equal(100, result.DiscountAmount);
        }

        [Fact]
        public async Task ValidateAsync_ValidPercentageCoupon_ReturnsCorrectDiscount() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "SAVE10PCT", DiscountType = "Percentage", DiscountValue = 10, IsActive = true });
            await db.SaveChangesAsync();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("SAVE10PCT", "user1", 1000);
            Assert.True(result.IsValid);
            Assert.Equal(100, result.DiscountAmount); // 10% of 1000
        }

        [Fact]
        public async Task ValidateAsync_MinOrderNotMet_ReturnsInvalid() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "BIGORDER", DiscountType = "Fixed", DiscountValue = 200, IsActive = true, MinOrderAmount = 1000 });
            await db.SaveChangesAsync();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("BIGORDER", "user1", 500);
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateAsync_MaxUsesReached_ReturnsInvalid() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "LIMITED", DiscountType = "Fixed", DiscountValue = 50, IsActive = true, MaxUses = 5, UsedCount = 5 });
            await db.SaveChangesAsync();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("LIMITED", "user1", 300);
            Assert.False(result.IsValid);
        }

        [Fact]
        public async Task ValidateAsync_DiscountCannotExceedSubtotal() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "BIG", DiscountType = "Fixed", DiscountValue = 9999, IsActive = true });
            await db.SaveChangesAsync();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("BIG", "user1", 300);
            Assert.True(result.IsValid);
            Assert.Equal(300, result.DiscountAmount); // capped at subtotal
        }

        [Fact]
        public async Task ValidateAsync_InactiveCoupon_ReturnsInvalid() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "INACTIVE", DiscountType = "Fixed", DiscountValue = 100, IsActive = false });
            await db.SaveChangesAsync();
            var svc = new CouponService(db);
            var result = await svc.ValidateAsync("INACTIVE", "user1", 500);
            Assert.False(result.IsValid);
        }
    }
}

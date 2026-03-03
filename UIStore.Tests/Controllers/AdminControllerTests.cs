using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using UIStore.Controllers;
using UIStore.Data;
using UIStore.Models;
using Xunit;

namespace UIStore.Tests.Controllers {
    public class AdminControllerTests {
        private ApplicationDbContext CreateDb() {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            return new ApplicationDbContext(opts);
        }

        private Mock<UserManager<ApplicationUser>> CreateUserManagerMock() {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }

        [Fact]
        public async Task Index_ReturnsView_WithStats() {
            var db = CreateDb();
            var um = CreateUserManagerMock();
            var ctrl = new AdminController(db, um.Object);

            var result = await ctrl.Index() as ViewResult;

            Assert.NotNull(result);
            Assert.Equal(0, ctrl.ViewBag.TotalUsers);
            Assert.Equal(0, ctrl.ViewBag.TotalOrders);
            Assert.Equal((decimal)0, ctrl.ViewBag.TotalRevenue);
        }

        [Fact]
        public async Task Categories_ReturnsViewWithCategories() {
            var db = CreateDb();
            db.Categories.Add(new Category { Name = "Cat1" });
            await db.SaveChangesAsync();
            var um = CreateUserManagerMock();
            var ctrl = new AdminController(db, um.Object);

            var result = await ctrl.Categories() as ViewResult;

            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateCategory_AddsCategory_AndRedirects() {
            var db = CreateDb();
            var um = CreateUserManagerMock();
            var ctrl = new AdminController(db, um.Object);

            var result = await ctrl.CreateCategory("NewCat") as RedirectToActionResult;

            Assert.NotNull(result);
            Assert.Equal("Categories", result.ActionName);
            Assert.Equal(1, await db.Categories.CountAsync());
        }

        [Fact]
        public async Task CreateCategory_IgnoresEmptyName() {
            var db = CreateDb();
            var um = CreateUserManagerMock();
            var ctrl = new AdminController(db, um.Object);

            await ctrl.CreateCategory("   ");

            Assert.Equal(0, await db.Categories.CountAsync());
        }

        [Fact]
        public async Task Coupons_ReturnsViewWithCoupons() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "TEST10", DiscountType = "Fixed", DiscountValue = 100, IsActive = true });
            await db.SaveChangesAsync();
            var um = CreateUserManagerMock();
            var ctrl = new AdminController(db, um.Object);

            var result = await ctrl.Coupons() as ViewResult;

            Assert.NotNull(result);
        }

        [Fact]
        public async Task ToggleProductStatus_TogglesIsDeleted() {
            var db = CreateDb();
            var cat = new Category { Name = "Cat" };
            db.Categories.Add(cat);
            var prod = new ProductViewModel { Title = "T", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "u1", IsDeleted = false };
            db.Products.Add(prod);
            await db.SaveChangesAsync();
            var um = CreateUserManagerMock();
            var ctrl = new AdminController(db, um.Object);

            await ctrl.ToggleProductStatus(prod.ID);

            var updated = await db.Products.FindAsync(prod.ID);
            Assert.True(updated.IsDeleted);
        }

        [Fact]
        public async Task DeleteCoupon_RemovesCoupon() {
            var db = CreateDb();
            db.Coupons.Add(new Coupon { Code = "DEL", DiscountType = "Fixed", DiscountValue = 50 });
            await db.SaveChangesAsync();
            var coupon = await db.Coupons.FirstAsync();
            var um = CreateUserManagerMock();
            var ctrl = new AdminController(db, um.Object);

            await ctrl.DeleteCoupon(coupon.Id);

            Assert.Equal(0, await db.Coupons.CountAsync());
        }
    }
}

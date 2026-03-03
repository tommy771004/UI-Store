using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UIStore.Controllers;
using UIStore.Data;
using UIStore.Models;
using UIStore.Services;
using Xunit;
using System.Security.Claims;

namespace UIStore.Tests.Controllers {
    public class CartControllerTests {
        private ApplicationDbContext CreateDb() {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            return new ApplicationDbContext(opts);
        }

        private CacheService CreateCache() => new CacheService(new MemoryCache(new MemoryCacheOptions()));

        private CartController CreateController(ApplicationDbContext db, string userId = "user1") {
            var ctrl = new CartController(db, CreateCache());
            ctrl.ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    }, "test"))
                }
            };
            return ctrl;
        }

        [Fact]
        public async Task Index_ReturnsCartItems_ForCurrentUser() {
            var db = CreateDb();
            var cat = new Category { Name = "Test" };
            db.Categories.Add(cat);
            var prod = new ProductViewModel { Title = "T", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "seller1" };
            db.Products.Add(prod);
            await db.SaveChangesAsync();
            db.CartItems.Add(new CartItem { UserId = "user1", ProductId = prod.ID, Quantity = 2 });
            await db.SaveChangesAsync();

            var ctrl = CreateController(db);
            var result = await ctrl.Index() as ViewResult;

            Assert.NotNull(result);
        }

        [Fact]
        public async Task AddToCart_AddsNewItem() {
            var db = CreateDb();
            var cat = new Category { Name = "Test" };
            db.Categories.Add(cat);
            var prod = new ProductViewModel { Title = "T", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "seller1" };
            db.Products.Add(prod);
            await db.SaveChangesAsync();

            var ctrl = CreateController(db);
            var result = await ctrl.AddToCart(prod.ID, 1) as RedirectToActionResult;

            Assert.NotNull(result);
            Assert.Equal("Index", result.ActionName);
            Assert.Equal(1, await db.CartItems.CountAsync(c => c.UserId == "user1"));
        }

        [Fact]
        public async Task AddToCart_IncreasesQuantity_IfAlreadyExists() {
            var db = CreateDb();
            var cat = new Category { Name = "Test" };
            db.Categories.Add(cat);
            var prod = new ProductViewModel { Title = "T", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "seller1" };
            db.Products.Add(prod);
            await db.SaveChangesAsync();
            db.CartItems.Add(new CartItem { UserId = "user1", ProductId = prod.ID, Quantity = 3 });
            await db.SaveChangesAsync();

            var ctrl = CreateController(db);
            await ctrl.AddToCart(prod.ID, 2);

            var item = await db.CartItems.FirstAsync(c => c.UserId == "user1");
            Assert.Equal(5, item.Quantity);
        }

        [Fact]
        public async Task Remove_RemovesItem() {
            var db = CreateDb();
            var cat = new Category { Name = "Test" };
            db.Categories.Add(cat);
            var prod = new ProductViewModel { Title = "T", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "seller1" };
            db.Products.Add(prod);
            await db.SaveChangesAsync();
            db.CartItems.Add(new CartItem { UserId = "user1", ProductId = prod.ID, Quantity = 1 });
            await db.SaveChangesAsync();
            var item = await db.CartItems.FirstAsync();

            var ctrl = CreateController(db);
            var result = await ctrl.Remove(item.Id) as RedirectToActionResult;

            Assert.NotNull(result);
            Assert.Equal(0, await db.CartItems.CountAsync());
        }

        [Fact]
        public async Task Remove_IgnoresItemFromOtherUser() {
            var db = CreateDb();
            var cat = new Category { Name = "Test" };
            db.Categories.Add(cat);
            var prod = new ProductViewModel { Title = "T", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "seller1" };
            db.Products.Add(prod);
            await db.SaveChangesAsync();
            db.CartItems.Add(new CartItem { UserId = "other_user", ProductId = prod.ID, Quantity = 1 });
            await db.SaveChangesAsync();
            var item = await db.CartItems.FirstAsync();

            var ctrl = CreateController(db, "user1");
            await ctrl.Remove(item.Id);

            // item should still be there (belongs to other_user)
            Assert.Equal(1, await db.CartItems.CountAsync());
        }
    }
}

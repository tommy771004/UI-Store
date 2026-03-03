using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UIStore.Controllers;
using UIStore.Data;
using UIStore.Models;
using UIStore.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace UIStore.Tests.Controllers {
    public class HomeControllerTests {
        private ApplicationDbContext CreateDb() {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            return new ApplicationDbContext(opts);
        }

        private CacheService CreateCache() => new CacheService(new MemoryCache(new MemoryCacheOptions()));

        [Fact]
        public async Task Index_NoProducts_ReturnsEmptyModel() {
            var db = CreateDb();
            var ctrl = new HomeController(db, CreateCache());
            var result = await ctrl.Index(null, null) as ViewResult;
            Assert.NotNull(result);
            var model = Assert.IsAssignableFrom<IEnumerable<ProductViewModel>>(result.Model);
            Assert.Empty(model);
        }

        [Fact]
        public async Task Index_WithProducts_ReturnsModel() {
            var db = CreateDb();
            var cat = new Category { Name = "Test" };
            db.Categories.Add(cat);
            db.Products.Add(new ProductViewModel { Title = "Test", Subtitle = "Sub", Price = 100, CategoryId = 1, Category = cat, UploaderId = "user1" });
            await db.SaveChangesAsync();
            var ctrl = new HomeController(db, CreateCache());
            var result = await ctrl.Index(null, null) as ViewResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Details_ProductNotFound_ReturnsNotFound() {
            var db = CreateDb();
            var ctrl = new HomeController(db, CreateCache());
            MockUserContext(ctrl);
            var result = await ctrl.Details(999);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_DeletedProduct_ReturnsNotFound() {
            var db = CreateDb();
            var cat = new Category { Name = "Test" };
            db.Categories.Add(cat);
            db.Products.Add(new ProductViewModel { Title = "Del", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "u1", IsDeleted = true });
            await db.SaveChangesAsync();
            var ctrl = new HomeController(db, CreateCache());
            MockUserContext(ctrl);
            var result = await ctrl.Details(1);
            Assert.IsType<NotFoundResult>(result);
        }

        private void MockUserContext(Controller ctrl) {
            ctrl.ControllerContext = new ControllerContext {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };
        }
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UIStore.Controllers.Api;
using UIStore.Data;
using UIStore.Models;
using Xunit;

namespace UIStore.Tests.Api {
    public class ProductsApiTests {
        private ApplicationDbContext CreateDb() {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            return new ApplicationDbContext(opts);
        }

        [Fact]
        public async Task GetProducts_EmptyDb_ReturnsEmptyList() {
            var db = CreateDb();
            var ctrl = new ProductsApiController(db);
            var result = await ctrl.GetProducts() as OkObjectResult;
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task GetProduct_NotFound_Returns404() {
            var db = CreateDb();
            var ctrl = new ProductsApiController(db);
            var result = await ctrl.GetProduct(999);
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetProduct_DeletedProduct_Returns404() {
            var db = CreateDb();
            var cat = new Category { Name = "Cat" };
            db.Categories.Add(cat);
            db.Products.Add(new ProductViewModel { Title = "Deleted", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "u1", IsDeleted = true });
            await db.SaveChangesAsync();
            var ctrl = new ProductsApiController(db);
            var result = await ctrl.GetProduct(1);
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetProducts_WithKeyword_FiltersCorrectly() {
            var db = CreateDb();
            var cat = new Category { Name = "Cat" };
            db.Categories.Add(cat);
            db.Products.Add(new ProductViewModel { Title = "Findme", Subtitle = "S", Price = 100, CategoryId = 1, Category = cat, UploaderId = "u1" });
            db.Products.Add(new ProductViewModel { Title = "Other", Subtitle = "S", Price = 200, CategoryId = 1, Category = cat, UploaderId = "u1" });
            await db.SaveChangesAsync();
            var ctrl = new ProductsApiController(db);
            var result = await ctrl.GetProducts(keyword: "Findme") as OkObjectResult;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetCategories_ReturnsOk() {
            var db = CreateDb();
            db.Categories.Add(new Category { Name = "Test Category" });
            await db.SaveChangesAsync();
            var ctrl = new ProductsApiController(db);
            var result = await ctrl.GetCategories() as OkObjectResult;
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }
    }
}

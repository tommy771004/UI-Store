using Microsoft.AspNetCore.Identity.EntityFrameworkCore; using Microsoft.EntityFrameworkCore; using UIStore.Models;
namespace UIStore.Data {
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser> {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {}
        public DbSet<SystemSetting> SystemSettings { get; set; } public DbSet<Category> Categories { get; set; } public DbSet<ProductViewModel> Products { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; } public DbSet<Order> Orders { get; set; } public DbSet<OrderItem> OrderItems { get; set; } 
        public DbSet<CartItem> CartItems { get; set; } public DbSet<WishlistItem> Wishlists { get; set; } 
        protected override void OnModelCreating(ModelBuilder builder) {
            base.OnModelCreating(builder);
            builder.Entity<ProductViewModel>().HasIndex(p => new { p.CategoryId, p.IsDeleted });
            builder.Entity<Order>().HasIndex(o => new { o.UserId, o.PaymentStatus });
        }
    }
}

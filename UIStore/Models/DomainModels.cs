using System; using System.Collections.Generic; using System.ComponentModel.DataAnnotations; using System.ComponentModel.DataAnnotations.Schema; using Microsoft.AspNetCore.Http; using Microsoft.AspNetCore.Identity;
namespace UIStore.Models {
    public class SystemSetting { [Key][StringLength(100)] public string Key { get; set; } [Required] public string Value { get; set; } [StringLength(200)] public string Description { get; set; } public DateTime LastUpdated { get; set; } = DateTime.UtcNow; }
    public class ApplicationUser : IdentityUser { [StringLength(50)] public string FullName { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
    public class Category { [Key] public int Id { get; set; } [Required][StringLength(50)] public string Name { get; set; } public ICollection<ProductViewModel> Products { get; set; } }

    public class ProductViewModel {
        [Key] public int ID { get; set; }
        [Required][StringLength(100)] public string Title { get; set; }
        [Required][StringLength(200)] public string Subtitle { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        [Column(TypeName="decimal(18,2)")] public decimal Price { get; set; }
        public bool IsNew { get; set; }
        [StringLength(255)] public string TemplateFileName { get; set; } = "dummy.zip";
        public int CategoryId { get; set; } [ForeignKey("CategoryId")] public Category Category { get; set; }
        public string UploaderId { get; set; } [ForeignKey("UploaderId")] public ApplicationUser Uploader { get; set; }
        public bool IsDeleted { get; set; } = false;
        public int SalesCount { get; set; } = 0; public double AverageRating { get; set; } = 0; public int ReviewCount { get; set; } = 0;
        public ICollection<ProductReview> Reviews { get; set; }
        public ICollection<ProductImage> Images { get; set; }
    }

    // 商品多圖
    public class ProductImage {
        [Key] public int Id { get; set; }
        public int ProductId { get; set; } [ForeignKey("ProductId")] public ProductViewModel Product { get; set; }
        [Required] public string ImageUrl { get; set; }
        public int SortOrder { get; set; } = 0;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    // 優惠券
    public class Coupon {
        [Key] public int Id { get; set; }
        [Required][StringLength(50)] public string Code { get; set; }
        [StringLength(100)] public string Description { get; set; }
        [Required][StringLength(20)] public string DiscountType { get; set; } // "Percentage" | "Fixed"
        [Column(TypeName="decimal(18,2)")] public decimal DiscountValue { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? MaxUses { get; set; }
        public int UsedCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        [Column(TypeName="decimal(18,2)")] public decimal MinOrderAmount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<CouponUsage> Usages { get; set; }
    }

    // 優惠券使用記錄
    public class CouponUsage {
        [Key] public int Id { get; set; }
        public int CouponId { get; set; } [ForeignKey("CouponId")] public Coupon Coupon { get; set; }
        [Required] public string UserId { get; set; } [ForeignKey("UserId")] public ApplicationUser User { get; set; }
        [Required] public string OrderId { get; set; } [ForeignKey("OrderId")] public Order Order { get; set; }
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProductReview { [Key] public int Id { get; set; } public int ProductId { get; set; } [ForeignKey("ProductId")] public ProductViewModel Product { get; set; } public string UserId { get; set; } [ForeignKey("UserId")] public ApplicationUser User { get; set; } [Range(1, 5)] public int Rating { get; set; } [StringLength(500)] public string Comment { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
    public class CartItem { [Key] public int Id { get; set; } [Required] public string UserId { get; set; } [ForeignKey("UserId")] public ApplicationUser User { get; set; } public int ProductId { get; set; } [ForeignKey("ProductId")] public ProductViewModel Product { get; set; } public int Quantity { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
    public class WishlistItem { [Key] public int Id { get; set; } [Required] public string UserId { get; set; } [ForeignKey("UserId")] public ApplicationUser User { get; set; } public int ProductId { get; set; } [ForeignKey("ProductId")] public ProductViewModel Product { get; set; } public DateTime AddedAt { get; set; } = DateTime.UtcNow; }
    public class Order { [Key] public string OrderId { get; set; } = Guid.NewGuid().ToString(); [StringLength(20)] public string MerchantTradeNo { get; set; } [StringLength(50)] public string TransactionId { get; set; } public string UserId { get; set; } [ForeignKey("UserId")] public ApplicationUser User { get; set; } [Column(TypeName="decimal(18,2)")] public decimal TotalAmount { get; set; } public string PaymentMethod { get; set; } public string PaymentStatus { get; set; } public DateTime OrderDate { get; set; } = DateTime.UtcNow; public DateTime? PaidAt { get; set; } public ICollection<OrderItem> OrderItems { get; set; }
        // 優惠券欄位
        public int? CouponId { get; set; } [ForeignKey("CouponId")] public Coupon Coupon { get; set; }
        [Column(TypeName="decimal(18,2)")] public decimal DiscountAmount { get; set; } = 0;
    }
    public class OrderItem { [Key] public int Id { get; set; } [Required] public string OrderId { get; set; } [ForeignKey("OrderId")] public Order Order { get; set; } public int ProductId { get; set; } [ForeignKey("ProductId")] public ProductViewModel Product { get; set; } [Column(TypeName="decimal(18,2)")] public decimal UnitPrice { get; set; } public int Quantity { get; set; } }
    public class PartnerDashboardViewModel { public ProductViewModel Product { get; set; } public int SalesCount { get; set; } public decimal TotalRevenue { get; set; } }
    public class UploadUIViewModel { [Required][StringLength(100)] public string Title { get; set; } [Required][StringLength(200)] public string Subtitle { get; set; } [Required] public string Description { get; set; } [Required][Range(0, 100000)] public decimal Price { get; set; } [Required] public int CategoryId { get; set; } [Required] public IFormFile CoverImage { get; set; } [Required] public IFormFile TemplateFile { get; set; } public List<IFormFile> GalleryImages { get; set; } }
    public class EditUIViewModel { public int ID { get; set; } [Required][StringLength(100)] public string Title { get; set; } [Required][StringLength(200)] public string Subtitle { get; set; } [Required] public string Description { get; set; } [Required][Range(0, 100000)] public decimal Price { get; set; } [Required] public int CategoryId { get; set; } public IFormFile CoverImage { get; set; } public IFormFile TemplateFile { get; set; } public List<IFormFile> GalleryImages { get; set; } }
    public class OrderReviewViewModel { public IEnumerable<CartItem> Items { get; set; } public decimal Subtotal { get; set; } public decimal ShippingFee { get; set; } public decimal Tax { get; set; } public decimal Total { get; set; } public string CouponCode { get; set; } public decimal DiscountAmount { get; set; } = 0; public string CouponMessage { get; set; } }

    // 優惠券驗證 ViewModel
    public class CouponValidationResult { public bool IsValid { get; set; } public string Message { get; set; } public decimal DiscountAmount { get; set; } public int? CouponId { get; set; } }

    // view model used for user registration
    public class RegisterViewModel {
        [Required][EmailAddress] public string Email { get; set; }
        [Required][StringLength(100, MinimumLength = 6)] [DataType(DataType.Password)] public string Password { get; set; }
        [Required][Compare("Password", ErrorMessage = "密碼與確認密碼不一致")] [DataType(DataType.Password)] public string ConfirmPassword { get; set; }
        [StringLength(50)] public string FullName { get; set; }
    }

    public class ForgotPasswordViewModel {
        [Required][EmailAddress] public string Email { get; set; }
    }

    public class ResetPasswordViewModel {
        [Required][EmailAddress] public string Email { get; set; }
        [Required] public string Token { get; set; }
        [Required][StringLength(100, MinimumLength = 6)] [DataType(DataType.Password)] public string Password { get; set; }
        [Required][Compare("Password", ErrorMessage = "密碼與確認密碼不一致")] [DataType(DataType.Password)] public string ConfirmPassword { get; set; }
    }

    public class LoginViewModel {
        [Required][EmailAddress] public string Email { get; set; }
        [Required][DataType(DataType.Password)] public string Password { get; set; }
        public bool RememberMe { get; set; }
    }

    // Admin 優惠券 ViewModel
    public class CreateCouponViewModel {
        [Required][StringLength(50)] public string Code { get; set; }
        [StringLength(100)] public string Description { get; set; }
        [Required] public string DiscountType { get; set; }
        [Required][Range(0.01, 100000)] public decimal DiscountValue { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? MaxUses { get; set; }
        [Range(0, 100000)] public decimal MinOrderAmount { get; set; } = 0;
    }

    // Partner 銷售圖表 ViewModel
    public class PartnerSalesChartViewModel { public string Label { get; set; } public decimal Revenue { get; set; } public int Sales { get; set; } }
}

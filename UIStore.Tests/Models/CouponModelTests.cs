using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using UIStore.Models;
using Xunit;

namespace UIStore.Tests.Models {
    public class CouponModelTests {
        private IList<ValidationResult> Validate(object model) {
            var ctx = new ValidationContext(model);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, ctx, results, true);
            return results;
        }

        [Fact]
        public void Coupon_ValidModel_PassesValidation() {
            var c = new Coupon { Code = "TEST10", DiscountType = "Fixed", DiscountValue = 100 };
            var errs = Validate(c);
            Assert.Empty(errs);
        }

        [Fact]
        public void Coupon_MissingCode_FailsValidation() {
            var c = new Coupon { Code = null, DiscountType = "Fixed", DiscountValue = 100 };
            var errs = Validate(c);
            Assert.NotEmpty(errs);
        }

        [Fact]
        public void CreateCouponViewModel_ValidPercentage_PassesValidation() {
            var vm = new CreateCouponViewModel { Code = "PCT20", DiscountType = "Percentage", DiscountValue = 20 };
            var errs = Validate(vm);
            Assert.Empty(errs);
        }

        [Fact]
        public void ProductImage_RequiresImageUrl() {
            var img = new ProductImage { ProductId = 1, ImageUrl = null };
            var errs = Validate(img);
            Assert.NotEmpty(errs);
        }

        [Fact]
        public void ProductImage_ValidModel_PassesValidation() {
            var img = new ProductImage { ProductId = 1, ImageUrl = "/uploads/images/test.jpg" };
            var errs = Validate(img);
            Assert.Empty(errs);
        }
    }
}

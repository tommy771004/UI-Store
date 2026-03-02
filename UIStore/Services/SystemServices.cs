using System; using System.Collections.Generic; using System.IO; using System.IO.Compression; using System.Linq; using System.Security.Cryptography; using System.Text; using System.Threading; using System.Threading.Tasks; using System.Web; using Microsoft.AspNetCore.Http; using Microsoft.EntityFrameworkCore; using Microsoft.Extensions.Caching.Memory; using Microsoft.Extensions.Configuration; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.Hosting; using Microsoft.Extensions.Logging; using UIStore.Data; using UIStore.Models;
namespace UIStore.Services {
    public class CacheService {
        private readonly IMemoryCache _c; public CacheService(IMemoryCache c) { _c = c; }
        public async Task<T> GetOrSetAsync<T>(string k, Func<Task<T>> f, TimeSpan e) { if (_c.TryGetValue(k, out T v)) return v; v = await f(); _c.Set(k, v, new MemoryCacheEntryOptions().SetAbsoluteExpiration(e)); return v; }
        public void Remove(string k) => _c.Remove(k);
    }
    public class SystemSettingService {
        private readonly IServiceProvider _sp; private readonly CacheService _cs; public SystemSettingService(IServiceProvider sp, CacheService cs) { _sp = sp; _cs = cs; }
        public async Task<T> GetSettingAsync<T>(string k, T def) { return await _cs.GetOrSetAsync($"Set_{k}", async () => { using var sc = _sp.CreateScope(); var ctx = sc.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var s = await ctx.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == k); if (s != null) { try { return typeof(T) == typeof(string[]) ? (T)(object)s.Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).ToArray() : (T)Convert.ChangeType(s.Value, typeof(T)); } catch { return def; } } return def; }, TimeSpan.FromMinutes(30)); }
    }
    public class FileSecurityService {
        private readonly SystemSettingService _set; private static readonly byte[] J = {0xFF,0xD8,0xFF}; private static readonly byte[] P = {0x89,0x50,0x4E,0x47}; private static readonly byte[] Z = {0x50,0x4B,0x03,0x04};
        public FileSecurityService(SystemSettingService s) { _set = s; }
        public bool IsValidImageFile(IFormFile f) { if (f.Length < 4) return false; using var r = new BinaryReader(f.OpenReadStream()); var h = r.ReadBytes(4); return h.Take(3).SequenceEqual(J) || h.Take(4).SequenceEqual(P); }
        public async Task<(bool IsSafe, bool IsMalicious, string ErrorMessage)> ScanZipContentAsync(IFormFile f) {
            var ms = (await _set.GetSettingAsync("Security_MaxZipSizeMB", 500)) * 1024 * 1024; var me = await _set.GetSettingAsync("Security_MaxZipEntries", 5000); var de = await _set.GetSettingAsync("Security_DangerousExts", new[] { ".exe", ".dll", ".bat", ".php" });
            try { using var s = f.OpenReadStream(); using var a = new ZipArchive(s, ZipArchiveMode.Read); long ts = 0; int ec = 0; foreach (var e in a.Entries) { ec++; if (ec > me) return (false, false, "Too many files"); if (e.FullName.Contains("..") || e.FullName.StartsWith("/")) return (false, true, "Invalid path"); var ext = Path.GetExtension(e.Name).ToLowerInvariant(); if (de.Contains(ext)) return (false, true, "Dangerous file"); if (e.Length > 100*1024*1024) return (false, false, "File too large"); ts += e.Length; if (ts > ms) return (false, false, "Total size too large"); if (e.CompressedLength > 0 && ((double)e.Length / e.CompressedLength) > 100) return (false, true, "Zip Bomb"); } return ec == 0 ? (false, false, "Empty") : (true, false, "OK"); } catch { return (false, false, "Error"); }
        }
    }
    public class ECPayService {
        private readonly IConfiguration _cfg; public ECPayService(IConfiguration c) { _cfg = c; }
        public string GenerateCheckoutHtml(string oid, decimal amt, string item, string ret, string cb) {
            var p = new Dictionary<string, string> { {"MerchantID", _cfg["ECPay:MerchantID"]??"2000132"}, {"MerchantTradeNo", oid}, {"MerchantTradeDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}, {"PaymentType", "aio"}, {"TotalAmount", ((int)amt).ToString()}, {"TradeDesc", "Order"}, {"ItemName", item}, {"ReturnURL", ret}, {"ClientBackURL", cb}, {"ChoosePayment", "Credit"}, {"EncryptType", "1"} }; p["CheckMacValue"] = ComputeCheckMacValue(p);
            var sb = new StringBuilder(); sb.Append($"<form id='ecpay-form' action='{_cfg["ECPay:PaymentUrl"]??"https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5"}' method='POST'>"); foreach (var k in p) sb.Append($"<input type='hidden' name='{k.Key}' value='{k.Value}' />"); sb.Append("</form><script>document.getElementById('ecpay-form').submit();</script>"); return sb.ToString();
        }
        public string ComputeCheckMacValue(Dictionary<string, string> p) {
            var s = $"HashKey={_cfg["ECPay:HashKey"]??"5294y06JbISpM5x9"}&" + string.Join("&", p.OrderBy(x=>x.Key).Select(x => $"{x.Key}={x.Value}")) + $"&HashIV={_cfg["ECPay:HashIV"]??"v77hoKGq4kWxNNIS"}";
            var e = HttpUtility.UrlEncode(s).ToLower().Replace("%2d","-").Replace("%5f","_").Replace("%2e",".").Replace("%21","!").Replace("%2a","*").Replace("%28","(").Replace("%29",")"); return BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(e))).Replace("-", "").ToUpper();
        }
    }
    public class LinePayService { public async Task<string> RequestPaymentAsync(string oid, decimal a, string p, string r, string c) => "https://sandbox-web-pay.line.me/web/payment/wait"; public async Task<bool> ConfirmPaymentAsync(string t, decimal a) => true; }
    public interface IEmailService { Task SendEmailAsync(string to, string sub, string html); }
    public class MockEmailService : IEmailService { private readonly ILogger<MockEmailService> _l; public MockEmailService(ILogger<MockEmailService> l) { _l = l; } public Task SendEmailAsync(string to, string sub, string html) { _l.LogInformation($"[Email Sent] To: {to} | Sub: {sub}"); return Task.CompletedTask; } }
    
    public class ProductStatsUpdateService : BackgroundService {
        private readonly IServiceProvider _sp; private readonly ILogger<ProductStatsUpdateService> _logger;
        public ProductStatsUpdateService(IServiceProvider sp, ILogger<ProductStatsUpdateService> l) { _sp = sp; _logger = l; }
        protected override async Task ExecuteAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    using var scope = _sp.CreateScope(); var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await ctx.Database.ExecuteSqlRawAsync(@"UPDATE ""Products"" p SET ""SalesCount"" = COALESCE(sales.TotalSales, 0), ""AverageRating"" = COALESCE(reviews.AvgRating, 0), ""ReviewCount"" = COALESCE(reviews.RevCount, 0) FROM (SELECT ""ProductId"", SUM(""Quantity"") AS TotalSales FROM ""OrderItems"" oi INNER JOIN ""Orders"" o ON oi.""OrderId"" = o.""OrderId"" WHERE o.""PaymentStatus"" = 'Paid' GROUP BY ""ProductId"") sales, (SELECT ""ProductId"", AVG(CAST(""Rating"" AS FLOAT)) AS AvgRating, COUNT(*) AS RevCount FROM ""ProductReviews"" GROUP BY ""ProductId"") reviews WHERE p.""ID"" = sales.""ProductId"" AND p.""ID"" = reviews.""ProductId"";", token);
                } catch(Exception ex) { _logger.LogError(ex, "更新統計失敗"); }
                await Task.Delay(TimeSpan.FromMinutes(15), token);
            }
        }
    }

    public class DatabaseCleanupService : BackgroundService {
        private readonly IServiceProvider _sp; private readonly ILogger<DatabaseCleanupService> _logger;
        public DatabaseCleanupService(IServiceProvider sp, ILogger<DatabaseCleanupService> l) { _sp = sp; _logger = l; }
        protected override async Task ExecuteAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    using var scope = _sp.CreateScope(); var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var ec = await ctx.CartItems.Where(c => c.CreatedAt < DateTime.UtcNow.AddDays(-7)).ToListAsync(token); if (ec.Any()) { ctx.CartItems.RemoveRange(ec); }
                    var eo = await ctx.Orders.Include(o => o.OrderItems).Where(o => o.PaymentStatus == "Pending" && o.OrderDate < DateTime.UtcNow.AddHours(-24)).ToListAsync(token); if (eo.Any()) { ctx.Orders.RemoveRange(eo); }
                    await ctx.SaveChangesAsync(token);
                } catch (Exception ex) { _logger.LogError(ex, "資料庫清理失敗"); }
                await Task.Delay(TimeSpan.FromHours(1), token);
            }
        }
    }
}

using System; using System.IO; using System.Linq; using System.Text; using Microsoft.AspNetCore.Authentication.JwtBearer; using Microsoft.AspNetCore.Builder; using Microsoft.AspNetCore.Http; using Microsoft.AspNetCore.Identity; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.FileProviders; using Microsoft.Extensions.Hosting; using Microsoft.Extensions.Logging; using Microsoft.IdentityModel.Tokens; using UIStore.Data; using UIStore.Models; using UIStore.Services; using UIStore.Filters; using Microsoft.AspNetCore.ResponseCompression; using System.IO.Compression; using Microsoft.AspNetCore.RateLimiting;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCompression(options => { options.EnableForHttps = true; options.Providers.Add<BrotliCompressionProvider>(); options.Providers.Add<GzipCompressionProvider>(); options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "image/svg+xml" }); });
builder.Services.Configure<BrotliCompressionProviderOptions>(options => { options.Level = CompressionLevel.Fastest; });
builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 500*1024*1024; o.Limits.MinRequestBodyDataRate = new Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate(100, TimeSpan.FromSeconds(10)); });
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => { o.ValueCountLimit = 100; o.ValueLengthLimit = 1024 * 1024 * 4; o.MultipartBodyLengthLimit = 500 * 1024 * 1024; });

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<SystemSettingService>();
builder.Services.AddScoped<FileSecurityService>();
builder.Services.AddScoped<CouponService>();
builder.Services.AddTransient<ECPayService>();

// Email 服務：有設定 SMTP 就用真實的，否則用 Mock
var smtpHost = builder.Configuration["Smtp:Host"];
if (!string.IsNullOrWhiteSpace(smtpHost))
    builder.Services.AddTransient<IEmailService, SmtpEmailService>();
else
    builder.Services.AddTransient<IEmailService, MockEmailService>();

builder.Services.AddHostedService<ProductStatsUpdateService>(); builder.Services.AddHostedService<DatabaseCleanupService>();
builder.Services.AddHttpClient<LinePayService>(client => { client.Timeout = TimeSpan.FromSeconds(10); }).AddStandardResilienceHandler(options => { options.Retry.MaxRetryAttempts = 3; options.CircuitBreaker.FailureRatio = 0.5; });
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o => { o.Password.RequiredLength = 8; o.User.RequireUniqueEmail = true; }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o => { o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict; o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/Lockout"; });
builder.Services.AddAntiforgery(o => { o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict; o.SuppressXFrameOptionsHeader = true; });

// JWT 認證（供 API 使用）
var jwtKey = builder.Configuration["Jwt:Key"] ?? "UIStore_DefaultSecretKey_2026_ChangeThisInProduction!";
builder.Services.AddAuthentication()
    .AddGoogle(o=>{o.ClientId="x";o.ClientSecret="x";})
    .AddFacebook(o=>{o.AppId="x";o.AppSecret="x";})
    .AddJwtBearer("ApiJwt", o => {
        o.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false, ValidateAudience = false, ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "UI Store Developer API", Version = "v1", Description = "RESTful API with JWT Authentication" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme { In = Microsoft.OpenApi.Models.ParameterLocation.Header, Description = "請輸入 JWT Token (格式: Bearer {token})", Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement {{ new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }}, Array.Empty<string>() }});
});

builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o => { o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto; o.KnownNetworks.Clear(); o.KnownProxies.Clear(); });
builder.Services.AddHostFiltering(o => { o.AllowedHosts = new[] { "your-uistore.com", "localhost" }; });
builder.Services.AddRateLimiter(o => { o.AddFixedWindowLimiter("login", opt => { opt.Window = TimeSpan.FromMinutes(1); opt.PermitLimit = 5; }); o.AddFixedWindowLimiter("payment", opt => { opt.Window = TimeSpan.FromMinutes(1); opt.PermitLimit = 3; }); o.AddSlidingWindowLimiter("upload", opt => { opt.Window = TimeSpan.FromHours(1); opt.PermitLimit = 10; opt.SegmentsPerWindow = 4; }); o.OnRejected = async (context, token) => { context.HttpContext.Response.StatusCode = 429; context.HttpContext.Response.ContentType = "text/plain; charset=utf-8"; await context.HttpContext.Response.WriteAsync("請求過於頻繁，請稍後再試。", token); }; });
builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; o.Preload = true; });
builder.Services.AddControllersWithViews(o => { o.Filters.Add<GlobalSecurityFilter>(); o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); });
builder.Logging.ClearProviders(); builder.Logging.AddConsole(); builder.Logging.AddDebug(); builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

var app = builder.Build();
app.UseForwardedHeaders();
var env = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
Directory.CreateDirectory(Path.Combine(env.ContentRootPath, "SecureTemplates")); Directory.CreateDirectory(Path.Combine(env.WebRootPath, "uploads", "images"));

using (var scope = app.Services.CreateScope()) {
    var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    ctx.Database.EnsureCreated();
    var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(); foreach (var r in new[]{"Admin","Partner","User"}) if (!rm.RoleExistsAsync(r).GetAwaiter().GetResult()) rm.CreateAsync(new IdentityRole(r)).GetAwaiter().GetResult();
    var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (um.FindByEmailAsync("admin@uistore.com").GetAwaiter().GetResult() == null) { var a = new ApplicationUser { UserName = "admin@uistore.com", Email = "admin@uistore.com", FullName = "Admin" }; var pwd = System.IO.File.Exists("ADMIN_CREDENTIALS.txt") ? System.IO.File.ReadAllText("ADMIN_CREDENTIALS.txt").Split('\n').FirstOrDefault(x => x.Contains("密碼:"))?.Split(':')[1].Trim() ?? "TempAdmin123!" : "TempAdmin123!"; if (um.CreateAsync(a, pwd).GetAwaiter().GetResult().Succeeded) um.AddToRoleAsync(a, "Admin").GetAwaiter().GetResult(); }
    if (!ctx.Categories.Any()) { ctx.Categories.AddRange(new Category{Name="電商模板"}, new Category{Name="後台儀表板"}); ctx.SaveChanges(); }
    if (!ctx.Products.Any()) {
        ctx.Products.Add(new ProductViewModel { Title="UI Store 旗艦版", Subtitle="最高資安與效能架構示範", Description="這是一段詳細說明，包含完整的前後端設計。", Price=990, CategoryId=ctx.Categories.First().Id, UploaderId=um.FindByEmailAsync("admin@uistore.com").GetAwaiter().GetResult().Id, ImageUrl="/images/dummy.jpg", TemplateFileName="dummy.zip", IsNew=true }); ctx.SaveChanges();
    }
}

app.UseResponseCompression();
if (app.Environment.IsDevelopment()) { app.UseDeveloperExceptionPage(); app.UseSwagger(); app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "UI Store API v1")); } else { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.Use(async (context, next) => { context.Response.Headers.Append("X-Frame-Options", "DENY"); context.Response.Headers.Append("X-Content-Type-Options", "nosniff"); context.Response.Headers.Append("X-XSS-Protection", "1; mode=block"); context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin"); context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; img-src 'self' data: https:; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline'; form-action 'self' https://payment-stage.ecpay.com.tw https://sandbox-web-pay.line.me;"); context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()"); await next(); });
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = ctx => { ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000"); } });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(env.WebRootPath, "uploads", "images")), RequestPath = "/uploads/images", OnPrepareResponse = c => { c.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff"); c.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000"); if(Path.GetExtension(c.File.Name).ToLower() != ".jpg") c.Context.Response.ContentType = "application/octet-stream"; } });
app.UseRouting(); app.UseHostFiltering(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

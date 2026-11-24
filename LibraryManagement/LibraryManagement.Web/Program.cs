using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LibraryManagement.Web.Data;
using LibraryManagement.Web.Options;
using LibraryManagement.Web.Services;

MongoConventions.Register();   // phải gọi trước khi dùng collection

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Cấu hình
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<LibraryRules>(builder.Configuration.GetSection("LibraryRules"));

// MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ConnectionString));
builder.Services.AddSingleton<MongoDbContext>();

// Service (đăng ký sẵn TẤT CẢ để B không phải sửa file này)
builder.Services.AddScoped<CounterService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ReaderService>();
builder.Services.AddScoped<LoanService>();
builder.Services.AddScoped<BookService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<FavoriteService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<ReportService>();
// LoanService và ReservationService gọi lẫn nhau: LoanService nhận Lazy<ReservationService> để tránh vòng phụ thuộc.
builder.Services.AddScoped(sp => new Lazy<ReservationService>(() => sp.GetRequiredService<ReservationService>()));
// Tải ảnh bìa từ đường dẫn (BookService). Tắt tự chuyển hướng để BookService kiểm tra từng địa chỉ trước khi theo.
builder.Services.AddHttpClient(BookService.CoverHttpClient, c =>
    {
        c.Timeout = TimeSpan.FromSeconds(15);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("LibraryManagement/1.0");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
// Chạy nền: hủy đặt giữ quá hạn nhận, trả sách về kho
builder.Services.AddHostedService<HoldExpiryService>();

// Đăng nhập bằng cookie, phân quyền theo role Librarian / Reader
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.Name = ".LibraryManagement.Auth";
        o.Cookie.HttpOnly = true;

        // Làm mới claim CardNotice của độc giả trong phiên (BUG-10, mục 6.3 thiết kế kỹ thuật):
        // tối đa 1 lần mỗi phút đọc lại thẻ; Thủ thư vừa khóa / mở khóa / gia hạn thì thanh cảnh báo đổi theo.
        o.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async ctx =>
            {
                if (ctx.Principal?.Identity is not ClaimsIdentity identity) return;
                var readerId = identity.FindFirst("ReaderId")?.Value;
                if (readerId == null) return;   // chỉ áp dụng cho độc giả

                var checkedAt = identity.FindFirst("CardCheckedAt")?.Value;
                if (long.TryParse(checkedAt, out var ticks)
                    && DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) < TimeSpan.FromMinutes(1))
                    return;

                var auth = ctx.HttpContext.RequestServices.GetRequiredService<AuthService>();
                var notice = await auth.GetCardNoticeAsync(readerId);

                foreach (var c in identity.FindAll(c => c.Type is "CardNotice" or "CardCheckedAt").ToList())
                    identity.RemoveClaim(c);
                if (!string.IsNullOrEmpty(notice))
                    identity.AddClaim(new Claim("CardNotice", notice));
                identity.AddClaim(new Claim("CardCheckedAt", DateTime.UtcNow.Ticks.ToString()));

                ctx.ReplacePrincipal(new ClaimsPrincipal(identity));
                ctx.ShouldRenew = true;   // cấp lại cookie chứa claim mới
            }
        };
    });

var app = builder.Build();

// Cấu hình pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DbInitializer.InitializeAsync(app.Services);   // tạo index + seed

app.Run();

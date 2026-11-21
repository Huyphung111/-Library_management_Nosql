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
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<ReportService>();
// LoanService và ReservationService gọi lẫn nhau: LoanService nhận Lazy<ReservationService> để tránh vòng phụ thuộc.
builder.Services.AddScoped(sp => new Lazy<ReservationService>(() => sp.GetRequiredService<ReservationService>()));

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

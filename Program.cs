using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore; // để dùng UseMySql
using WedNightFury.Models; // namespace chứa AppDbContext
using QuestPDF.Infrastructure; // ✅ thêm dòng này để dùng LicenseType

var builder = WebApplication.CreateBuilder(args);

// ⚙️ Cấu hình license miễn phí cho QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// Thêm MVC và Session
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

// Đăng ký HttpContextAccessor để dùng trong _Layout.cshtml
builder.Services.AddHttpContextAccessor();

// Đăng ký AppDbContext với DI container
// 👉 Dùng MySQL (Laragon mặc định)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // ⭐ KÍCH HOẠT SESSION

app.UseAuthorization();

// Route mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

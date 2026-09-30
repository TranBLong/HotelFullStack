using HotelBlazor.Components;
using HotelBlazor.Data; // Thay bằng namespace DbContext của bạn
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình chuỗi kết nối PostgreSQL (phải nằm trước builder.Build())
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Cấu hình Blazor Interactive Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// =========================================================
// BUILD APPLICATION
// =========================================================
var app = builder.Build(); 

// 2. ĐẶT ĐOẠN CODE CỦA BẠN NGAY TẠI ĐÂY (Trước khi ứng dụng lắng nghe request)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    // Tự động áp dụng Migration vào PostgreSQL
    await dbContext.Database.MigrateAsync();

    // Lấy thông tin Seed Admin từ Configuration
    var adminEmail = app.Configuration["ADMIN_EMAIL"];
    var adminPassword = app.Configuration["ADMIN_PASSWORD"];

    // Logic kiểm tra và dùng UserManager/PasswordHasher để tạo tài khoản Admin
}

// 3. Cấu hình HTTP Request Pipeline & Routing
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
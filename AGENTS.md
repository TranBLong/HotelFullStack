# Quy tắc dự án HotelFullStack (dành cho AI agent)

## Công nghệ
- ASP.NET Core Blazor Web App **chỉ dùng Interactive Server** (render mode Server). Target framework net10.0.
- **Không dùng Interactive WebAssembly**, không giữ dự án `HotelBlazor.Client`. Chỉ còn một dự án server (`HotelBlazor`).
- Lý do: WASM chạy trong trình duyệt không gọi được Service → DbContext trực tiếp; dùng Server giữ kiến trúc đơn giản Component → Service → DbContext và tránh over-engineering.
- **Chỉ dùng GitHub, Docker Hub, Render. Không thêm dịch vụ khác.**
- Database: SQLite (chấp nhận mất dữ liệu khi redeploy trên Render free), hoặc PostgreSQL của Render (cần duyệt package Npgsql trước khi dùng).
- Xác thực: ASP.NET Core Identity với **Cookie Authentication**. **Không dùng JWT**.
- Giao diện: Blazor Components + Bootstrap (có sẵn trong template). Không cài thêm thư viện UI lớn nếu chưa được duyệt.
- Chỉ dùng C# và Razor. Comment code bằng tiếng Việt ngắn gọn khi cần giải thích logic nghiệp vụ.
- Package đã được duyệt thêm (nếu dùng): `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (để PersistKeysToDbContext).

## Kiến trúc
- Tách rõ lớp:
  - `Models/` hoặc `Entities/`: entity (ApplicationUser, Room, RoomType, Booking…).
  - `Data/`: DbContext, migrations, seed data.
  - `Services/`: business logic. Component không inject DbContext trực tiếp.
  - `Components/Pages/`: màn hình Blazor (Home, RoomDetail, Dashboard, RoomGrid…).
  - `Components/Shared/`: component dùng chung (Navbar, LoadingSpinner, ErrorAlert…).
  - `Pages/` (Razor Pages): dùng cho **Login** và **Register** (form POST) vì Cookie không set ổn định trong lúc circuit SignalR đang chạy.
- Mọi truy cập dữ liệu đi qua Service → DbContext.
- Phân quyền bằng `[Authorize(Roles = "...")]` và policy/role claim sau khi đăng nhập bằng Cookie.
- Connection string lấy từ `appsettings.json` / biến môi trường, không hard-code.

## Xác thực & Cookie (Blazor Server)
- Trang Login / Register phải là **Razor Page** (hoặc endpoint form POST), không phải Blazor component interactive, để cookie được set đúng trước khi vào circuit.
- Sau khi login thành công, redirect theo role:
  - Customer → trang danh sách / tìm phòng
  - Receptionist → sơ đồ phòng / quản lý đơn
  - Admin → Dashboard
- Chỉ Admin mới được tạo tài khoản Receptionist / Admin. Customer tự đăng ký với role mặc định `Customer`.

## Triển khai (Docker + GitHub Actions + Render)
- **Luồng deploy cố định:**
  1. Push lên nhánh `main`
  2. GitHub Actions build image Docker → push lên Docker Hub (dùng secret `DOCKER_USERNAME`, `DOCKER_PASSWORD`)
  3. Gọi Deploy Hook của Render (secret `RENDER_DEPLOY_HOOK_URL`)
  4. Render Web Service chọn **"Existing image"** (kéo image từ Docker Hub). **Không dùng SSH/VPS, không để Render tự build từ repo.**
- Dockerfile phải:
  - Multi-stage build: `dotnet publish` dự án server.
  - **Đọc PORT lúc chạy** (không dùng `ENV ASPNETCORE_URLS=...` vì được tính lúc build). Dùng dạng shell:
    ```
    CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet HotelBlazor.dll"]
    ```
  - Có endpoint `/health` dùng `MapHealthChecks`, **AllowAnonymous**, luôn trả 200 (không redirect sang Login).
- Khi chạy Production (Render):
  - Gọi `Database.Migrate()` tự động khi khởi động.
  - Seed tài khoản Admin mặc định nếu chưa có (email/password lấy từ biến môi trường `ADMIN_EMAIL`, `ADMIN_PASSWORD`).
  - Bật `ForwardedHeaders` (X-Forwarded-For, X-Forwarded-Proto). **Phải xóa `KnownNetworks` và `KnownProxies`** vì proxy của Render không phải loopback; nếu không header sẽ bị bỏ qua → lỗi redirect/cookie.
  - Data Protection keys: lưu vào database bằng `PersistKeysToDbContext` (package `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` đã được duyệt). **Không dùng volume** (Render free không có persistent disk). Nếu không PersistKeys thì chấp nhận người dùng phải đăng nhập lại sau mỗi lần restart.
- SQLite trên Render free plan sẽ mất dữ liệu khi redeploy / restart. Chấp nhận reset dữ liệu demo.
- Không hard-code secret vào Dockerfile hay workflow; dùng GitHub Secrets / Render Environment Variables.
- Repo Docker Hub để **public**, hoặc khai báo registry credential trên Render nếu để private.
- Image luôn push cả tag `latest` và tag theo `github.sha`; Deploy Hook chạy **sau khi** push thành công.

## Bảo mật
- Không viết cứng mật khẩu, connection string thật, token hoặc khóa API vào mã nguồn.
- Không đọc hoặc sửa file chứa thông tin bí mật nếu không được yêu cầu rõ.
- Mật khẩu phải được hash bằng Identity (PasswordHasher). Không lưu plain text.
- Seed Admin chỉ chạy khi thiếu tài khoản và lấy thông tin từ biến môi trường.

## Cách làm việc
- Luôn lập kế hoạch (Planning mode) và chờ sinh viên duyệt trước khi sửa code.
- Mỗi lần chỉ làm **một lát cắt nhỏ** theo yêu cầu.
- Giải thích thay đổi bằng tiếng Việt ngắn gọn, nêu rõ file nào đã tạo/sửa.
- Không nói "đã chạy được" nếu chưa chạy kiểm tra thật. Nêu rõ điều gì chưa kiểm chứng.
- Khi tạo migration ở môi trường local: giải thích lệnh và chờ xác nhận trước khi chạy `dotnet ef database update`.
- Ở Production (Render): được phép tự `Database.Migrate()` khi khởi động. Nếu `Program.cs` chưa có thì phải thêm vào (kiểm tra file trước, không giả định).
- Ưu tiên code đơn giản, dễ đọc, phù hợp dự án demo sinh viên. Tránh over-engineering.

## Quy tắc đặc thù dự án khách sạn
- Role chỉ có 3 giá trị: `Customer`, `Receptionist`, `Admin`.
- Trạng thái phòng (RoomStatus): `Available`, `Occupied`, `Cleaning`, `Maintenance`.
- Trạng thái đơn (BookingStatus): `Pending`, `Confirmed`, `CheckedIn`, `Completed`, `Cancelled`.
- Khi tìm phòng trống: loại bỏ phòng có booking (không Cancelled) thỏa mãn overlap:
  `ExistingCheckIn < NewCheckOut AND ExistingCheckOut > NewCheckIn`.
- Không xóa phòng nếu còn booking liên quan (chặn bằng logic nghiệp vụ).

## Cấm
- Không tự ý thêm JWT, OAuth, Identity Server phức tạp.
- Không dùng Interactive WebAssembly / dự án Client riêng.
- Không cài package ngoài danh sách đã được duyệt (trừ `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` và Npgsql nếu được duyệt riêng).
- Không viết code thừa hoặc comment dài dòng không cần thiết.
- Không commit file `bin/`, `obj/`, `.env`, `appsettings.Development.json` chứa secret.
- Không thêm dịch vụ ngoài GitHub / Docker Hub / Render.
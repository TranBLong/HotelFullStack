# Quy tắc dự án HotelFullStack (dành cho AI agent)

## Công nghệ
- ASP.NET Core Blazor Web App **chỉ dùng Interactive Server** (render mode Server). Target framework net10.0.
- **Không dùng Interactive WebAssembly**, không giữ dự án `HotelBlazor.Client`. Chỉ còn một dự án server (`HotelBlazor`).
- Lý do: WASM chạy trong trình duyệt không gọi được Service → DbContext trực tiếp; dùng Server giữ kiến trúc đơn giản Component → Service → DbContext và tránh over-engineering.
- **Chỉ dùng GitHub, Docker Hub, Jenkins, AWS EC2. Không thêm dịch vụ khác.**
- Database: **MariaDB 11.4 cho cả local và EC2** (trên EC2 chạy bằng Docker). Không dùng SQLite.
- Xác thực: ASP.NET Core Identity với **Cookie Authentication**. **Không dùng JWT**.
- Giao diện: Blazor Components + Bootstrap (có sẵn trong template). Không cài thêm thư viện UI lớn nếu chưa được duyệt.
- Chỉ dùng C# và Razor. Comment code bằng tiếng Việt ngắn gọn khi cần giải thích logic nghiệp vụ.
- Package đã được duyệt thêm: `Microting.EntityFrameworkCore.MySql`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
       `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`.

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
- Connection string `DefaultConnection` lấy từ user-secrets (local) / biến môi trường trong `docker-compose.prod.yml` (EC2). Không ghi giá trị thật vào appsettings.json.

## Xác thực & Cookie (Blazor Server)
- Trang Login / Register phải là **Razor Page** (hoặc endpoint form POST), không phải Blazor component interactive, để cookie được set đúng trước khi vào circuit.
- Sau khi login thành công, redirect theo role:
  - Customer → trang danh sách / tìm phòng
  - Receptionist → sơ đồ phòng / quản lý đơn
  - Admin → Dashboard
- Chỉ Admin mới được tạo tài khoản Receptionist / Admin. Customer tự đăng ký với role mặc định `Customer`.

## Triển khai (Docker + Jenkins + AWS EC2)
- **Luồng deploy cố định:**
  1. Push lên nhánh `main`
  2. Jenkins (chạy trên VM Ubuntu, job `HotelBlazor-CICD`, `pollSCM` mỗi 2 phút) checkout mã nguồn từ GitHub
  3. Jenkins build image Docker bằng `docker build --network=host` (workaround vì container trên VM không ra được NuGet) → push lên Docker Hub với tag `latest` và tag số build (`$BUILD_NUMBER`)
  4. Jenkins SSH vào EC2: `scp docker-compose.prod.yml`, rồi `docker compose pull app && docker compose up -d`
- **Secret nằm ở đâu:** Jenkins Credentials (`github-pat`, `dockerhub-cred`, `ec2-ssh-key`) và file `~/hotel/.env` trên EC2 (`DOCKER_USER`, `DB_ROOT_PASSWORD`, `DB_APP_PASSWORD`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`). Không commit `.env`; `.env.example` chỉ chứa giá trị mẫu.
- Dockerfile phải:
  - Multi-stage build: `dotnet publish` dự án server.
  - **Đọc PORT lúc chạy** (không dùng `ENV ASPNETCORE_URLS=...` vì được tính lúc build). Dùng dạng shell:
    ```
    CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet HotelBlazor.dll"]
    ```
  - Có endpoint `/health` dùng `MapHealthChecks`, **AllowAnonymous**, luôn trả 200 (không redirect sang Login).
- `docker-compose.prod.yml` chạy 2 service: `app` (image từ Docker Hub, map `80:8080`) và `db` (`mariadb:11.4`, volume `hotel-db-data`, healthcheck, giới hạn RAM cho máy EC2 2 GB). Cổng 3306 **không** publish ra ngoài.
- Khi chạy Production (EC2):
  - Gọi `Database.Migrate()` tự động khi khởi động.
  - Seed tài khoản Admin mặc định nếu chưa có (email/password lấy từ biến môi trường `ADMIN_EMAIL`, `ADMIN_PASSWORD`).
  - Bật `ForwardedHeaders` (X-Forwarded-For, X-Forwarded-Proto) và **xóa `KnownNetworks`, `KnownProxies`** để chạy được sau reverse proxy hoặc tunnel (nếu có); nếu không header sẽ bị bỏ qua → lỗi redirect/cookie.
  - Data Protection keys: nếu chưa lưu vào DB (`PersistKeysToDbContext`) thì chấp nhận người dùng phải đăng nhập lại sau mỗi lần deploy (container mới).
- Dữ liệu nằm trong MariaDB (volume Docker trên EC2), tách khỏi container `app` nên redeploy không làm mất dữ liệu. **Không chạy `docker compose down -v`** trên server vì sẽ xóa dữ liệu.
- Không hard-code secret vào Dockerfile, Jenkinsfile hay compose; dùng Jenkins Credentials / `.env` trên server.
- Repo Docker Hub để **public**, hoặc khai báo `docker login` trên EC2 nếu để private (pipeline đã tự login).
- Image luôn push cả tag `latest` và tag theo số build; bước Deploy chạy **sau khi** push thành công.

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
- Chỉ chạy `dotnet ef database update` trên DB local. Không trỏ chuỗi kết nối local vào DB trên EC2.
  Trên EC2, schema được cập nhật bởi `Database.Migrate()` khi khởi động.
- Ở Production (EC2): được phép tự `Database.Migrate()` khi khởi động. Nếu `Program.cs` chưa có thì phải thêm vào (kiểm tra file trước, không giả định).
- Ưu tiên code đơn giản, dễ đọc, phù hợp dự án demo sinh viên. Tránh over-engineering.

## Quy tắc đặc thù dự án khách sạn
- Role chỉ có 3 giá trị: `Customer`, `Receptionist`, `Admin`.
- Trạng thái phòng (RoomStatus): `Available`, `Occupied`, `Cleaning`, `Maintenance`.
- Trạng thái đơn (BookingStatus): `Pending`, `Confirmed`, `CheckedIn`, `Completed`, `Cancelled`.
- Khi tìm phòng trống và khi tạo/sửa booking: chỉ booking có status `Pending`, `Confirmed`, `CheckedIn`
  mới chặn phòng; `Completed` và `Cancelled` KHÔNG chặn. Điều kiện overlap:
  `ExistingCheckIn < NewCheckOut AND ExistingCheckOut > NewCheckIn`.
  Logic này nằm ở MỘT chỗ dùng chung, không viết lặp ở nhiều service.
- `TotalAmount` được tính và lưu vào Booking lúc đặt.
- Đơn walk-in: `UserId` = null, có `GuestName` / `GuestPhone`.
- Check-out: đơn -> `Completed`, phòng -> `Cleaning`; lễ tân bấm "Đã dọn xong" thì phòng -> `Available`.
- Khóa tài khoản dùng Identity lockout.
- Không xóa phòng nếu còn booking liên quan (chặn bằng logic nghiệp vụ).

## Cấm
- Không tự ý thêm JWT, OAuth, Identity Server phức tạp.
- Không dùng Interactive WebAssembly / dự án Client riêng.
- Không cài package ngoài danh sách đã được duyệt ở mục Công nghệ.
- Không viết code thừa hoặc comment dài dòng không cần thiết.
- Không commit file `bin/`, `obj/`, `.env`, `appsettings.Development.json` chứa secret.
- Không thêm dịch vụ ngoài GitHub / Docker Hub / Jenkins / AWS EC2.

Kế hoạch triển khai HotelFullStack

## Cập nhật trạng thái theo AGENTS.md và SPEC.md mới

- PostgreSQL dùng cho cả local và Render. `DefaultConnection` lấy từ user-secrets ở local / biến môi trường trên Render; không ghi connection string thật vào repo.
- Chỉ booking `Pending`, `Confirmed`, `CheckedIn` chặn phòng. Điều kiện overlap là `ExistingCheckIn < NewCheckOut AND ExistingCheckOut > NewCheckIn`; `Completed` và `Cancelled` không chặn.
- Predicate overlap dùng chung giữa tìm phòng và kiểm tra lúc lưu booking online/walk-in.
- `TotalAmount` được tính và lưu vào booking lúc tạo.
- Walk-in dùng `UserId = null` và lưu `GuestName` / `GuestPhone`.
- Check-out chuyển đơn sang `Completed`, phòng sang `Cleaning`; sau thao tác “Đã dọn xong”, phòng chuyển sang `Available`.
- `dotnet ef database update` chỉ chạy trên database local sau khi giải thích và được duyệt; không chạy lệnh đó trên Render. Production cập nhật schema bằng `Database.Migrate()` lúc khởi động.
- Trạng thái: A0, A, B, C, D, sửa logic overlap và E đã hoàn thành; F chưa làm.

## Vấn đề tại thời điểm lập kế hoạch ban đầu

Khi kế hoạch được lập, project server còn `HotelBlazor.Client` và cấu hình WASM, và các thư mục nghiệp vụ chưa tồn tại. Các ghi chú bên dưới mô tả baseline lúc đó, không phải trạng thái hiện tại; A0 đến D cùng sửa logic overlap đã hoàn thành.

Kế hoạch dưới đây chia thành các lát cắt nhỏ, theo thứ tự hợp lý và theo quyết định đã thống nhất: PostgreSQL cho cả local và Render; tách A0 để gỡ toàn bộ WASM; mỗi lát cắt chỉ tập trung vào 1 phần chức năng rõ ràng, đồng thời giữ nguyên tiêu chí SPEC và AGENTS.

## Baseline đã quét trước khi thực hiện

- `HotelBlazor/Program.cs`: đang dùng `AddRazorComponents()` + `AddInteractiveServerComponents()` + `AddInteractiveWebAssemblyComponents()`, và `MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()`; đây là cấu hình WASM, không phù hợp với AGENTS.
- `HotelBlazor.Client/Program.cs`: project client vẫn tồn tại và chạy `WebAssemblyHostBuilder.CreateDefault(args)`, khớp với mô hình WASM.
- `HotelBlazor/Components/Pages`: chỉ có `Home.razor`, `Weather.razor`, `Counter`/`Error`/`NotFound`; chưa có màn hình nghiệp vụ.
- Không có `Data/`, `Models/`, `Services/` trong `HotelBlazor/` theo cấu hình folder hiện tại.
- `Dockerfile`: vẫn là template mặc định (build/publish, EXPOSE 80, ENV ASPNETCORE_URLS=http://+:80), chưa có `PORT` runtime, `/health`, `ForwardedHeaders`, Data Protection persist DB như AGENTS yêu cầu.
- `.github/workflows/deploy.yml`: chỉ build + push Docker image lên Docker Hub, chưa có deploy hook Render và chưa có bước kiểm tra health/production-specific config.

## [ĐÃ HOÀN THÀNH] A0 — gỡ `HotelBlazor.Client` và toàn bộ cấu hình WASM, chạy lại được

- File sẽ tạo hoặc sửa:
  - `HotelBlazor/Program.cs`
  - `HotelBlazor/HotelBlazor.csproj`
  - `HotelBlazor.Client/` (xóa bỏ khỏi giải pháp nếu cần)
  - `HotelBlazor.sln` (nếu project client còn xuất hiện)
  - `HotelBlazor/Components/App.razor` / `Routes.razor` (nếu cần bật Server-only routing)

- Tiêu chí SPEC tương ứng:
  - A0 không có tiêu chí SPEC riêng; mục tiêu là chuẩn hoá kiến trúc server-only để các lát cắt sau chạy đúng với `AGENTS.md`.
- Cách kiểm tra:
  - Không còn `ProjectReference` tới `HotelBlazor.Client`.
  - Không còn `AddInteractiveWebAssemblyComponents()` và `AddInteractiveWebAssemblyRenderMode()`.
  - Dự án build/khởi chạy với `Blazor Server` mà không phụ thuộc vào project client.
- Kết quả: đã gỡ project Client, tham chiếu project, cấu hình WASM và dòng COPY Client trong Dockerfile; solution server-only build thành công.

## [ĐÃ HOÀN THÀNH] A — Entity, DbContext, Identity, seed dữ liệu và seed Admin từ biến môi trường

- File sẽ tạo hoặc sửa:
  - `HotelBlazor/Program.cs`
  - `HotelBlazor/Data/ApplicationDbContext.cs`
  - `HotelBlazor/Models/ApplicationUser.cs`
  - `HotelBlazor/Models/Room.cs`
  - `HotelBlazor/Models/RoomType.cs`
  - `HotelBlazor/Models/Booking.cs`
  - `HotelBlazor/Models/BookingStatus.cs`
  - `HotelBlazor/Models/RoomStatus.cs`
  - `HotelBlazor/Models/ApplicationRole.cs` (nếu cần role enum/string constants)
  - `HotelBlazor/Data/DbInitializer.cs` hoặc `SeedData.cs`
  - `HotelBlazor/Components/Shared/Navbar.razor`
  - `HotelBlazor/Components/Shared/LoadingSpinner.razor`
  - `HotelBlazor/Components/Shared/ErrorAlert.razor`
  - `HotelBlazor/appsettings.json`, `HotelBlazor/appsettings.Development.json` (nếu cần)
  - `HotelBlazor/HotelBlazor.csproj` (thêm `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` nếu cần)

- Tiêu chí SPEC tương ứng:
  - 2 (trạng thái giao diện và cấu hình dữ liệu)
  - 17, 18, 19 (Production / Docker / Render)
- Cách kiểm tra:
  - Kiểm tra `ApplicationDbContext` có `DbSet` đầy đủ và migration khởi tạo đúng.
  - Kiểm tra tài khoản Admin được seed từ `ADMIN_EMAIL` / `ADMIN_PASSWORD` khi thiếu user.
  - `Database.Migrate()` đã được cấu hình trong Production; ForwardedHeaders thuộc phạm vi rà soát chưa làm ở F.
  - Seed 3 role: `Customer`, `Receptionist`, `Admin`.
- Cấu hình kết nối: `DefaultConnection`, lấy từ user-secrets local hoặc biến môi trường Render.

## [ĐÃ HOÀN THÀNH] B — Login/Register bằng Razor Page + redirect theo role

- File sẽ tạo hoặc sửa:
  - `HotelBlazor/Pages/Account/Login.cshtml` và `.cshtml.cs`
  - `HotelBlazor/Pages/Account/Register.cshtml` và `.cshtml.cs`
  - `HotelBlazor/Pages/Shared/_Layout.cshtml` hoặc layout chung nếu cần
  - `HotelBlazor/Components/App.razor` hoặc `Routes.razor`
  - `HotelBlazor/Program.cs` (Cookie auth, role policy, redirect login)
  - `HotelBlazor/Services/AuthService` nếu cần đóng gói logic đối với role/redirect

- Tiêu chí SPEC tương ứng:
  - 1, 2, 3, 4
- Cách kiểm tra:
  - Mở trang cần auth khi chưa login -> redirect `/login`.
  - Register Customer -> tự đăng nhập và redirect đúng trang Customer.
  - Đăng nhập với Customer/Receptionist/Admin -> redirect theo role.
  - F12 → Application → Cookies có `.AspNetCore.Identity.Application` sau login/register.
- Route đích đã chọn: Customer -> `/rooms`, Receptionist -> `/reception`, Admin -> `/dashboard`.

## [ĐÃ HOÀN THÀNH] C — Customer: tìm phòng trống, xem chi tiết, đặt phòng, hủy đơn Pending

- File sẽ tạo hoặc sửa:
  - `HotelBlazor/Services/RoomService.cs`
  - `HotelBlazor/Services/BookingService.cs`
  - `HotelBlazor/Components/Pages/Customer/RoomSearch.razor`
  - `HotelBlazor/Components/Pages/Customer/RoomDetail.razor`
  - `HotelBlazor/Components/Pages/Customer/BookingHistory.razor`
  - `HotelBlazor/Models/BookingStatus.cs`, `RoomStatus.cs` nếu chưa tạo ở A
- Tiêu chí SPEC tương ứng:
  - 5, 6, 7
  - 15 (logic overlap)
  - 16 (mọi màn hình danh sách phải có loading/error/empty/data + nút Thử lại)
- Cách kiểm tra:
  - Chỉ booking `Pending`, `Confirmed` hoặc `CheckedIn` có overlap loại phòng khỏi kết quả; `Completed` và `Cancelled` không loại.
  - Đặt 2 đêm với giá 500.000 -> `TotalAmount = 1.000.000` và status `Pending`.
  - Hủy đơn Pending -> status `Cancelled` và phòng trở lại có thể được đặt.
  - Mỗi screen có trạng thái loading, lỗi, rỗng và dữ liệu theo SPEC.
- Trạng thái quyết định: `TotalAmount` được lưu tại thời điểm đặt.

## [ĐÃ HOÀN THÀNH] D — Receptionist: sơ đồ phòng, duyệt/từ chối đơn, check-in/check-out, tạo đơn walk-in

- File sẽ tạo hoặc sửa:
  - `HotelBlazor/Components/Pages/Receptionist/RoomBoard.razor`
  - `HotelBlazor/Components/Pages/Receptionist/BookingQueue.razor`
  - `HotelBlazor/Components/Pages/Receptionist/WalkInBooking.razor`
  - `HotelBlazor/Services/ReceptionService.cs`
  - `HotelBlazor/Models/BookingStatus.cs`, `RoomStatus.cs` nếu chưa tạo ở A
  - `HotelBlazor/Migrations/*AddWalkInBookingFields*` (đã tạo và áp dụng trên database local sau khi được xác nhận)
- Tiêu chí SPEC tương ứng:
  - 8, 9, 10, 11
  - 16 (loading/error/empty/data cho danh sách đơn)
- Cách kiểm tra:
  - Load sơ đồ phòng -> badge màu theo trạng thái phòng.
  - Duyệt đơn -> `Confirmed`; từ chối -> `Cancelled` + lưu reason.
  - Check-in -> đơn `CheckedIn` và phòng `Occupied` cùng lúc.
  - Check-out -> đơn `Completed`, phòng `Cleaning`; sau khi lễ tân xác nhận đã dọn xong -> phòng `Available`.
  - Walk-in lưu `UserId = null`, `GuestName`, `GuestPhone`, status `Confirmed`, và `TotalAmount`; kiểm tra overlap ngay lúc lưu.
- Room board đã có chuyển trạng thái `Cleaning` -> `Available` (thao tác xác nhận phòng đã sẵn sàng sau khi dọn).

## [ĐÃ HOÀN THÀNH] Sửa logic overlap — gom về một biểu thức dùng chung

- File đã tạo/sửa:
  - `HotelBlazor/Services/BookingOverlap.cs`
  - `HotelBlazor/Services/RoomService.cs`
  - `HotelBlazor/Services/BookingService.cs`
  - `HotelBlazor/Services/ReceptionService.cs`
- Predicate chung chỉ coi `Pending`, `Confirmed`, `CheckedIn` là trạng thái chặn, kèm điều kiện overlap ngày; `Completed` và `Cancelled` không chặn.
- Cả tìm phòng lẫn kiểm tra lúc tạo booking online/walk-in đều dùng predicate này.
- Kiểm tra đã thực hiện: build thành công và quét mã xác nhận điều kiện ngày/status chỉ định nghĩa một chỗ.

## [ĐÃ HOÀN THÀNH] E — Admin: Dashboard, CRUD RoomType/Room, quản lý user, xem toàn bộ đơn, tạo tài khoản Receptionist/Admin

- File sẽ tạo hoặc sửa:
  - `HotelBlazor/Components/Pages/Admin/Dashboard.razor`
  - `HotelBlazor/Components/Pages/Admin/RoomTypeManagement.razor`
  - `HotelBlazor/Components/Pages/Admin/RoomManagement.razor`
  - `HotelBlazor/Components/Pages/Admin/UserManagement.razor`
  - `HotelBlazor/Components/Pages/Admin/BookingOverview.razor`
  - `HotelBlazor/Components/Pages/Admin/CreateStaffAccount.razor` (hoặc trong UserManagement)
  - `HotelBlazor/Services/AdminService.cs`
- Tiêu chí SPEC tương ứng:
  - 12, 13, 14
  - 16 (UI error handling cho danh sách)
- Cách kiểm tra:
  - Dashboard hiển thị tổng số phòng, phòng trống, đơn Pending, doanh thu dự kiến.
  - Service giới hạn danh sách đơn tối đa 100, mới nhất trước; lọc theo trạng thái.
  - Admin không cho xóa phòng còn bất kỳ booking liên quan hoặc loại phòng còn phòng trực thuộc.
  - Admin tạo tài khoản Receptionist/Admin qua Identity; khóa/mở khóa dùng Identity lockout.
  - Không cho Admin tự khóa hoặc khóa Admin cuối cùng còn hoạt động.
- Quyết định UX: Login hiển thị thông báo “Tài khoản đã bị khóa. Vui lòng thử lại sau.”
- Không thay đổi model/schema nên không cần tạo migration.
- Đã chạy kiểm tra build bằng `dotnet build`; chưa kiểm thử thủ công trên trình duyệt hoặc thử thao tác với dữ liệu thật.

## [CHƯA LÀM] F — Rà soát Dockerfile, /health, ForwardedHeaders, Data Protection so với AGENTS.md (chỉ báo cáo chỗ thiếu, chưa sửa)

- File sẽ tạo hoặc sửa:
  - `Dockerfile`
  - `.github/workflows/deploy.yml`
  - `HotelBlazor/Program.cs`
  - `HotelBlazor/Properties/...` nếu cần cho health check hoặc forwarding

- Tiêu chí SPEC tương ứng:
  - 17, 18, 19, 20
- Ràng buộc thao tác database:
  - Chỉ chạy `dotnet ef database update` trên database local sau khi giải thích và được người dùng xác nhận.
  - Không chạy `database update` trên Render; production áp dụng migration bằng `Database.Migrate()` lúc khởi động.
- Cách kiểm tra:
  - `PORT` được đọc trong runtime, không hard-code `ASPNETCORE_URLS` khi build.
  - `/health` trả 200 cho `AllowAnonymous`.
  - `UseForwardedHeaders()` và bỏ `KnownNetworks` / `KnownProxies` để Render proxy không làm mất header.
  - Data Protection keys lưu vào DB bằng `PersistKeysToDbContext` hoặc chấp nhận phải login lại sau restart.
  - Workflow push lên Docker Hub và gọi Render Deploy Hook sau khi push.
- Điểm chưa chắc chắn:
  - Render deploy hook URL và biến môi trường thực tế chưa có trong repo; do đó chỉ có thể báo cáo chỗ thiếu, không xác nhận hay sửa ngay.

## [CẬP NHẬT] Thứ tự và trạng thái triển khai

1. A0 -> A -> B -> C -> D -> sửa logic overlap -> E (đã hoàn thành) -> F
2. Mỗi lát cắt nên làm với 1 commit logic nhỏ, đồng thời cập nhật UI hiển thị đủ 4 trạng thái đầy đủ theo SPEC: loading, error, empty, data.
3. Cleanup architecture theo AGENTS (server-only) phải hoàn tất trong A0/A để tránh dư WASM trong dự án.
4. F chưa làm; chỉ báo cáo chỗ thiếu, chưa sửa.

## [CẬP NHẬT] Lưu ý đánh giá rủi ro

- Dự án hiện tại đang sai chuẩn với AGENTS ngay từ đầu; do đó, phần chỉnh sửa architecture sẽ là bước khởi tạo bắt buộc, không thể bỏ qua.
- `HotelBlazor.Client` và `AddInteractiveWebAssemblyComponents` là điểm không tương thích lớn nhất cần xử lý trước khi xây chức năng nghiệp vụ.
- PostgreSQL được dùng cho cả local và Render; connection string lấy từ user-secrets local / biến môi trường Render.
- `TotalAmount` lưu lúc đặt; walk-in có `UserId = null` cùng `GuestName` / `GuestPhone`.
- Check-out -> `Cleaning`; lễ tân xác nhận đã dọn xong -> `Available`.
- User bị khóa bằng Identity lockout.
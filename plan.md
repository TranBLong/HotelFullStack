# Kế hoạch triển khai HotelFullStack

## Vấn đề và hướng tiếp cận

Dự án hiện tại đang lệch so với `AGENTS.md` và `SPEC.md`: project server vẫn còn `HotelBlazor.Client` và cấu hình WASM (`AddInteractiveWebAssemblyComponents`, `ProjectReference` tới client), trong khi AGENTS yêu cầu duy nhất sử dụng Blazor Server và bỏ hoàn toàn mô hình Client riêng. Ngoài ra, các thư mục `Data/`, `Models/`, `Services/`, `Pages/` chưa tồn tại trong project server, trong khi `SPEC.md` yêu cầu login/register bằng Razor Page, identity, booking, room management và deploy theo chuẩn Render/Docker.

Kế hoạch dưới đây chia thành các lát cắt nhỏ, theo thứ tự hợp lý và theo quyết định đã thống nhất: PostgreSQL cho cả local và Render; tách A0 để gỡ toàn bộ WASM; mỗi lát cắt chỉ tập trung vào 1 phần chức năng rõ ràng, đồng thời giữ nguyên tiêu chí SPEC và AGENTS.

## Tình trạng hiện tại đã quét

- `HotelBlazor/Program.cs`: đang dùng `AddRazorComponents()` + `AddInteractiveServerComponents()` + `AddInteractiveWebAssemblyComponents()`, và `MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()`; đây là cấu hình WASM, không phù hợp với AGENTS.
- `HotelBlazor.Client/Program.cs`: project client vẫn tồn tại và chạy `WebAssemblyHostBuilder.CreateDefault(args)`, khớp với mô hình WASM.
- `HotelBlazor/Components/Pages`: chỉ có `Home.razor`, `Weather.razor`, `Counter`/`Error`/`NotFound`; chưa có màn hình nghiệp vụ.
- Không có `Data/`, `Models/`, `Services/` trong `HotelBlazor/` theo cấu hình folder hiện tại.
- `Dockerfile`: vẫn là template mặc định (build/publish, EXPOSE 80, ENV ASPNETCORE_URLS=http://+:80), chưa có `PORT` runtime, `/health`, `ForwardedHeaders`, Data Protection persist DB như AGENTS yêu cầu.
- `.github/workflows/deploy.yml`: chỉ build + push Docker image lên Docker Hub, chưa có deploy hook Render và chưa có bước kiểm tra health/production-specific config.

## [THAY ĐỔI] A0 — gỡ `HotelBlazor.Client` và toàn bộ cấu hình WASM, chạy lại được

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
- Điểm chưa chắc chắn:
  - Cần xác nhận cách xoá project `HotelBlazor.Client` khỏi solution mà không phá vỡ cấu trúc repo; chắc chắn phải làm trong bước này trước khi vào business logic.

## [THAY ĐỔI] A — Entity, DbContext, Identity, seed dữ liệu và seed Admin từ biến môi trường

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
  - Kiểm tra `Program.cs` có `Database.Migrate()` trong production và `UseForwardedHeaders()` ở nơi thích hợp (đúng nhưng sẽ được rà soát kỹ ở F).
  - Seed 3 role: `Customer`, `Receptionist`, `Admin`.
- Điểm chưa chắc chắn:
  - Cần xác nhận tên exact của `ConnectionStrings` và cách lấy từ `user-secrets` / biến môi trường theo quy định AGENTS; cần thống nhất ngay khi code thực thi.

## [THAY ĐỔI] B — Login/Register bằng Razor Page + redirect theo role

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
- Điểm chưa chắc chắn:
  - Cần xác định route đích mặc định cho từng role: Customer -> `/rooms`, Receptionist -> `/reception`, Admin -> `/dashboard` (theo AGENTS) hoặc theo route thực tế khi pages được tạo.

## [THAY ĐỔI] C — Customer: tìm phòng trống, xem chi tiết, đặt phòng, hủy đơn Pending

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
  - Tạo booking đã Confirmed cho phòng A trong khoảng overlap -> phòng A không xuất hiện trong tìm kiếm.
  - Đặt 2 đêm với giá 500.000 -> `TotalAmount = 1.000.000` và status `Pending`.
  - Hủy đơn Pending -> status `Cancelled` và phòng trở lại có thể được đặt.
  - Mỗi screen có trạng thái loading, lỗi, rỗng và dữ liệu theo SPEC.
- Điểm chưa chắc chắn:
  - `TotalAmount` lưu lúc đặt theo quyết định mới, nên service và UI cần nhất quán; cần xác nhận để không tính nhầm ở nơi khác.

## [THAY ĐỔI] D — Receptionist: sơ đồ phòng, duyệt/từ chối đơn, check-in/check-out, tạo đơn walk-in

- File sẽ tạo hoặc sửa:
  - `HotelBlazor/Components/Pages/Receptionist/RoomBoard.razor`
  - `HotelBlazor/Components/Pages/Receptionist/BookingQueue.razor`
  - `HotelBlazor/Components/Pages/Receptionist/WalkInBooking.razor`
  - `HotelBlazor/Services/ReceptionService.cs`
  - `HotelBlazor/Models/BookingStatus.cs`, `RoomStatus.cs` nếu chưa tạo ở A
- Tiêu chí SPEC tương ứng:
  - 8, 9, 10, 11
  - 16 (loading/error/empty/data cho danh sách đơn)
- Cách kiểm tra:
  - Load sơ đồ phòng -> badge màu theo trạng thái phòng.
  - Duyệt đơn -> `Confirmed`; từ chối -> `Cancelled` + lưu reason.
  - Check-in -> đơn `CheckedIn` và phòng `Occupied` cùng lúc.
  - Check-out -> đơn `Completed` và phòng `Cleaning` theo quyết định đã thống nhất.
  - Tạo đơn walk-in -> lưu booking với role/ trạng thái phù hợp và hiển thị trên hàng chờ.
- Điểm chưa chắc chắn:
  - Phòng sau checkout chuyển sang `Cleaning` hay `Available` ngay; quyết định đã chọn `Cleaning` như trạng thái trung gian, nên cần gắn logic vào room board và check-out service.

## [THAY ĐỔI] E — Admin: Dashboard, CRUD RoomType/Room, quản lý user, xem toàn bộ đơn, tạo tài khoản Receptionist/Admin

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
  - Admin không cho xóa phòng còn booking liên quan.
  - Admin tạo tài khoản Receptionist/Admin với role phù hợp.
  - Khóa user bằng Identity lockout -> user không login được nữa.
- Điểm chưa chắc chắn:
  - Cần xác định văn bản/UX khi user bị lockout: có hiển thị message “tài khoản đã bị khóa” trong login page hay không; quy định chung là login fail nhưng có thể custom thêm message.

## [THAY ĐỔI] F — Rà soát Dockerfile, /health, ForwardedHeaders, Data Protection so với AGENTS.md (chỉ báo cáo chỗ thiếu, chưa sửa)

- File sẽ tạo hoặc sửa:
  - `Dockerfile`
  - `.github/workflows/deploy.yml`
  - `HotelBlazor/Program.cs`
  - `HotelBlazor/Properties/...` nếu cần cho health check hoặc forwarding

- Tiêu chí SPEC tương ứng:
  - 17, 18, 19, 20
- Cách kiểm tra:
  - `PORT` được đọc trong runtime, không hard-code `ASPNETCORE_URLS` khi build.
  - `/health` trả 200 cho `AllowAnonymous`.
  - `UseForwardedHeaders()` và bỏ `KnownNetworks` / `KnownProxies` để Render proxy không làm mất header.
  - Data Protection keys lưu vào DB bằng `PersistKeysToDbContext` hoặc chấp nhận phải login lại sau restart.
  - Workflow push lên Docker Hub và gọi Render Deploy Hook sau khi push.
- Điểm chưa chắc chắn:
  - Render deploy hook URL và biến môi trường thực tế chưa có trong repo; do đó chỉ có thể báo cáo chỗ thiếu, không xác nhận hay sửa ngay.

## [THAY ĐỔI] Thứ tự triển khai đề xuất

1. A0 -> A -> B -> C -> D -> E -> F
2. Mỗi lát cắt nên làm với 1 commit logic nhỏ, đồng thời cập nhật UI hiển thị đủ 4 trạng thái đầy đủ theo SPEC: loading, error, empty, data.
3. Cleanup architecture theo AGENTS (server-only) phải hoàn tất trong A0/A để tránh dư WASM trong dự án.
4. Lát cắt F nên được đề xuất và rà soát sớm ngay sau A0/A, nhưng vẫn chỉ báo cáo chỗ thiếu chưa sửa theo yêu cầu.

## [THAY ĐỔI] Lưu ý đánh giá rủi ro

- Dự án hiện tại đang sai chuẩn với AGENTS ngay từ đầu; do đó, phần chỉnh sửa architecture sẽ là bước khởi tạo bắt buộc, không thể bỏ qua.
- `HotelBlazor.Client` và `AddInteractiveWebAssemblyComponents` là điểm không tương thích lớn nhất cần xử lý trước khi xây chức năng nghiệp vụ.
- Với quyết định dùng PostgreSQL cho cả local và Render, không còn dùng SQLite theo mặc định; Render free vẫn có thể reset dữ liệu theo deployment, nhưng phần app phải chuẩn bị cho connection string từ env/user-secrets.
- Các định nghĩa mới đi kèm theo quyết định: `TotalAmount` lưu lúc đặt; `check-out` -> `Cleaning`; user khóa bằng `Identity lockout`.

# SPEC.md – Đặc tả dự án HotelFullStack (phiên bản đơn giản)

## 1. Mục tiêu người dùng

Người dùng mở ứng dụng sẽ thấy màn hình Đăng nhập / Đăng ký trước tiên.

- Khách hàng (Customer) có thể tìm phòng theo ngày, xem chi tiết, đặt phòng, xem lịch sử đơn và hủy đơn đang chờ.
- Lễ tân (Receptionist) có thể xem sơ đồ trạng thái phòng, duyệt đơn online, thực hiện check-in / check-out và tạo đơn walk-in.
- Quản trị viên (Admin) có thể xem dashboard thống kê cơ bản, quản lý loại phòng & phòng, quản lý tài khoản người dùng và theo dõi toàn bộ đơn đặt.

Sau khi đăng nhập thành công, hệ thống tự động chuyển hướng đúng trang theo vai trò.

## 2. Các trạng thái giao diện

Mỗi màn hình danh sách / tìm kiếm / chi tiết phải xử lý đủ 4 trạng thái:

| Trạng thái       | Hiển thị                                                                 |
|------------------|--------------------------------------------------------------------------|
| Đang tải         | Vòng quay (Spinner) hoặc skeleton, không hiện dữ liệu cũ                 |
| Lỗi              | Thông báo lỗi rõ ràng + nút "Thử lại"                                    |
| Rỗng             | Thông báo "Không có dữ liệu" / "Không tìm thấy phòng" + gợi ý hành động |
| Có dữ liệu       | Danh sách / form / chi tiết bình thường                                  |

Các trạng thái đặc thù:
- Đăng nhập / Đăng ký: loading khi submit, lỗi validation hoặc sai mật khẩu.
- Đặt phòng: loading khi tính tổng tiền / gửi đơn, thành công thì hiện mã đơn.
- Sơ đồ phòng (Receptionist): badge màu theo trạng thái phòng.

## 3. Tiêu chí chấp nhận

### Auth & Routing
1. *Cho* người dùng chưa đăng nhập, *khi* mở bất kỳ trang nào cần quyền, *thì* bị chuyển về trang Login.
   - Cách kiểm chứng: mở `/rooms` khi chưa login → redirect `/login`.
2. *Cho* người dùng đăng ký tài khoản mới, *khi* submit form hợp lệ, *thì* được tạo với role mặc định `Customer` và tự động đăng nhập.
   - Cách kiểm chứng: đăng ký → kiểm tra bảng AspNetUsers + AspNetUserRoles.
3. *Cho* người dùng đăng nhập thành công, *khi* có role Customer / Receptionist / Admin, *thì* được redirect đúng trang tương ứng.
   - Cách kiểm chứng: đăng nhập 3 tài khoản khác role → kiểm tra URL đích.
4. *Cho* trang Login / Register, *khi* submit, *thì* dùng form POST (Razor Page hoặc endpoint) để cookie được set đúng trước khi vào Blazor circuit.
   - Cách kiểm chứng: sau login, F12 → Application → Cookies có cookie `.AspNetCore.Identity.Application`.

### Customer
5. *Cho* Customer đang ở trang tìm phòng, *khi* chọn ngày Check-in, Check-out, số khách, khoảng giá hoặc loại phòng, *thì* danh sách chỉ hiện phòng còn trống theo thuật toán overlap.
   - Cách kiểm chứng: tạo 1 booking Confirmed cho phòng A ngày 1-3; tìm ngày 2-4 → phòng A không xuất hiện.
6. *Cho* Customer xem chi tiết phòng, *khi* nhấn Đặt phòng và nhập đủ thông tin, *thì* hệ thống tính `Tổng tiền = (CheckOut - CheckIn) × Giá/đêm` và tạo đơn trạng thái `Pending`.
   - Cách kiểm chứng: đặt 2 đêm giá 500.000 → tổng 1.000.000, status = Pending.
7. *Cho* Customer có đơn `Pending`, *khi* nhấn Hủy, *thì* đơn chuyển sang `Cancelled` và phòng trở lại có thể đặt.
   - Cách kiểm chứng: hủy đơn → status đổi, tìm lại phòng thì thấy.

### Receptionist
8. *Cho* Receptionist xem sơ đồ phòng, *khi* load trang, *thì* mỗi phòng hiện badge màu tương ứng trạng thái (Available / Occupied / Cleaning / Maintenance).
   - Cách kiểm chứng: đổi status 1 phòng trong DB → refresh → badge đổi màu.
9. *Cho* Receptionist có danh sách đơn Pending, *khi* nhấn Duyệt, *thì* đơn thành `Confirmed`; nhấn Từ chối thì thành `Cancelled` kèm lý do.
   - Cách kiểm chứng: duyệt → status Confirmed; từ chối → Cancelled + lý do lưu được.
10. *Cho* Receptionist thực hiện Check-in, *khi* xác nhận, *thì* đơn → `CheckedIn`, phòng → `Occupied`.
    - Cách kiểm chứng: check-in → kiểm tra 2 status đổi đồng thời.
11. *Cho* Receptionist thực hiện Check-out, *khi* xác nhận, *thì* đơn → `Completed`, phòng → `Cleaning`; lễ tân bấm "Đã dọn xong" thì phòng → `Available`.
    - Cách kiểm chứng: check-out → phòng Cleaning; bấm "Đã dọn xong" → phòng Available.

### Admin
12. *Cho* Admin ở Dashboard, *khi* mở trang, *thì* thấy ít nhất: tổng số phòng, số phòng trống, số đơn Pending, tổng doanh thu dự kiến (từ đơn Confirmed/CheckedIn/Completed).
    - Cách kiểm chứng: tạo dữ liệu mẫu → số liệu khớp.
13. *Cho* Admin quản lý RoomType / Room, *khi* thêm / sửa / xóa, *thì* dữ liệu được cập nhật; không cho xóa phòng đang có booking.
    - Cách kiểm chứng: thử xóa phòng có booking → bị chặn + thông báo.
14. *Cho* Admin quản lý User, *khi* khóa tài khoản, *thì* user đó không đăng nhập được nữa.
    - Cách kiểm chứng: khóa → thử login → thất bại.

### Logic chung
15. *Cho* bất kỳ thao tác tạo/sửa booking, *khi* khoảng ngày overlap với booking khác có status Pending, Confirmed hoặc CheckedIn của cùng phòng, *thì* hệ thống từ chối và báo lỗi.
    - Cách kiểm chứng: đặt phòng A ngày 2-4, duyệt, check-in, check-out → khách khác tìm đúng ngày đó vẫn thấy và đặt được phòng A; khi đơn còn Pending/Confirmed/CheckedIn thì không thấy.
16. *Cho* mọi màn hình danh sách, *khi* API/DB lỗi hoặc mất kết nối, *thì* hiện thông báo lỗi + nút Thử lại, không crash.
    - Cách kiểm chứng: tạm dừng DB hoặc ném exception → UI vẫn ổn định.

### Triển khai (Docker / Render / CI)
17. *Cho* ứng dụng chạy trên Render, *khi* container khởi động, *thì* tự động chạy `Database.Migrate()` và seed tài khoản Admin (nếu chưa có) từ biến môi trường `ADMIN_EMAIL` / `ADMIN_PASSWORD`.
    - Cách kiểm chứng: xem log Render lúc start có dòng migrate + seed; đăng nhập bằng tài khoản Admin được seed.
18. *Cho* Dockerfile, *khi* build và chạy, *thì*:
    - Lắng nghe đúng cổng từ biến `PORT` lúc **runtime** (dùng `CMD ["sh","-c","ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet HotelBlazor.dll"]`).
    - Endpoint `/health` (MapHealthChecks + AllowAnonymous) trả 200, không redirect.
    - Cookie hoạt động đúng nhờ ForwardedHeaders (đã xóa KnownNetworks/KnownProxies) và Data Protection keys lưu DB (hoặc chấp nhận phải login lại sau restart).
    - Cách kiểm chứng: `curl /health` → 200; login → restart container → không lỗi proxy/redirect.
19. *Cho* deploy lên Render với PostgreSQL, *khi* redeploy, *thì* dữ liệu được giữ nguyên và migration mới (nếu có) được áp dụng tự động lúc khởi động.
    - Cách kiểm chứng: tạo dữ liệu → redeploy → dữ liệu còn; xem log có dòng migrate.
20. *Cho* code push lên `main`, *khi* GitHub Actions chạy xong, *thì* image mới xuất hiện trên Docker Hub và Render deploy bản mới (qua Deploy Hook).
    - Cách kiểm chứng: tab Actions xanh, Docker Hub có tag mới, mở URL Render thấy bản mới.

## 4. Ràng buộc kỹ thuật

- Tuân thủ toàn bộ quy tắc trong file `AGENTS.md`.
- Chỉ dùng **Interactive Server** (không WASM, không dự án Client riêng).
- Xác thực chỉ dùng Cookie Authentication + ASP.NET Core Identity (không JWT).
- Login / Register dùng Razor Page form POST.
- Entity tối thiểu: `ApplicationUser` (kế thừa IdentityUser), `RoomType`, `Room`, `Booking`.
- Trạng thái dùng enum hoặc string constant rõ ràng, không hard-code magic string lung tung.
- Mọi Service phải async.
- UI phải responsive cơ bản (mobile + desktop).
- Không dùng thư viện bên thứ 3 ngoài template Blazor mặc định và các package đã duyệt trong AGENTS.md.
- Database: PostgreSQL cho cả local và Render.
- Comment code bằng tiếng Việt ngắn gọn khi cần giải thích logic nghiệp vụ.
- Production: tự `Database.Migrate()` + seed Admin từ env; ForwardedHeaders (xóa KnownNetworks/KnownProxies); Data Protection keys lưu DB hoặc chấp nhận re-login sau restart.
- Chỉ dùng GitHub + Docker Hub + Render. Không thêm dịch vụ khác.

## 5. Phạm vi không làm (Out of scope cho phiên bản đơn giản)

- Thanh toán thật / cổng thanh toán Sandbox phức tạp (chỉ chọn phương thức + ghi nhận).
- Upload ảnh thật lên cloud (chỉ lưu URL chuỗi).
- Email / SMS thông báo.
- Báo cáo biểu đồ phức tạp (chỉ thẻ số liệu + bảng đơn giản).
- Multi-language.
- Real-time nâng cao giữa nhiều người dùng (thông báo đẩy, chat…). Blazor Server vẫn dùng SignalR cho circuit bình thường, không tắt.
- Interactive WebAssembly / tách API riêng chỉ để phục vụ WASM.
- Persistent disk / volume trên Render (free plan không hỗ trợ).
- Render tự build từ repo hoặc deploy bằng SSH/VPS.

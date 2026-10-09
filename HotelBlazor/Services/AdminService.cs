using System.ComponentModel.DataAnnotations;
using HotelBlazor.Data;
using HotelBlazor.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelBlazor.Services;

public sealed class AdminService
{
    private static readonly DateTimeOffset LockedUntil = DateTimeOffset.UtcNow.AddYears(100);
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<AdminDashboardData> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalRooms = await _dbContext.Rooms.CountAsync(cancellationToken);
        var availableRooms = await _dbContext.Rooms
            .CountAsync(room => room.Status == RoomStatus.Available, cancellationToken);
        var pendingBookings = await _dbContext.Bookings
            .CountAsync(booking => booking.Status == BookingStatus.Pending, cancellationToken);
        var expectedRevenue = await _dbContext.Bookings
            .Where(booking =>
                booking.Status == BookingStatus.Confirmed ||
                booking.Status == BookingStatus.CheckedIn ||
                booking.Status == BookingStatus.Completed)
            .SumAsync(booking => (decimal?)booking.TotalAmount, cancellationToken) ?? 0m;

        return new AdminDashboardData(totalRooms, availableRooms, pendingBookings, expectedRevenue);
    }

    public async Task<IReadOnlyList<AdminBookingRow>> GetBookingsAsync(
        BookingStatus? status,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Booking> query = _dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.User)
            .Include(booking => booking.Room)
            .OrderByDescending(booking => booking.CreatedAtUtc)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(booking => booking.Status == status.Value);
        }

        return await query
            .Take(100)
            .Select(booking => new AdminBookingRow
            {
                Id = booking.Id,
                GuestName = booking.User != null
                    ? booking.User.FullName ?? booking.User.Email ?? booking.GuestName
                    : booking.GuestName,
                GuestPhone = booking.GuestPhone,
                RoomNumber = booking.Room.RoomNumber,
                CheckIn = booking.CheckIn,
                CheckOut = booking.CheckOut,
                TotalAmount = booking.TotalAmount,
                Status = booking.Status,
                RejectionReason = booking.RejectionReason,
                CreatedAtUtc = booking.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomType>> GetRoomTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.RoomTypes
            .AsNoTracking()
            .OrderBy(roomType => roomType.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveRoomTypeAsync(RoomTypeEditModel model, CancellationToken cancellationToken = default)
    {
        ValidateRoomType(model);
        var name = model.Name.Trim();
        var duplicate = await _dbContext.RoomTypes.AnyAsync(
            roomType => roomType.Id != model.Id && roomType.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new AdminOperationException("Tên loại phòng này đã được sử dụng.");
        }

        RoomType roomType;
        if (model.Id == 0)
        {
            roomType = new RoomType();
            _dbContext.RoomTypes.Add(roomType);
        }
        else
        {
            roomType = await _dbContext.RoomTypes
                .SingleOrDefaultAsync(item => item.Id == model.Id, cancellationToken)
                ?? throw new AdminOperationException("Không tìm thấy loại phòng cần sửa.");
        }

        roomType.Name = name;
        roomType.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        roomType.Capacity = model.Capacity;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRoomTypeAsync(int roomTypeId, CancellationToken cancellationToken = default)
    {
        var roomType = await _dbContext.RoomTypes
            .SingleOrDefaultAsync(item => item.Id == roomTypeId, cancellationToken)
            ?? throw new AdminOperationException("Không tìm thấy loại phòng.");

        if (await _dbContext.Rooms.AnyAsync(room => room.RoomTypeId == roomTypeId, cancellationToken))
        {
            throw new AdminOperationException("Không thể xóa loại phòng đang có phòng thuộc loại này.");
        }

        _dbContext.RoomTypes.Remove(roomType);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Rooms
            .AsNoTracking()
            .Include(room => room.RoomType)
            .OrderBy(room => room.RoomNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveRoomAsync(RoomEditModel model, CancellationToken cancellationToken = default)
    {
        ValidateRoom(model);
        var roomNumber = model.RoomNumber.Trim();
        var duplicate = await _dbContext.Rooms.AnyAsync(
            room => room.Id != model.Id && room.RoomNumber.ToLower() == roomNumber.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new AdminOperationException("Số phòng này đã được sử dụng.");
        }

        if (!await _dbContext.RoomTypes.AnyAsync(type => type.Id == model.RoomTypeId, cancellationToken))
        {
            throw new AdminOperationException("Vui lòng chọn loại phòng hợp lệ.");
        }

        Room room;
        if (model.Id == 0)
        {
            room = new Room();
            _dbContext.Rooms.Add(room);
        }
        else
        {
            room = await _dbContext.Rooms
                .SingleOrDefaultAsync(item => item.Id == model.Id, cancellationToken)
                ?? throw new AdminOperationException("Không tìm thấy phòng cần sửa.");
        }

        room.RoomNumber = roomNumber;
        room.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        room.PricePerNight = model.PricePerNight;
        room.RoomTypeId = model.RoomTypeId;
        room.Status = model.Status;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRoomAsync(int roomId, CancellationToken cancellationToken = default)
    {
        var room = await _dbContext.Rooms
            .SingleOrDefaultAsync(item => item.Id == roomId, cancellationToken)
            ?? throw new AdminOperationException("Không tìm thấy phòng.");

        if (await _dbContext.Bookings.AnyAsync(booking => booking.RoomId == roomId, cancellationToken))
        {
            throw new AdminOperationException("Không thể xóa phòng đã có đơn đặt liên quan.");
        }

        _dbContext.Rooms.Remove(room);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUserRow>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var rows = new List<AdminUserRow>(users.Count);
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roles = await _userManager.GetRolesAsync(user);
            rows.Add(new AdminUserRow
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                Roles = roles.ToArray(),
                IsLockedOut = user.LockoutEnabled &&
                              user.LockoutEnd.HasValue &&
                              user.LockoutEnd.Value > DateTimeOffset.UtcNow
            });
        }

        return rows;
    }

    public async Task SetUserLockoutAsync(
        string targetUserId,
        string currentAdminId,
        bool lockAccount,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == currentAdminId)
        {
            throw new AdminOperationException("Bạn không thể khóa tài khoản đang sử dụng.");
        }

        var user = await _userManager.FindByIdAsync(targetUserId)
            ?? throw new AdminOperationException("Không tìm thấy tài khoản.");

        if (lockAccount && await _userManager.IsInRoleAsync(user, Role.Admin))
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync(Role.Admin);
            var otherAvailableAdmins = adminUsers.Count(admin =>
                admin.Id != targetUserId &&
                (!admin.LockoutEnabled || !admin.LockoutEnd.HasValue ||
                 admin.LockoutEnd.Value <= DateTimeOffset.UtcNow));
            if (otherAvailableAdmins == 0)
            {
                throw new AdminOperationException("Không thể khóa Admin cuối cùng còn hoạt động.");
            }
        }

        user.LockoutEnabled = true;
        var lockoutResult = await _userManager.SetLockoutEndDateAsync(
            user,
            lockAccount ? LockedUntil : null);
        EnsureIdentitySuccess(lockoutResult);

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        EnsureIdentitySuccess(stampResult);
    }

    public async Task CreateStaffAccountAsync(StaffAccountInput model, CancellationToken cancellationToken = default)
    {
        ValidateStaffAccount(model);

        if (await _userManager.FindByEmailAsync(model.Email.Trim()) is not null)
        {
            throw new AdminOperationException("Email này đã được đăng ký.");
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            throw new AdminOperationException(FormatIdentityErrors(createResult));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Không thể gán quyền và không thể dọn tài khoản mới tạo. Cần kiểm tra dữ liệu người dùng.");
            }

            throw new AdminOperationException(FormatIdentityErrors(roleResult));
        }
    }

    private static void ValidateRoomType(RoomTypeEditModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new AdminOperationException("Vui lòng nhập tên loại phòng.");
        }

        if (model.Capacity < 1)
        {
            throw new AdminOperationException("Sức chứa phải ít nhất là 1.");
        }
    }

    private static void ValidateRoom(RoomEditModel model)
    {
        if (string.IsNullOrWhiteSpace(model.RoomNumber))
        {
            throw new AdminOperationException("Vui lòng nhập số phòng.");
        }

        if (model.PricePerNight <= 0)
        {
            throw new AdminOperationException("Giá mỗi đêm phải lớn hơn 0.");
        }
    }

    private static void ValidateStaffAccount(StaffAccountInput model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) ||
            !new EmailAddressAttribute().IsValid(model.Email.Trim()))
        {
            throw new AdminOperationException("Vui lòng nhập email hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(model.FullName))
        {
            throw new AdminOperationException("Vui lòng nhập họ tên.");
        }

        if (model.Role != Role.Receptionist && model.Role != Role.Admin)
        {
            throw new AdminOperationException("Chỉ được tạo tài khoản Receptionist hoặc Admin.");
        }
    }

    private static string FormatIdentityErrors(IdentityResult result)
    {
        var errors = result.Errors.Select(error => error.Description).ToArray();
        if (errors.Any(error => error.Contains("email", StringComparison.OrdinalIgnoreCase)))
        {
            return "Email này đã được đăng ký.";
        }

        return $"Không thể hoàn tất thao tác tài khoản: {string.Join(" ", errors)}";
    }

    private static void EnsureIdentitySuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new AdminOperationException(FormatIdentityErrors(result));
        }
    }
}

public sealed record AdminDashboardData(int TotalRooms, int AvailableRooms, int PendingBookings, decimal ExpectedRevenue);

public sealed class AdminBookingRow
{
    public int Id { get; init; }
    public string GuestName { get; init; } = string.Empty;
    public string? GuestPhone { get; init; }
    public string RoomNumber { get; init; } = string.Empty;
    public DateOnly CheckIn { get; init; }
    public DateOnly CheckOut { get; init; }
    public decimal TotalAmount { get; init; }
    public BookingStatus Status { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class AdminUserRow
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public bool IsLockedOut { get; init; }
}

public sealed class AdminOperationException : Exception
{
    public AdminOperationException(string message) : base(message)
    {
    }
}

public sealed class RoomTypeEditModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên loại phòng.")]
    [StringLength(100, ErrorMessage = "Tên loại phòng không được vượt quá 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    public string? Description { get; set; }

    [Range(1, 100, ErrorMessage = "Sức chứa phải từ 1 đến 100 khách.")]
    public int Capacity { get; set; } = 2;
}

public sealed class RoomEditModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số phòng.")]
    [StringLength(20, ErrorMessage = "Số phòng không được vượt quá 20 ký tự.")]
    public string RoomNumber { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999", ErrorMessage = "Giá mỗi đêm phải lớn hơn 0.")]
    public decimal PricePerNight { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại phòng.")]
    public int RoomTypeId { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Available;
}

public sealed class StaffAccountInput
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(120, ErrorMessage = "Họ tên không được vượt quá 120 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    public string Role { get; set; } = HotelBlazor.Models.Role.Receptionist;
}

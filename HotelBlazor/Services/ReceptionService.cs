using System.Data;
using HotelBlazor.Data;
using HotelBlazor.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HotelBlazor.Services;

public sealed class ReceptionService
{
    private readonly ApplicationDbContext _dbContext;

    public ReceptionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Room>> GetRoomBoardAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Rooms
            .AsNoTracking()
            .Include(room => room.RoomType)
            .OrderBy(room => room.RoomNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Room>> GetAvailableRoomsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Status == RoomStatus.Available)
            .OrderBy(room => room.RoomNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Room)
            .ThenInclude(room => room.RoomType)
            .Where(booking => booking.Status == BookingStatus.Pending)
            .OrderBy(booking => booking.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetActiveBookingsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Room)
            .ThenInclude(room => room.RoomType)
            .Where(booking =>
                booking.Status == BookingStatus.Confirmed ||
                booking.Status == BookingStatus.CheckedIn)
            .OrderBy(booking => booking.CheckIn)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateRoomStatusAsync(
        int roomId,
        RoomStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var room = await _dbContext.Rooms
            .SingleOrDefaultAsync(candidate => candidate.Id == roomId, cancellationToken)
            ?? throw new ReceptionRequestException("Không tìm thấy phòng.");

        var isAllowed = (room.Status, newStatus) switch
        {
            (RoomStatus.Cleaning, RoomStatus.Available) => true,
            (RoomStatus.Available, RoomStatus.Maintenance) => true,
            (RoomStatus.Maintenance, RoomStatus.Available) => true,
            _ => false
        };

        if (!isAllowed)
        {
            throw new ReceptionRequestException("Không thể chuyển phòng từ trạng thái hiện tại sang trạng thái đã chọn.");
        }

        room.Status = newStatus;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await GetBookingForUpdateAsync(bookingId, cancellationToken);
        EnsureStatus(booking, BookingStatus.Pending, "Chỉ có thể duyệt đơn đang chờ xác nhận.");
        booking.Status = BookingStatus.Confirmed;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(
        int bookingId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ReceptionRequestException("Vui lòng nhập lý do từ chối.");
        }

        var booking = await GetBookingForUpdateAsync(bookingId, cancellationToken);
        EnsureStatus(booking, BookingStatus.Pending, "Chỉ có thể từ chối đơn đang chờ xác nhận.");
        booking.Status = BookingStatus.Cancelled;
        booking.RejectionReason = reason.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CheckInAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await GetBookingForUpdateAsync(bookingId, cancellationToken);
        EnsureStatus(booking, BookingStatus.Confirmed, "Chỉ có thể check-in đơn đã xác nhận.");
        if (booking.Room.Status != RoomStatus.Available)
        {
            throw new ReceptionRequestException("Phòng hiện không ở trạng thái sẵn sàng check-in.");
        }

        booking.Status = BookingStatus.CheckedIn;
        booking.Room.Status = RoomStatus.Occupied;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CheckOutAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await GetBookingForUpdateAsync(bookingId, cancellationToken);
        EnsureStatus(booking, BookingStatus.CheckedIn, "Chỉ có thể check-out đơn đang lưu trú.");

        booking.Status = BookingStatus.Completed;
        booking.Room.Status = RoomStatus.Cleaning;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Booking> CreateWalkInAsync(
        string guestName,
        string? guestPhone,
        int roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(guestName))
        {
            throw new ReceptionRequestException("Vui lòng nhập tên khách.");
        }

        if (checkOut <= checkIn)
        {
            throw new ReceptionRequestException("Ngày trả phòng phải sau ngày nhận phòng.");
        }

        if (checkIn < DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ReceptionRequestException("Ngày nhận phòng không được ở quá khứ.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var room = await _dbContext.Rooms
            .SingleOrDefaultAsync(candidate => candidate.Id == roomId, cancellationToken)
            ?? throw new ReceptionRequestException("Không tìm thấy phòng.");

        if (room.Status != RoomStatus.Available)
        {
            throw new ReceptionRequestException("Phòng hiện không sẵn sàng để đặt.");
        }

        var hasOverlap = await _dbContext.Bookings
            .Where(BookingOverlap.IsBlockingForDateRange(checkIn, checkOut))
            .AnyAsync(booking => booking.RoomId == roomId, cancellationToken);
        if (hasOverlap)
        {
            throw new ReceptionRequestException("Phòng đã có đơn trong khoảng ngày này. Vui lòng chọn phòng hoặc ngày khác.");
        }

        var booking = new Booking
        {
            GuestName = guestName.Trim(),
            GuestPhone = string.IsNullOrWhiteSpace(guestPhone) ? null : guestPhone.Trim(),
            UserId = null,
            RoomId = roomId,
            CheckIn = checkIn,
            CheckOut = checkOut,
            TotalAmount = (checkOut.DayNumber - checkIn.DayNumber) * room.PricePerNight,
            Status = BookingStatus.Confirmed
        };

        _dbContext.Bookings.Add(booking);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSerializationFailure(exception))
        {
            _dbContext.Entry(booking).State = EntityState.Detached;
            throw new ReceptionRequestException(
                "Phòng vừa được đặt trong khoảng ngày này. Vui lòng chọn phòng hoặc ngày khác.",
                exception);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            _dbContext.Entry(booking).State = EntityState.Detached;
            throw new ReceptionRequestException(
                "Phòng vừa được đặt trong khoảng ngày này. Vui lòng chọn phòng hoặc ngày khác.",
                exception);
        }
        catch
        {
            _dbContext.Entry(booking).State = EntityState.Detached;
            throw;
        }

        return booking;
    }

    private async Task<Booking> GetBookingForUpdateAsync(int bookingId, CancellationToken cancellationToken)
    {
        return await _dbContext.Bookings
            .Include(booking => booking.Room)
            .SingleOrDefaultAsync(booking => booking.Id == bookingId, cancellationToken)
            ?? throw new ReceptionRequestException("Không tìm thấy đơn đặt phòng.");
    }

    private static void EnsureStatus(Booking booking, BookingStatus expected, string message)
    {
        if (booking.Status != expected)
        {
            throw new ReceptionRequestException(message);
        }
    }

    private static bool IsSerializationFailure(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.SerializationFailure
        };
    }
}

public sealed class ReceptionRequestException : Exception
{
    public ReceptionRequestException(string message) : base(message)
    {
    }

    public ReceptionRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

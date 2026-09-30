using System.Data;
using HotelBlazor.Data;
using HotelBlazor.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HotelBlazor.Services;

public sealed class BookingService
{
    private readonly ApplicationDbContext _dbContext;

    public BookingService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Booking> CreateAsync(
        int roomId,
        string userId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken = default)
    {
        ValidateDates(checkIn, checkOut);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Không xác định được người dùng hiện tại.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var room = await _dbContext.Rooms
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == roomId, cancellationToken);

        if (room is null)
        {
            throw new BookingRequestException("Phòng không còn tồn tại.");
        }

        if (room.Status != RoomStatus.Available)
        {
            throw new BookingRequestException("Phòng hiện không thể đặt.");
        }

        var hasOverlap = await HasOverlappingBookingAsync(
            roomId,
            checkIn,
            checkOut,
            cancellationToken);
        if (hasOverlap)
        {
            throw new BookingRequestException("Phòng vừa được đặt trong khoảng ngày này. Vui lòng tìm phòng khác.");
        }

        var booking = new Booking
        {
            CustomerId = userId,
            RoomId = roomId,
            CheckIn = checkIn,
            CheckOut = checkOut,
            TotalAmount = (checkOut.DayNumber - checkIn.DayNumber) * room.PricePerNight,
            Status = BookingStatus.Pending
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
            throw new BookingRequestException(
                "Phòng vừa được đặt trong khoảng ngày này. Vui lòng tìm phòng khác.",
                exception);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            _dbContext.Entry(booking).State = EntityState.Detached;
            throw new BookingRequestException(
                "Phòng vừa được đặt trong khoảng ngày này. Vui lòng tìm phòng khác.",
                exception);
        }
        catch
        {
            _dbContext.Entry(booking).State = EntityState.Detached;
            throw;
        }

        return booking;
    }

    public async Task<IReadOnlyList<Booking>> GetForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);

        return await _dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Room)
            .ThenInclude(room => room.RoomType)
            .Where(booking => booking.CustomerId == userId)
            .OrderByDescending(booking => booking.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task CancelPendingAsync(
        int bookingId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);

        var booking = await _dbContext.Bookings
            .SingleOrDefaultAsync(candidate => candidate.Id == bookingId, cancellationToken);

        if (booking is null || booking.CustomerId != userId)
        {
            throw new BookingRequestException("Không tìm thấy đơn đặt phòng của bạn.");
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new BookingRequestException("Chỉ có thể hủy đơn đang chờ xác nhận.");
        }

        booking.Status = BookingStatus.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasOverlappingBookingAsync(
        int roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Bookings.AnyAsync(booking =>
            booking.RoomId == roomId &&
            booking.Status != BookingStatus.Cancelled &&
            booking.CheckIn < checkOut &&
            booking.CheckOut > checkIn,
            cancellationToken);
    }

    private static void ValidateDates(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn)
        {
            throw new BookingRequestException("Ngày trả phòng phải sau ngày nhận phòng.");
        }

        if (checkIn < DateOnly.FromDateTime(DateTime.Today))
        {
            throw new BookingRequestException("Ngày nhận phòng không được ở quá khứ.");
        }
    }

    private static void EnsureUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Không xác định được người dùng hiện tại.");
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

public sealed class BookingRequestException : Exception
{
    public BookingRequestException(string message) : base(message)
    {
    }

    public BookingRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

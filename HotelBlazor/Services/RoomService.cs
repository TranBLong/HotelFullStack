using HotelBlazor.Data;
using HotelBlazor.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelBlazor.Services;

public sealed class RoomService
{
    private readonly ApplicationDbContext _dbContext;

    public RoomService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Room>> SearchAvailableAsync(
        RoomSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ValidateCriteria(criteria);

        IQueryable<Room> rooms = _dbContext.Rooms
            .AsNoTracking()
            .Include(room => room.RoomType)
            .Where(room => room.Status == RoomStatus.Available &&
                           room.RoomType.Capacity >= criteria.GuestCount);

        if (criteria.MinPrice.HasValue)
        {
            rooms = rooms.Where(room => room.PricePerNight >= criteria.MinPrice.Value);
        }

        if (criteria.MaxPrice.HasValue)
        {
            rooms = rooms.Where(room => room.PricePerNight <= criteria.MaxPrice.Value);
        }

        if (criteria.RoomTypeId.HasValue)
        {
            rooms = rooms.Where(room => room.RoomTypeId == criteria.RoomTypeId.Value);
        }

        var blockingRoomIds = _dbContext.Bookings
            .Where(BookingOverlap.IsBlockingForDateRange(criteria.CheckIn, criteria.CheckOut))
            .Select(booking => booking.RoomId);

        return await rooms
            .Where(room => !blockingRoomIds.Contains(room.Id))
            .OrderBy(room => room.PricePerNight)
            .ThenBy(room => room.RoomNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomType>> GetRoomTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RoomTypes
            .AsNoTracking()
            .OrderBy(roomType => roomType.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Room?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Rooms
            .AsNoTracking()
            .Include(room => room.RoomType)
            .SingleOrDefaultAsync(room => room.Id == id, cancellationToken);
    }

    private static void ValidateCriteria(RoomSearchCriteria criteria)
    {
        if (criteria.CheckOut <= criteria.CheckIn)
        {
            throw new ArgumentException("Ngày trả phòng phải sau ngày nhận phòng.");
        }

        if (criteria.CheckIn < DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ArgumentException("Ngày nhận phòng không được ở quá khứ.");
        }

        if (criteria.GuestCount < 1)
        {
            throw new ArgumentException("Số khách phải ít nhất là 1.");
        }

        if (criteria.MinPrice < 0 || criteria.MaxPrice < 0)
        {
            throw new ArgumentException("Khoảng giá không thể âm.");
        }

        if (criteria.MinPrice.HasValue && criteria.MaxPrice.HasValue &&
            criteria.MinPrice.Value > criteria.MaxPrice.Value)
        {
            throw new ArgumentException("Giá tối thiểu không thể lớn hơn giá tối đa.");
        }
    }
}

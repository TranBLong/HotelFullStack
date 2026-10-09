using System.Linq.Expressions;
using HotelBlazor.Models;

namespace HotelBlazor.Services;

public static class BookingOverlap
{
    public static Expression<Func<Booking, bool>> IsBlockingForDateRange(
        DateOnly checkIn,
        DateOnly checkOut)
    {
        return booking =>
            (booking.Status == BookingStatus.Pending ||
             booking.Status == BookingStatus.Confirmed ||
             booking.Status == BookingStatus.CheckedIn) &&
            booking.CheckIn < checkOut &&
            booking.CheckOut > checkIn;
    }
}

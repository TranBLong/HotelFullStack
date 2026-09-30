namespace HotelBlazor.Services;

public sealed class RoomSearchCriteria
{
    public DateOnly CheckIn { get; init; }
    public DateOnly CheckOut { get; init; }
    public int GuestCount { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? RoomTypeId { get; init; }
}

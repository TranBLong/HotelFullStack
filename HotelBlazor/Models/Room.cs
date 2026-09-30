namespace HotelBlazor.Models;

public class Room
{
    public int Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerNight { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

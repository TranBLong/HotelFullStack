namespace HotelBlazor.Models;

public class Booking
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string? GuestPhone { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

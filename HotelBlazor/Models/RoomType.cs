namespace HotelBlazor.Models;

public class RoomType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}

using Microsoft.AspNetCore.Identity;

namespace HotelBlazor.Models;

public class ApplicationUser : IdentityUser
{
    // Có thể thêm các thuộc tính mở rộng sau (ví dụ: FullName)
    public string? FullName { get; set; }
}
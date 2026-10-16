using HotelBlazor.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelBlazor.Data;

public static class DbInitializer
{
    private static readonly IReadOnlyDictionary<string, string> RoomImageUrls =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Standard"] = "/images/rooms/standard-1.jpg",
            ["Deluxe"] = "/images/rooms/deluxe-1.jpg",
            ["Suite"] = "/images/rooms/suite-1.jpg"
        };

    public static async Task InitializeAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var roleName in Role.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
            EnsureSuccess(roleResult, $"Không thể tạo role {roleName}.");
        }

        await SeedAdminAsync(userManager, logger);
        await SeedRoomTypesAsync(dbContext, cancellationToken);
        await SeedRoomsAsync(dbContext, cancellationToken);
        await BackfillRoomImagesAsync(dbContext, cancellationToken);
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        if ((await userManager.GetUsersInRoleAsync(Role.Admin)).Count != 0)
        {
            return;
        }

        var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Bỏ qua seed Admin vì chưa cấu hình ADMIN_EMAIL và ADMIN_PASSWORD.");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Administrator"
        };

        var createResult = await userManager.CreateAsync(admin, password);
        EnsureSuccess(createResult, "Không thể tạo tài khoản Admin.");

        var roleResult = await userManager.AddToRoleAsync(admin, Role.Admin);
        EnsureSuccess(roleResult, "Không thể gán role Admin cho tài khoản.");
        logger.LogInformation("Đã seed tài khoản Admin từ cấu hình môi trường.");
    }

    private static async Task SeedRoomTypesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.RoomTypes.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.RoomTypes.AddRange(
            new RoomType { Name = "Standard", Description = "Phòng tiêu chuẩn", Capacity = 2 },
            new RoomType { Name = "Deluxe", Description = "Phòng rộng, tiện nghi nâng cấp", Capacity = 3 },
            new RoomType { Name = "Suite", Description = "Phòng suite dành cho gia đình", Capacity = 4 });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRoomsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Rooms.AnyAsync(cancellationToken))
        {
            return;
        }

        var roomTypes = await dbContext.RoomTypes
            .OrderBy(roomType => roomType.Id)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (roomTypes.Count == 0)
        {
            throw new InvalidOperationException("Không thể seed phòng mẫu khi chưa có loại phòng.");
        }

        var rooms = new List<Room>();
        for (var index = 1; index <= 6; index++)
        {
            var roomType = roomTypes[(index - 1) % roomTypes.Count];
            rooms.Add(new Room
            {
                RoomNumber = $"10{index}",
                Description = $"{roomType.Name} - phòng {index}",
                ImageUrl = GetRoomImageUrl(roomType.Name),
                PricePerNight = 500000m + (index * 250000m),
                Status = RoomStatus.Available,
                RoomTypeId = roomType.Id
            });
        }

        dbContext.Rooms.AddRange(rooms);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task BackfillRoomImagesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rooms = await dbContext.Rooms
            .Include(room => room.RoomType)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var room in rooms)
        {
            if (string.IsNullOrWhiteSpace(room.ImageUrl) &&
                RoomImageUrls.TryGetValue(room.RoomType.Name, out var imageUrl))
            {
                room.ImageUrl = imageUrl;
                changed = true;
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string? GetRoomImageUrl(string roomTypeName) =>
        RoomImageUrls.TryGetValue(roomTypeName, out var imageUrl) ? imageUrl : null;

    private static void EnsureSuccess(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"{message} {errors}");
        }
    }
}

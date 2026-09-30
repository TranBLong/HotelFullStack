using HotelBlazor.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HotelBlazor.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RoomType>(entity =>
        {
            entity.Property(roomType => roomType.Name).HasMaxLength(100).IsRequired();
            entity.Property(roomType => roomType.Description).HasMaxLength(500);
        });

        builder.Entity<Room>(entity =>
        {
            entity.HasIndex(room => room.RoomNumber).IsUnique();
            entity.Property(room => room.RoomNumber).HasMaxLength(20).IsRequired();
            entity.Property(room => room.Description).HasMaxLength(500);
            entity.Property(room => room.PricePerNight).HasPrecision(18, 2);
            entity.Property(room => room.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(room => room.RoomType)
                .WithMany(roomType => roomType.Rooms)
                .HasForeignKey(room => room.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Booking>(entity =>
        {
            entity.Property(booking => booking.TotalAmount).HasPrecision(18, 2);
            entity.Property(booking => booking.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(booking => booking.RejectionReason).HasMaxLength(1000);
            entity.HasOne(booking => booking.Room)
                .WithMany(room => room.Bookings)
                .HasForeignKey(booking => booking.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.Customer)
                .WithMany(customer => customer.Bookings)
                .HasForeignKey(booking => booking.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
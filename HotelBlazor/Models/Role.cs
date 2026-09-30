namespace HotelBlazor.Models;

public static class Role
{
    public const string Customer = "Customer";
    public const string Receptionist = "Receptionist";
    public const string Admin = "Admin";
    public const string ReceptionistOrAdmin = Receptionist + "," + Admin;

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly(new[] { Customer, Receptionist, Admin });
}

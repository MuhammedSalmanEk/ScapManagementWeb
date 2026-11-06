namespace ScrapManagement.Web.Infrastructure;

public class AppOptions
{
    public const string SectionName = "App";
    public string Name { get; set; } = "Scrap Shop Manager";
    public string CurrencySymbol { get; set; } = "₹";
    /// <summary>IANA id (Asia/Kolkata) or Windows id (India Standard Time). Used for "today" and displayed times.</summary>
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
}

public class SeedOptions
{
    public const string SectionName = "Seed";
    public string MainAdminEmail { get; set; } = "admin@scrap.local";
    public string MainAdminPassword { get; set; } = "Admin@12345";
    public string MainAdminName { get; set; } = "Main Admin";
}

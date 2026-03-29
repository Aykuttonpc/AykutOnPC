namespace AykutOnPC.Core.Configuration;

public class SeedDataSettings
{
    public const string SectionName = "SeedData";

    public AdminUserSettings AdminUser { get; set; } = new();
    public string GitHubUsername { get; set; } = "Aykuttonpc";
    public string HeroTitle { get; set; } = "Hello";
    public string HeroSubtitle { get; set; } = "Developer";
    public List<SpecSeed> Specs { get; set; } = new();
}

public class AdminUserSettings
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
}

public class SpecSeed
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Proficiency { get; set; }
}

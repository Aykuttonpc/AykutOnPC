namespace AykutOnPC.Core.Configuration;

public class SecuritySettings
{
    public const string SectionName = "SecuritySettings";

    public int BCryptWorkFactor { get; set; } = 12;
    public string DataProtectionPath { get; set; } = "/app/keys";
    public string ApplicationName { get; set; } = "AykutOnPC";
}

namespace CraftorAI.Email.Configuration;

public class ResendConfiguration
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;
}

namespace CraftorAI.Email.Contracts;

public class EmailSendResponse
{
    public EmailSendResult Result { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess => Result == EmailSendResult.Success;
    public bool ShouldRetry { get; set; } = false;
    public string? MessageId { get; set; }
}

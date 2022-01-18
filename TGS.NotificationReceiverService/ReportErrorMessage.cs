namespace TGS.NotificationReceiverService
{
    public record ReportErrorMessage(string Message, string? StackTrace, string? InnerMessage);
}
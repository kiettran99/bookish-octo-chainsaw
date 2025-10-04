namespace Common.Shared.Models.Emails;

public class SendEmailMessage
{
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public List<string> ToEmails { get; set; } = null!;
    public List<string>? CcEmails { get; set; }
    public List<EmailAttachment>? Attachments { get; set; }
}

public class EmailAttachment
{
    public string FileName { get; set; } = null!;

    public byte[] Attachment { get; set; } = null!;
}
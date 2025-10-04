namespace Email.Models;

public class EmailAttachment
{
    public string FileName { get; set; } = null!;

    public byte[] Attachment { get; set; } = null!;
}
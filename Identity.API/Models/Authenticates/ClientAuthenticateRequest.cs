using System.ComponentModel.DataAnnotations;

namespace Identity.Domain.Models.Authenticates;

public class ClientAuthenticateRequest
{
    [Required(ErrorMessage = "error_client_authenticate_provideraccountid")]
    public string ProviderAccountId { get; set; } = null!;

    [Required(ErrorMessage = "error_client_authenticate_name")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "error_client_authenticate_email")]
    public string Email { get; set; } = null!;

    public string? Image { get; set; }

    public string? Region { get; set; }
    public bool EmailVerified { get; set; }

    public string? IpAddress { get; set; }
    public string? BrowserFingerprint { get; set; }
}

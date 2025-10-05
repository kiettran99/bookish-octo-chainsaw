using System.ComponentModel.DataAnnotations;

namespace Identity.Domain.Models.Authenticates;

public class GoogleAuthenticateRequest
{
    [Required(ErrorMessage = "error_google_authenticate_email")]
    public string Email { get; set; } = null!;

    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Picture { get; set; }
    public string? Locale { get; set; }
    public bool EmailVerified { get; set; }

    public string? ProviderAccountId { get; set; }
}

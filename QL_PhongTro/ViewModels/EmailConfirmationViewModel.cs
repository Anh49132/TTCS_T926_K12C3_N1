using System.ComponentModel.DataAnnotations;
namespace QL_PhongTro.ViewModels;
public sealed class EmailConfirmationViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(6, MinimumLength = 6)] public string Code { get; set; } = "";
}

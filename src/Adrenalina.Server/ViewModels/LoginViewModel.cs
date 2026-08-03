using System.ComponentModel.DataAnnotations;

namespace Adrenalina.Server.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o login.")]
    [StringLength(64)]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(256)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

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

public sealed class AdminAccessRecoveryViewModel
{
    public string TemporaryPassword { get; set; } = string.Empty;
    public string AccessFilePath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class ChangeAdminPasswordViewModel
{
    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(256, MinimumLength = 12, ErrorMessage = "A senha precisa ter entre 12 e 256 caracteres.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a nova senha.")]
    [Compare(nameof(NewPassword), ErrorMessage = "As senhas não conferem.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

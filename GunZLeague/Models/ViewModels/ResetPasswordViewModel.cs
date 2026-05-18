using System.ComponentModel.DataAnnotations;

namespace GunZLeague.Models.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a new password.")]
        [StringLength(20, MinimumLength = 4, ErrorMessage = "Password must be between 4 and 20 characters.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm your new password.")]
        [Compare(nameof(NewPassword), ErrorMessage = "New passwords do not match.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

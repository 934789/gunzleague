using System.ComponentModel.DataAnnotations;

namespace GunZLeague.Models.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(50, ErrorMessage = "Email must be 50 characters or less.")]
        public string Email { get; set; } = string.Empty;
    }
}

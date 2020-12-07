using System.ComponentModel.DataAnnotations;

namespace SimpleAPI_NetCore50.Schemas
{
    public class AuthRequest
    {
        [EmailAddress]
        [Required(ErrorMessage ="Email is Required")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Password is Required")]
        public string Password { get; set; }
        public string Role { get; set; }
    }
}

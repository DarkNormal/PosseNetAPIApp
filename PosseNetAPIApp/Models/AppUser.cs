using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosseNetAPIApp.Models
{
    public class AppUser
    {
        [Key]
        public int UserID { get; set; }
        [Required(ErrorMessage = "Name is Required!")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Email Address is required!")]
        [EmailAddress]
        public string Email { get; set; }
        [Required(ErrorMessage = "Password Required")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Must enter a username")]
        public string Username { get; set; }
        public ICollection<Event> AttendEvents { get; set; }
    }

    public class AppUserDTO
    {
        [Key]
        public int UserID { get; set; }
        [Required(ErrorMessage = "Name is Required!")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Email Address is required!")]
        [EmailAddress]
        public string Email { get; set; }
        [Required(ErrorMessage = "Password Required")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Must enter a username")]
        public string Username { get; set; }
        public ICollection<Event> AttendEvents { get; set; }
    }
}

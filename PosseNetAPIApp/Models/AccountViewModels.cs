using System;
using System.Collections.Generic;

namespace PosseNetAPIApp.Models
{
    // Models returned by AccountController actions.

    public class ManageInfoViewModel
    {
        public string LocalLoginProvider { get; set; }

        public string Email { get; set; }

        public IEnumerable<UserLoginInfoViewModel> Logins { get; set; }

        public IEnumerable<ExternalLoginViewModel> ExternalLoginProviders { get; set; }
    }

    public class UserInfoViewModel
    {
        public string Username { get; set; }

        public bool HasRegistered { get; set; }

        public string LoginProvider { get; set; }

        public ICollection<UserBasicDetailsModel> Followers { get; set; }
        public ICollection<UserBasicDetailsModel> Following { get; set; }
        public ICollection<Event> Events { get; set; }
    }

    public class UserLoginInfoViewModel
    {
        public string LoginProvider { get; set; }

        public string ProviderKey { get; set; }
    }
}

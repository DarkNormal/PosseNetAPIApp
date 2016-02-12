﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OAuth;
using PosseNetAPIApp.Models;
using PosseNetAPIApp.Providers;
using PosseNetAPIApp.Results;
using System.Net;
using System.Linq;
using Microsoft.Owin.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PosseNetAPIApp.Controllers
{
    [Authorize]
    [RoutePrefix("api/Account")]
    public class AccountController : ApiController
    {
        private const string LocalLoginProvider = "Local";
        private ApplicationUserManager _userManager;
        private ApplicationDbContext db = new ApplicationDbContext();

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager,
            ISecureDataFormat<AuthenticationTicket> accessTokenFormat)
        {
            UserManager = userManager;
            AccessTokenFormat = accessTokenFormat;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //Not pretty code - need to figure out a more elegant way of doing this
        [AllowAnonymous]
        [HttpGet]
        [Route("GetUsersFriendStatus")]
        public IHttpActionResult GetUsers(string username)
        {
            //get friends of the supplied username
            var friends = new FriendRelationshipsController().GetFriendRelationships(username);
            //get all users except for the calling user
            var users = db.Users.Where(x => x.UserName.Equals(username) == false).Select(x => x.UserName).ToList();
            List<BasicFriendDetails> returnList = new List<BasicFriendDetails>();
            foreach (string user in users)
            {
                //add all users to a list, set IsFriend to false by default
                returnList.Add(new BasicFriendDetails(user, false));
            }
            foreach (BasicFriendDetails friend in returnList)
            {
                foreach (FriendRelationships addedFriend in friends)
                {
                    //if the name matches, change the IsFriend property to true
                    if (friend.Username.Equals(addedFriend.ToUsername, StringComparison.OrdinalIgnoreCase) ||
                        friend.Username.Equals(addedFriend.FromUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        friend.IsFriend = true;
                    }
                }

            }

            return Ok(returnList);
        }


        //to be used for any validation of users existing, primarily for initial check before /Token to recieve username if email provided
        // POST api/Account/CheckAccoutExist
        [HttpPost]
        [AllowAnonymous]
        [Route("TokenLogin")]
        public async Task<HttpResponseMessage> LoginUser(UserAccountBindingModel model)     //username and password, can also be email
        {
            if (!ModelState.IsValid)
            {
                return Request.CreateResponse(HttpStatusCode.PreconditionFailed, "Invalid data entered");            //mode state is invlaid
            }
            if (model.Username.Contains("@"))                                               //if the username contains the @ symbol, prevented at registration to prevent confusion
            {
                var user = await UserManager.FindByEmailAsync(model.Username);              //find the user by email first
                if (user != null)                                                           //if success, then perform password check
                {
                    bool passwordCheck = await UserManager.CheckPasswordAsync(user, model.Password);
                    if (passwordCheck)
                    {
                        model.Username = user.UserName;                                         //if password also matches, set the model username (in this case its an email) to the associated username
                        return Request.CreateResponse(HttpStatusCode.OK, model);                //return the model back to the client (with username)
                    }
                    else
                    {
                        return Request.CreateErrorResponse(HttpStatusCode.PreconditionFailed, "Account with that email/username pasword combo not found");
                    }
                }
                else
                {

                    return Request.CreateResponse(HttpStatusCode.PreconditionFailed, "Account with that email/username pasword combo not found");            //user not found
                }
            }
            else
            {
                var checkUser = await UserManager.FindAsync(model.Username, model.Password);    //finds users based on username and password
                if (checkUser != null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, model);                    //found user, return model as it satisfies the requirements
                }
                else
                {
                    return Request.CreateErrorResponse(HttpStatusCode.PreconditionFailed, "Account with that email/username pasword combo not found");
                }
            }

        }

        public ISecureDataFormat<AuthenticationTicket> AccessTokenFormat { get; private set; }

        // GET api/Account/UserInfo
        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("UserInfo")]
        public UserInfoViewModel GetUserInfo()
        {
            ExternalLoginData externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            return new UserInfoViewModel
            {
                Email = User.Identity.GetUserName(),
                HasRegistered = externalLogin == null,
                LoginProvider = externalLogin != null ? externalLogin.LoginProvider : null
            };
        }

        // POST api/Account/Logout
        [Route("Logout")]
        public IHttpActionResult Logout()
        {
            Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
            return Ok();
        }

        // GET api/Account/ManageInfo?returnUrl=%2F&generateState=true
        [Route("ManageInfo")]
        public async Task<ManageInfoViewModel> GetManageInfo(string returnUrl, bool generateState = false)
        {
            IdentityUser user = await UserManager.FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return null;
            }

            List<UserLoginInfoViewModel> logins = new List<UserLoginInfoViewModel>();

            foreach (IdentityUserLogin linkedAccount in user.Logins)
            {
                logins.Add(new UserLoginInfoViewModel
                {
                    LoginProvider = linkedAccount.LoginProvider,
                    ProviderKey = linkedAccount.ProviderKey
                });
            }

            if (user.PasswordHash != null)
            {
                logins.Add(new UserLoginInfoViewModel
                {
                    LoginProvider = LocalLoginProvider,
                    ProviderKey = user.UserName,
                });
            }

            return new ManageInfoViewModel
            {
                LocalLoginProvider = LocalLoginProvider,
                Email = user.UserName,
                Logins = logins,
                ExternalLoginProviders = GetExternalLogins(returnUrl, generateState)
            };
        }
        //TODO remove allow anonymous
        [HttpPost]
        [AllowAnonymous]
        [Route("ChangeUsername")]
        public async Task<IHttpActionResult> ChangeUsername(RegisterBindingModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUsername = db.Users.Where(x => x.UserName.Equals(model.Username, StringComparison.OrdinalIgnoreCase)).ToList(); //finds any users with existing username
                if (existingUsername.Count < 1) //the chosen username doesn't exist
                {
                    var u = UserManager.FindByEmail(model.Email);       //finds the user details based on email
                    if (u != null)    //if the email checks out with an existing user
                    {
                        bool passwordCheck = await UserManager.CheckPasswordAsync(u, model.Password);   //check the password is correct to the user
                        if (passwordCheck)
                        {
                            u.UserName = model.Username;
                            UserManager.Update(u);
                            return Json(new { success = true });
                        }
                        return Json(new { success = false, cause = "Incorrect password supplied" });
                    }
                    return Json(new { success = false, cause = "User not found" });
                }
                return Json(new
                {
                    success = false,
                    cause = "Username is already taken"
                });
            }
            return BadRequest(ModelState);

        }

        // POST api/Account/ChangePassword
        [Route("ChangePassword")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> ChangePassword(ChangePasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var u = UserManager.FindByEmail(model.Email);
            if (u != null)
            {
                IdentityResult result = await UserManager.ChangePasswordAsync(u.Id, model.OldPassword,
                    model.NewPassword);

                if (!result.Succeeded)
                {
                    return Json(new { success = false, cause = "Invalid password provided" });
                }

                return Json(new { success = true});
            }
            return Json(new { success = false, cause = "Invalid user / password provided" });
        }

        // POST api/Account/SetPassword
        [Route("SetPassword")]
        public async Task<IHttpActionResult> SetPassword(SetPasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // POST api/Account/AddExternalLogin
        [Route("AddExternalLogin")]
        public async Task<IHttpActionResult> AddExternalLogin(AddExternalLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);

            AuthenticationTicket ticket = AccessTokenFormat.Unprotect(model.ExternalAccessToken);

            if (ticket == null || ticket.Identity == null || (ticket.Properties != null
                && ticket.Properties.ExpiresUtc.HasValue
                && ticket.Properties.ExpiresUtc.Value < DateTimeOffset.UtcNow))
            {
                return BadRequest("External login failure.");
            }

            ExternalLoginData externalData = ExternalLoginData.FromIdentity(ticket.Identity);

            if (externalData == null)
            {
                return BadRequest("The external login is already associated with an account.");
            }

            IdentityResult result = await UserManager.AddLoginAsync(User.Identity.GetUserId(),
                new UserLoginInfo(externalData.LoginProvider, externalData.ProviderKey));

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // POST api/Account/RemoveLogin
        [Route("RemoveLogin")]
        public async Task<IHttpActionResult> RemoveLogin(RemoveLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result;

            if (model.LoginProvider == LocalLoginProvider)
            {
                result = await UserManager.RemovePasswordAsync(User.Identity.GetUserId());
            }
            else
            {
                result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(),
                    new UserLoginInfo(model.LoginProvider, model.ProviderKey));
            }

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // GET api/Account/ExternalLogin
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalCookie)]
        [AllowAnonymous]
        [Route("ExternalLogin", Name = "ExternalLogin")]
        public async Task<IHttpActionResult> GetExternalLogin(string provider, string error = null)
        {
            if (error != null)
            {
                return Redirect(Url.Content("~/") + "#error=" + Uri.EscapeDataString(error));
            }

            if (!User.Identity.IsAuthenticated)
            {
                return new ChallengeResult(provider, this);
            }

            ExternalLoginData externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            if (externalLogin == null)
            {
                return InternalServerError();
            }

            if (externalLogin.LoginProvider != provider)
            {
                Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);
                return new ChallengeResult(provider, this);
            }

            ApplicationUser user = await UserManager.FindAsync(new UserLoginInfo(externalLogin.LoginProvider,
                externalLogin.ProviderKey));

            bool hasRegistered = user != null;

            if (hasRegistered)
            {
                Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);

                ClaimsIdentity oAuthIdentity = await user.GenerateUserIdentityAsync(UserManager,
                   OAuthDefaults.AuthenticationType);
                ClaimsIdentity cookieIdentity = await user.GenerateUserIdentityAsync(UserManager,
                    CookieAuthenticationDefaults.AuthenticationType);

                AuthenticationProperties properties = ApplicationOAuthProvider.CreateProperties(user.UserName);
                Authentication.SignIn(properties, oAuthIdentity, cookieIdentity);
            }
            else
            {
                IEnumerable<Claim> claims = externalLogin.GetClaims();
                ClaimsIdentity identity = new ClaimsIdentity(claims, OAuthDefaults.AuthenticationType);
                Authentication.SignIn(identity);
            }

            return Ok();
        }

        // GET api/Account/ExternalLogins?returnUrl=%2F&generateState=true
        [AllowAnonymous]
        [Route("ExternalLogins")]
        public IEnumerable<ExternalLoginViewModel> GetExternalLogins(string returnUrl, bool generateState = false)
        {
            IEnumerable<AuthenticationDescription> descriptions = Authentication.GetExternalAuthenticationTypes();
            List<ExternalLoginViewModel> logins = new List<ExternalLoginViewModel>();

            string state;

            if (generateState)
            {
                const int strengthInBits = 256;
                state = RandomOAuthStateGenerator.Generate(strengthInBits);
            }
            else
            {
                state = null;
            }

            foreach (AuthenticationDescription description in descriptions)
            {
                ExternalLoginViewModel login = new ExternalLoginViewModel
                {
                    Name = description.Caption,
                    Url = Url.Route("ExternalLogin", new
                    {
                        provider = description.AuthenticationType,
                        response_type = "token",
                        client_id = Startup.PublicClientId,
                        redirect_uri = new Uri(Request.RequestUri, returnUrl).AbsoluteUri,
                        state = state
                    }),
                    State = state
                };
                logins.Add(login);
            }

            return logins;
        }

        // POST api/Account/Register
        [AllowAnonymous]
        [Route("Register")]
        public async Task<HttpResponseMessage> Register(RegisterBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, ModelState);
            }

            var user = new ApplicationUser() { UserName = model.Username, Email = model.Email };

            IdentityResult result = await UserManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, result);
            }

            return Request.CreateResponse(HttpStatusCode.OK, model);
        }


        //fbtoken is the ACCESS_TOKEN received from Facebook via the Android client
        [HttpGet]
        [AllowAnonymous]
        [Route("VerifyFacebookLogin")]
        public async Task<IHttpActionResult> FacebookLogin(string fbtoken)
        {

            //attempts to retrieve information of the user from Facebook API
            var altverified = await VerifyExternalAccessToken("Facebook", fbtoken);
            var facebookUser = await VerifyFacebookAccessToken(fbtoken);
            if (facebookUser != null)
            {
                //checks for existing users with the same email
                var userIdentity = await UserManager.FindByEmailAsync(facebookUser.Email);
                if (userIdentity != null)
                {
                    return Json(new { success = false, cause = "There is an account already associated with this email" });
                }
                else
                {
                    var user = new ApplicationUser() { UserName = facebookUser.Name, Email = facebookUser.Email };

                    IdentityResult result = await UserManager.CreateAsync(user);
                    if (!result.Succeeded)
                    {
                        return GetErrorResult(result);
                    }
                }
            }
            return Ok(new { token = fbtoken });
        }
        private async Task<FacebookUserViewModel> VerifyFacebookAccessToken(string accessToken)
        {
            FacebookUserViewModel fbUser = null;
            var path = "https://graph.facebook.com/me?fields=id,name,email&access_token=" + accessToken;
            var client = new HttpClient();
            var uri = new Uri(path);
            var response = await client.GetAsync(uri);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                fbUser = Newtonsoft.Json.JsonConvert.DeserializeObject<FacebookUserViewModel>(content);
            }

            return fbUser;
        }
        public class FacebookUserViewModel
        {
            [JsonProperty("id")]
            public string ID { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("email")]
            public string Email { get; set; }
        }

        // POST api/Account/RegisterExternal

        [AllowAnonymous]
        [Route("RegisterExternal")]
        public async Task<IHttpActionResult> RegisterExternal(RegisterExternalBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var verifiedAccessToken = await VerifyExternalAccessToken(model.Provider, model.ExternalAccessToken);
            if (verifiedAccessToken == null)
            {
                return BadRequest("Invalid Provider or External Access Token");
            }
            var facebookUser = await VerifyFacebookAccessToken(model.ExternalAccessToken);
            ApplicationUser user = await UserManager.FindAsync(new UserLoginInfo(model.Provider, verifiedAccessToken.user_id));

            bool hasRegistered = user != null;
            if (hasRegistered)
            {
                //var login = db.UserLogins.Where(x => x.ProviderKey == verifiedAccessToken.user_id).ToList();
                //if(login != null)
                //{

                //}
                 var accessTokenResponseExisting = GenerateLocalAccessTokenResponse(facebookUser.Name);

                return Ok(accessTokenResponseExisting);
            }

            user = new ApplicationUser() { UserName = facebookUser.Name, Email = facebookUser.Email };

            IdentityResult result = await UserManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            var info = new ExternalLoginInfo()
            {
                DefaultUserName = facebookUser.Name,
                Login = new UserLoginInfo(model.Provider, verifiedAccessToken.user_id)
            };

            result = await UserManager.AddLoginAsync(user.Id, info.Login);
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            //generate access token response
            var accessTokenResponse = GenerateLocalAccessTokenResponse(facebookUser.Name);

            return Ok(accessTokenResponse);
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("ObtainLocalAccessToken")]
        public async Task<IHttpActionResult> ObtainLocalAccessToken(string provider, string externalAccessToken)
        {

            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(externalAccessToken))
            {
                return BadRequest("Provider or external access token is not sent");
            }

            var verifiedAccessToken = await VerifyExternalAccessToken(provider, externalAccessToken);
            if (verifiedAccessToken == null)
            {
                return BadRequest("Invalid Provider or External Access Token");
            }

            IdentityUser user = await UserManager.FindAsync(new UserLoginInfo(provider, verifiedAccessToken.user_id));

            bool hasRegistered = user != null;

            if (!hasRegistered)
            {
                return BadRequest("External user is not registered");
            }

            //generate access token response
            var accessTokenResponse = GenerateLocalAccessTokenResponse(user.UserName);

            return Ok(accessTokenResponse);

        }



        private async Task<ParsedExternalAccessToken> VerifyExternalAccessToken(string provider, string accessToken)
        {
            ParsedExternalAccessToken parsedToken = null;

            var verifyTokenEndPoint = "";

            if (provider == "Facebook")
            {
                //You can get it from here: https://developers.facebook.com/tools/accesstoken/
                //More about debug_tokn here: http://stackoverflow.com/questions/16641083/how-does-one-get-the-app-access-token-for-debug-token-inspection-on-facebook

                var appToken = "1543886725935090|RAo60r7g3WaYsExBCSEYK6Dm9xU";
                verifyTokenEndPoint = string.Format("https://graph.facebook.com/debug_token?input_token={0}&access_token={1}", accessToken, appToken);
            }
            else if (provider == "Google")
            {
                verifyTokenEndPoint = string.Format("https://www.googleapis.com/oauth2/v1/tokeninfo?access_token={0}", accessToken);
            }
            else
            {
                return null;
            }

            var client = new HttpClient();
            var uri = new Uri(verifyTokenEndPoint);
            var response = await client.GetAsync(uri);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                dynamic jObj = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(content);

                parsedToken = new ParsedExternalAccessToken();

                if (provider == "Facebook")
                {
                    parsedToken.user_id = jObj["data"]["user_id"];
                    parsedToken.app_id = jObj["data"]["app_id"];

                    if (!string.Equals(Startup.facebookAuthOptions.AppId, parsedToken.app_id, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
                else if (provider == "Google")
                {
                    //parsedToken.user_id = jObj["user_id"];
                    //parsedToken.app_id = jObj["audience"];

                    //if (!string.Equals(Startup.googleAuthOptions.ClientId, parsedToken.app_id, StringComparison.OrdinalIgnoreCase))
                    //{
                    //    return null;
                    //}

                }

            }

            return parsedToken;
        }

        private JObject GenerateLocalAccessTokenResponse(string userName)
        {

            var tokenExpiration = TimeSpan.FromDays(1);

            ClaimsIdentity identity = new ClaimsIdentity(OAuthDefaults.AuthenticationType);

            identity.AddClaim(new Claim(ClaimTypes.Name, userName));
            identity.AddClaim(new Claim("role", "user"));

            var props = new AuthenticationProperties()
            {
                IssuedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.Add(tokenExpiration),
            };

            var ticket = new AuthenticationTicket(identity, props);

            var accessToken = Startup.OAuthBearerOptions.AccessTokenFormat.Protect(ticket);

            JObject tokenResponse = new JObject(
                                        new JProperty("userName", userName),
                                        new JProperty("access_token", accessToken),
                                        new JProperty("token_type", "bearer"),
                                        new JProperty("expires_in", tokenExpiration.TotalSeconds.ToString()),
                                        new JProperty(".issued", ticket.Properties.IssuedUtc.ToString()),
                                        new JProperty(".expires", ticket.Properties.ExpiresUtc.ToString())
        );

            return tokenResponse;
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }

        #region Helpers

        private IAuthenticationManager Authentication
        {
            get { return Request.GetOwinContext().Authentication; }
        }

        private IHttpActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return InternalServerError();
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }

        private class ExternalLoginData
        {
            public string LoginProvider { get; set; }
            public string ProviderKey { get; set; }
            public string UserName { get; set; }

            public IList<Claim> GetClaims()
            {
                IList<Claim> claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.NameIdentifier, ProviderKey, null, LoginProvider));

                if (UserName != null)
                {
                    claims.Add(new Claim(ClaimTypes.Name, UserName, null, LoginProvider));
                }

                return claims;
            }

            public static ExternalLoginData FromIdentity(ClaimsIdentity identity)
            {
                if (identity == null)
                {
                    return null;
                }

                Claim providerKeyClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

                if (providerKeyClaim == null || String.IsNullOrEmpty(providerKeyClaim.Issuer)
                    || String.IsNullOrEmpty(providerKeyClaim.Value))
                {
                    return null;
                }

                if (providerKeyClaim.Issuer == ClaimsIdentity.DefaultIssuer)
                {
                    return null;
                }

                return new ExternalLoginData
                {
                    LoginProvider = providerKeyClaim.Issuer,
                    ProviderKey = providerKeyClaim.Value,
                    UserName = identity.FindFirstValue(ClaimTypes.Name)
                };
            }
        }

        private static class RandomOAuthStateGenerator
        {
            private static RandomNumberGenerator _random = new RNGCryptoServiceProvider();

            public static string Generate(int strengthInBits)
            {
                const int bitsPerByte = 8;

                if (strengthInBits % bitsPerByte != 0)
                {
                    throw new ArgumentException("strengthInBits must be evenly divisible by 8.", "strengthInBits");
                }

                int strengthInBytes = strengthInBits / bitsPerByte;

                byte[] data = new byte[strengthInBytes];
                _random.GetBytes(data);
                return HttpServerUtility.UrlTokenEncode(data);
            }
        }

        #endregion
    }
}

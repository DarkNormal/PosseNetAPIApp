using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data.Entity.Infrastructure;
using SendGrid;
using System.Net.Mail;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;

namespace PosseNetAPIApp.Controllers
{
    //[Authorize]
    [RoutePrefix("api/Account")]
    public class AccountController : ApiController
    {
        private const string LocalLoginProvider = "Local";
        private CloudBlockBlob blockBlob;
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

        // POST: api/FriendRelationships
        [Route("Follow")]
        public async Task<IHttpActionResult> Follow(FriendRelationships friendRelationships)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            else
            {
                //always set accepted to false - confirmation pending from other user
                friendRelationships.HasAccepted = false;

                //check that the username the request is coming from exists
                var checkFromUser = db.Users.FirstOrDefault(x => x.UserName == friendRelationships.FromUsername);
                var checkToUser = db.Users.FirstOrDefault(x => x.UserName == friendRelationships.ToUsername);
                if (checkFromUser != null && checkToUser != null)
                {
                    bool existingFollowing = false;
                    foreach(ApplicationUser user in checkFromUser.Following)
                    {
                        if(user.UserName.Equals(checkToUser.UserName, StringComparison.OrdinalIgnoreCase))
                        {
                            existingFollowing = true;
                            break;
                        }
                    }
                    if (existingFollowing == true)
                    {
                        return Json(new { success = false, cause = "You are already following " + checkToUser.UserName });
                    }
                    else
                    {
                        checkFromUser.Following.Add(checkToUser);
                        checkToUser.Followers.Add(checkFromUser);
                        try
                        {
                            db.SaveChanges();
                        }
                        catch (Exception)
                        {

                        }
                        //send a push notification to the requested user's device
                        await PostNotification("gcm", String.Format("{0} is now following you", checkFromUser.UserName), checkFromUser.Email, checkToUser.Email);
                        var myMessage = new SendGridMessage();
                        myMessage.From = new MailAddress("no-reply@posseup.azurewebsites.net");
                        myMessage.AddTo(string.Format(@"{0} <{1}>", checkToUser.UserName, checkToUser.Email));
                        myMessage.Subject = string.Format("{0} is now following you", checkFromUser.UserName);
                        myMessage.Html = "<p>New Follower</p>";
                        myMessage.Text = "New Follower plain text!";
                        var apiKey = System.Environment.GetEnvironmentVariable("SENDGRID_APIKEY");
                        var transportWeb = new Web(apiKey);

                        // Send the email.
                        await transportWeb.DeliverAsync(myMessage);
                        return Json(new { success = true });
                    }
                }


            }
            return BadRequest("Incorrect To or From Username");
        }
        // POST: api/FriendRelationships
        [Route("Unfollow")]
        public IHttpActionResult Unfollow(FriendRelationships friendRelationships)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            else
            {
                friendRelationships.HasAccepted = false;
                //check that the username the request is coming from exists
                var checkFromUser = db.Users.FirstOrDefault(x => x.UserName == friendRelationships.FromUsername);
                var checkToUser = db.Users.FirstOrDefault(x => x.UserName == friendRelationships.ToUsername);
                if (checkFromUser != null && checkToUser != null)
                {
                    var friend = checkFromUser.Following.FirstOrDefault(x => x.UserName.Equals(checkToUser.UserName, StringComparison.OrdinalIgnoreCase));
                    var altFriend = checkToUser.Followers.FirstOrDefault(x => x.UserName.Equals(checkFromUser.UserName, StringComparison.OrdinalIgnoreCase));
                    if (friend != null && altFriend != null)
                    {
                        checkFromUser.Following.Remove(friend);
                        checkToUser.Followers.Remove(altFriend);
                        try
                        {
                            db.SaveChanges();
                            return Json(new { success = true, cause = "You unfollowed " + checkToUser.UserName });
                        }
                        catch (Exception)
                        {

                        }
                    }
                    else
                    {
                        return Json(new { success = false, cause = "You are not following " + checkToUser.UserName });
                    }
                }

            }
            return BadRequest("Incorrect To or From Username");
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
                        return Request.CreateResponse(HttpStatusCode.OK, new UserBasicDetailsModel(model.Username, model.Password, user.ProfileImageURL));                //return the model back to the client (with username)
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
                    return Request.CreateResponse(HttpStatusCode.OK, new UserBasicDetailsModel(checkUser.UserName, model.Password, checkUser.ProfileImageURL));                    //found user, return model as it satisfies the requirements
                }
                else
                {
                    return Request.CreateErrorResponse(HttpStatusCode.PreconditionFailed, "Account with that email/username pasword combo not found");
                }
            }

        }

        public ISecureDataFormat<AuthenticationTicket> AccessTokenFormat { get; private set; }

        // GET api/Account/UserInfo
        [Route("UserInfo/{username}")]
        public UserInfoViewModel GetUserInfo(string username)
        {
            var user = db.Users.FirstOrDefault(x => x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));
            List<UserBasicDetailsModel> following = new List<UserBasicDetailsModel>();
            foreach(ApplicationUser u in user.Following)
            {
                following.Add(new UserBasicDetailsModel(u.UserName, u.ProfileImageURL));
            }
            List<UserBasicDetailsModel> followers = new List<UserBasicDetailsModel>();
            foreach (ApplicationUser u in user.Followers)
            {
                followers.Add(new UserBasicDetailsModel(u.UserName, u.ProfileImageURL));
            }
            return new UserInfoViewModel
            {
                Username = user.UserName,
                ProfileImageURL = user.ProfileImageURL,
                Following = following,
                Followers = followers,
                Events = user.Events
            };
        }

        // POST api/Account/Logout
        [Route("Logout")]
        public IHttpActionResult Logout()
        {
            Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
            return Ok();
        }


        //TODO remove allow anonymous
        [HttpPost]
        [AllowAnonymous]
        [Route("ChangeUsername")]
        public async Task<IHttpActionResult> ChangeUsername(RegisterBindingModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUsername = db.Users.Where(x => x.UserName.Equals(model.UserName, StringComparison.OrdinalIgnoreCase)).ToList(); //finds any users with existing username
                if (existingUsername.Count < 1) //the chosen username doesn't exist
                {
                    var u = UserManager.FindByEmail(model.Email);       //finds the user details based on email
                    if (u != null)    //if the email checks out with an existing user
                    {
                        bool passwordCheck = await UserManager.CheckPasswordAsync(u, model.Password);   //check the password is correct to the user
                        if (passwordCheck)
                        {
                            u.UserName = model.UserName;
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

                return Json(new { success = true });
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
            if (model.ProfileImageURL != null)
            {
                string storageString = ConfigurationManager.ConnectionStrings["StorageConnectionString"].ToString();
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                // Retrieve reference to a previously created container.
                CloudBlobContainer container = blobClient.GetContainerReference("profile-pictures");

                //Create the "images" container if it doesn't already exist.
                container.CreateIfNotExists();

                // Retrieve reference to a blob with this name
                blockBlob = container.GetBlockBlobReference("profile_image_" + Guid.NewGuid());

                byte[] imageBytes = Convert.FromBase64String(model.ProfileImageURL);

                // create or overwrite the blob named "image_" and the current date and time 
                blockBlob.UploadFromByteArray(imageBytes, 0, imageBytes.Length);

                model.ProfileImageURL = getImageURL();
            }
            else
            {
                model.ProfileImageURL = "https://posseup.blob.core.windows.net/profile-pictures/05-512.png";
            }

            var user = new ApplicationUser() { UserName = model.UserName, Email = model.Email, ProfileImageURL = model.ProfileImageURL };

            IdentityResult result = await UserManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, result);
            }
            var myMessage = new SendGridMessage();
            myMessage.From = new MailAddress("mark@posseup.azurewebsites.net");
            myMessage.AddTo(string.Format(@"{0} <{1}>", user.UserName, user.Email));
            myMessage.Subject = "Welcome to Posse Up!";
            myMessage.Html = "<p>Hello World!</p>";
            myMessage.Text = "Hello World plain text!";
            await SendEmail(myMessage);
            // Send the email.
           
            return Request.CreateResponse(HttpStatusCode.OK, model);
        }
        private string getImageURL()
        {
            return blockBlob.StorageUri.PrimaryUri.AbsoluteUri;
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
            var existingUser = await UserManager.FindByEmailAsync(facebookUser.Email);
            bool hasRegistered = user != null;
            if (hasRegistered)
            {

                if (existingUser != null)
                {
                    facebookUser.Name = existingUser.UserName;
                }
                var accessTokenResponseExisting = GenerateLocalAccessTokenResponse(facebookUser.Name, facebookUser.Email);
                return Ok(accessTokenResponseExisting);
            }
            if(model.Name != null)
            {
                facebookUser.Name = model.Name;
            }
            user = new ApplicationUser() { UserName = facebookUser.Name, Email = facebookUser.Email };

            IdentityResult result = await UserManager.CreateAsync(user);
            if (!result.Succeeded)
            {

                return BadRequest(result.Errors.First());
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
            var accessTokenResponse = GenerateLocalAccessTokenResponse(facebookUser.Name, facebookUser.Email);

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

        private JObject GenerateLocalAccessTokenResponse(string userName, string email)
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
                                        new JProperty(".expires", ticket.Properties.ExpiresUtc.ToString()),
                                        new JProperty("email", email)
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
        private async Task SendEmail(SendGridMessage message)
        {
            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_APIKEY");
            var transportWeb = new Web(apiKey);
            await transportWeb.DeliverAsync(message);
        }
        private async Task<HttpResponseMessage> PostNotification(string pns, [FromBody]string message, string from_tag, string to_tag)
        {
            var user = from_tag;
            string[] userTag = new string[2];
            userTag[0] = "username:" + to_tag;
            userTag[1] = "from:" + user;

            Microsoft.Azure.NotificationHubs.NotificationOutcome outcome = null;
            HttpStatusCode ret = HttpStatusCode.InternalServerError;

            switch (pns.ToLower())
            {
                //case "wns":
                //    // Windows 8.1 / Windows Phone 8.1
                //    var toast = @"<toast><visual><binding template=""ToastText01""><text id=""1"">" +
                //                "From " + user + ": " + message + "</text></binding></visual></toast>";
                //    outcome = await Notifications.Instance.Hub.SendWindowsNativeNotificationAsync(toast, userTag);
                //    break;
                //case "apns":
                //    // iOS
                //    var alert = "{\"aps\":{\"alert\":\"" + "From " + user + ": " + message + "\"}}";
                //    outcome = await Notifications.Instance.Hub.SendAppleNativeNotificationAsync(alert, userTag);
                //    break;
                case "gcm":
                    // Android
                    var notif = "{ \"data\" : {\"message\":\"" + message + "\"}}";
                    outcome = await Notifications.Instance.Hub.SendGcmNativeNotificationAsync(notif, to_tag);
                    break;
            }

            if (outcome != null)
            {
                if (!((outcome.State == Microsoft.Azure.NotificationHubs.NotificationOutcomeState.Abandoned) ||
                    (outcome.State == Microsoft.Azure.NotificationHubs.NotificationOutcomeState.Unknown)))
                {
                    ret = HttpStatusCode.OK;
                }
            }

            return Request.CreateResponse(ret);
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

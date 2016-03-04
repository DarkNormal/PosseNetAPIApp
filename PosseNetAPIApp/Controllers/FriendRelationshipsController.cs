using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using PosseNetAPIApp.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Threading.Tasks;

namespace PosseNetAPIApp.Controllers
{
    [RoutePrefix("api/FriendRequests")]
    public class FriendRelationshipsController : ApiController
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        [Route("GetFriends/{username}")]
        public List<FriendRelationships> GetFriendRelationships(string username)
        {

            var list = db.FriendRelationships.Where(x => x.FromUsername == username || x.ToUsername == username).ToList(); //get connections where username is in the From Column
            return list;
        }
        [HttpPost]
        [Route("Check")]
        public IHttpActionResult CheckRelationshipStatus(FriendRelationships rel)
        {
            if (ModelState.IsValid)
            {
                var existingRelationship = db.FriendRelationships.FirstOrDefault(x => (x.FromUsername.Equals(rel.FromUsername) && x.ToUsername.Equals(rel.ToUsername)) ||
                       (x.ToUsername.Equals(rel.FromUsername) && x.FromUsername.Equals(rel.ToUsername)));
                if (existingRelationship != null)
                {
                    return Json(new { success = true, isFriend = true });
                }
            }
            return BadRequest();
        }

        // POST: api/FriendRelationships
        //Add a friend relationship between two registered users
        [ResponseType(typeof(FriendRelationships))]
        [Route("AddFriend")]
        public async Task<IHttpActionResult> AddFriend(FriendRelationships friendRelationships)
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
                if (checkFromUser != null)
                {
                    //check that the username the request is for exists
                    var checkToUser = db.Users.FirstOrDefault(x => x.UserName == friendRelationships.ToUsername);
                    if (checkToUser != null)
                    {
                        //check for any matching relationship already present (could be reversed so that is also checked)
                        var existingRelationship = db.FriendRelationships.Where(x => (x.FromUsername.Equals(friendRelationships.FromUsername) && x.ToUsername.Equals(friendRelationships.ToUsername)) ||
                        (x.ToUsername.Equals(friendRelationships.FromUsername) && x.FromUsername.Equals(friendRelationships.ToUsername))).ToList();

                        if (existingRelationship.Count > 0)
                        {
                            //existing relationship between the two users exists
                            return BadRequest("A relationship already exists between these users");
                        }
                        else {
                            //else add the relationship to the database
                            db.FriendRelationships.Add(friendRelationships);
                            db.SaveChanges();
                            //send a push notification to the requested user's device
                            await PostNotification("gcm", "friend request", checkFromUser.Email, checkToUser.Email);

                            return Json(new { success = true });
                        }
                    }
                    return BadRequest("Incorrect To or From Username");
                }
                return BadRequest("Incorrect To or From Username");
            }
        }

        // DELETE: api/FriendRelationships/5
        [ResponseType(typeof(FriendRelationships))]
        public IHttpActionResult DeleteFriendRelationships(int id)
        {
            FriendRelationships friendRelationships = db.FriendRelationships.Find(id);
            if (friendRelationships == null)
            {
                return NotFound();
            }

            db.FriendRelationships.Remove(friendRelationships);
            db.SaveChanges();

            return Ok(friendRelationships);
        }

        // DELETE: api/FriendRelationships/5
        [ResponseType(typeof(FriendRelationships))]
        public IHttpActionResult DeleteFriendRelationshipsByUsername(string username, string usernameOfFriend)
        {
            FriendRelationships friendRelationships = db.FriendRelationships.FirstOrDefault(x => x.FromUsername.Equals(username, StringComparison.OrdinalIgnoreCase) && x.ToUsername.Equals(usernameOfFriend, StringComparison.OrdinalIgnoreCase) ||
                                                                                                 x.FromUsername.Equals(usernameOfFriend, StringComparison.OrdinalIgnoreCase) && x.ToUsername.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (friendRelationships == null)
            {
                return NotFound();
            }

            db.FriendRelationships.Remove(friendRelationships);
            db.SaveChanges();

            return Ok(friendRelationships);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool FriendRelationshipsExists(int id)
        {
            return db.FriendRelationships.Count(e => e.FriendRelationshipsID == id) > 0;
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
                    var notif = "{ \"data\" : {\"message\":\"" + "From " + user + ": " + message + "\"}}";
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
    }
}
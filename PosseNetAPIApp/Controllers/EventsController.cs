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
using Microsoft.AspNet.Identity.Owin;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using SendGrid;
using System.Net.Mail;

namespace PosseNetAPIApp.Controllers
{
    [RoutePrefix("api/Event")]
    [Authorize]
    public class EventsController : ApiController
    {
        private ApplicationUserManager _userManager;
        private CloudBlockBlob blockBlob;

        private ApplicationDbContext db = new ApplicationDbContext();
        public EventsController()
        {

        }

        // GET: api/Events
        [Route("All/Public")]
        [HttpGet]
        public IEnumerable<Event> GetEvents()
        {
            return db.Events.Where(e => e.EventVisibility.Equals("Public")).ToList();
        }

        [Route("All/Public/{username}/Invite")]
        [HttpGet]
        public IEnumerable<Event> GetAllUserEvents(string username)
        {
            var user = db.Users.FirstOrDefault(x => x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user != null)
            {
                List<Event> returnEvents = GetEvents().ToList();
                var events = db.Events.Where(e => e.EventVisibility.Equals("Private")).ToList();
                foreach (Event e in events)
                {
                    if (e.EventInvitedGuests.Where(x => x.User == user).FirstOrDefault() != null || e.EventHost.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        returnEvents.Add(e);
                    }
                }
                return returnEvents;
            }
            else
            {
                return GetEvents();
            }
        }

        // GET: api/Events/5
        [ResponseType(typeof(Event))]
        public IHttpActionResult GetEvent(int id)
        {
            Event @event = db.Events.Find(id);
            if (@event == null)
            {
                return NotFound();
            }

            return Ok(@event);
        }

        // PUT: api/Events/5
        [ResponseType(typeof(void))]
        public IHttpActionResult PutEvent(int id, Event @event)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != @event.EventID)
            {
                return BadRequest();
            }

            db.Entry(@event).State = EntityState.Modified;

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        // POST: api/Events
        [ResponseType(typeof(Event))]
        public async Task<IHttpActionResult> PostEvent(Event newEvent)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (newEvent.EventImage != null)
            {
                string storageString = ConfigurationManager.ConnectionStrings["StorageConnectionString"].ToString();
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                // Retrieve reference to a previously created container.
                CloudBlobContainer container = blobClient.GetContainerReference("profile-pictures");

                //Create the "images" container if it doesn't already exist.
                container.CreateIfNotExists();

                // Retrieve reference to a blob with this name
                blockBlob = container.GetBlockBlobReference("event_image_" + Guid.NewGuid());

                byte[] imageBytes = Convert.FromBase64String(newEvent.EventImage);

                // create or overwrite the blob named "image_" and the current date and time 
                blockBlob.UploadFromByteArray(imageBytes, 0, imageBytes.Length);

                newEvent.EventImage = getImageURL();
            }
            else
            {
                newEvent.EventImage = "https://posseup.blob.core.windows.net/profile-pictures/event_image_ab089d5f-0b47-4b56-a8c5-8b2e6ce71e70";
            }
            if (newEvent.EventVenue == null)
            {
                newEvent.EventVenue = new Place();
            }
            var validUsers = new List<InvitedUser>();
            if (newEvent.EventInvitedGuests != null)
            {
                foreach (InvitedUser user in newEvent.EventInvitedGuests)
                {
                    var legitUser = db.Users.Where(x => x.UserName.Equals(user.User.UserName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (legitUser != null)
                    {
                        validUsers.Add(new InvitedUser() { User = legitUser, InvitedUserId = Guid.NewGuid()});
                        await inviteNotifications(newEvent, legitUser);
                    }
                }
                newEvent.EventInvitedGuests.Clear();
                newEvent.EventInvitedGuests = validUsers;
            }
            db.Events.Add(newEvent);
            db.SaveChanges();
            return CreatedAtRoute("DefaultApi", new { id = newEvent.EventID }, newEvent);
        }

        private string getImageURL()
        {
            return blockBlob.StorageUri.PrimaryUri.AbsoluteUri;
        }

        [HttpPost]
        [Route("Invite/{id}")]
        public async Task<IHttpActionResult> InviteFollowers(int id, ConfirmedAttendees usersToInvite)
        {
            var e = db.Events.Find(id);
            if (e != null)
            {
                foreach (string username in usersToInvite.Usernames)
                {
                    var validUser = db.Users.Where(x => x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (validUser != null)
                    {
                        if (e.EventInvitedGuests.Where(x => x.User == validUser).FirstOrDefault() == null)
                        {
                            e.EventInvitedGuests.Add(new InvitedUser() { User = validUser, InvitedUserId = Guid.NewGuid()});
                            db.SaveChanges();
                            await inviteNotifications(e, validUser);
                        }
                    }
                }
                
            }
            return Ok();
        }
        private async Task<IHttpActionResult> inviteNotifications(Event e, ApplicationUser user)
        {
            var hostuser = await UserManager.FindByEmailAsync(e.EventHost);
            var myMessage = new SendGridMessage();
            myMessage.From = new MailAddress("no-reply@posseup.azurewebsites.net");
            myMessage.Subject = string.Format("You're invited!");
            myMessage.Html = String.Format("<p>You're invited to attend {0}</p>", e.EventTitle);
            myMessage.Text = String.Format("You're invited to attend {0}", e.EventTitle);
            myMessage.AddTo(string.Format(@"{0} <{1}>", user.UserName, user.Email));
            var apiKey = System.Environment.GetEnvironmentVariable("SENDGRID_APIKEY");
            var transportWeb = new Web(apiKey);
            await PostNotification("gcm", "You've been invited to " + e.EventTitle + " by " + hostuser.UserName, user.Email);
            await transportWeb.DeliverAsync(myMessage);

            return Ok();


        }

        // DELETE: api/Events/5
        [ResponseType(typeof(Event))]
        public IHttpActionResult DeleteEvent(int id)
        {
            Event @event = db.Events.Find(id);
            if (@event == null)
            {
                return NotFound();
            }

            db.Events.Remove(@event);
            db.SaveChanges();

            return Ok(@event);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool EventExists(int id)
        {
            return db.Events.Count(e => e.EventID == id) > 0;
        }
        [Route("{id}/ConfirmGuests")]
        [HttpPost]
        public async Task<IHttpActionResult> ConfirmAttendance(int id, ConfirmedAttendees confirmed)
        {
            var e = db.Events.Find(id);
            if (e == null)
            {
                return BadRequest();
            }
            foreach (string u in confirmed.Usernames)
            {
                ApplicationUser attendee = e.ConfirmedGuests.Where(x => x.User.UserName.Equals(u, StringComparison.OrdinalIgnoreCase)).FirstOrDefault().User;
                if (attendee == null)
                {
                    ApplicationUser user = db.Users.FirstOrDefault(x => x.UserName.Equals(u, StringComparison.OrdinalIgnoreCase));
                    e.ConfirmedGuests.Add(new ConfirmedUser() { User = user, ConfirmedUserId = Guid.NewGuid()});
                    try
                    {
                        db.SaveChanges();
                        var myMessage = new SendGridMessage();
                        myMessage.From = new MailAddress("no-reply@posseup.azurewebsites.net");
                        myMessage.AddTo(string.Format(@"{0} <{1}>", user.UserName, user.Email));
                        myMessage.Subject = string.Format("Attendance confirmation for {0}", e.EventTitle);
                        myMessage.Html = String.Format("<p>You have just been confirmed as attending by the host of {0}</p>", e.EventTitle);
                        myMessage.Text = String.Format("You have just been confirmed as attending by the host of {0}", e.EventTitle);
                        var apiKey = System.Environment.GetEnvironmentVariable("SENDGRID_APIKEY");
                        var transportWeb = new Web(apiKey);

                        // Send the email.
                        await transportWeb.DeliverAsync(myMessage);
                        await PostNotification("gcm", String.Format("You have been confirmed as attending for {0}", e.EventTitle), user.Email);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!EventExists(id))
                        {
                            return NotFound();
                        }
                        else
                        {
                            throw;
                        }

                    }
                }
            }
            return Json(new { success = true });
        }
        public class ConfirmedAttendees
        {
            public string[] Usernames { get; set; }
        }

        //Add username to the list of attendees for the event
        //Checks if the username exists
        [Route("Attend/{id}")]
        [HttpPost]
        public  IHttpActionResult AttendEvent(int id, string username)
        {
            ApplicationUser user = db.Users.FirstOrDefault(x => x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user != null)
            {
                var e = db.Events.Find(id);
                if(db.Events.Find(id).EventAttendees.Where(x => x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase)).FirstOrDefault() == null)
                {
                    e.EventAttendees.Add(user);
                   user.Events.Add(e);
                    try
                    {
                        db.SaveChanges();
                        return Json(new { success = true });
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!EventExists(id))
                        {
                            return NotFound();
                        }
                        else
                        {
                            throw;
                        }

                    }
                }
            }
            return BadRequest();
        }

        [Route("Leave/{id}")]
        [HttpPost]
        public async Task<IHttpActionResult> LeaveEvent(int id, string username)
        {
            ApplicationUser user = await UserManager.FindByNameAsync(username);
            if (user != null)
            {
                var e = db.Events.Find(id);
                ApplicationUser attendee = e.EventAttendees.Where(x => x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (attendee != null)
                {
                    e.EventAttendees.Remove(attendee);
                    db.Users.First(x => x.Id == user.Id).Events.Remove(e);
                    try
                    {
                        db.SaveChanges();
                        return Json(new { success = true});
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!EventExists(id))
                        {
                            return NotFound();
                        }
                        else
                        {
                            throw;
                        }

                    }
                    
                }
                else
                {
                    return Json(new { success = false, cause = "You are not attending this event" });

                }
            }
            return BadRequest();
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

        private async Task<HttpResponseMessage> PostNotification(string pns, [FromBody]string message, string to_tag)
        {

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

    }
    

}
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

namespace PosseNetAPIApp.Controllers
{
    [RoutePrefix("api/Event")]
    public class EventsController : ApiController
    {
        private ApplicationUserManager _userManager;
        private CloudBlockBlob blockBlob;

        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: api/Events
        public IEnumerable<Event> GetEvents()
        {
            return db.Events.ToList();
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
            db.Events.Add(newEvent);
            db.SaveChanges();
            return CreatedAtRoute("DefaultApi", new { id = newEvent.EventID }, newEvent);
        }

        private string getImageURL()
        {
            return blockBlob.StorageUri.PrimaryUri.AbsoluteUri;
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
        public IHttpActionResult ConfirmAttendance(int id, string[] usernames)
        {
                var e = db.Events.Find(id);
            foreach(string u in usernames) {
                ApplicationUser attendee = e.ConfirmedGuests.Where(x => x.UserName.Equals(u, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if(attendee == null)
                {

                    e.ConfirmedGuests.Add(db.Users.FirstOrDefault(x => x.UserName.Equals(u, StringComparison.OrdinalIgnoreCase)));
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
                ApplicationUser attendee = e.EventAttendees.Where(x => x.UserName.Equals(username,StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if(attendee != null)
                {
                    return Json(new { success = false, cause = "You are already attending this event" });
                }
                else
                {
                    
                    e.EventAttendees.Add(user);
                    db.Users.First(x => x.Id == user.Id).Events.Add(e);

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
    }
    
}
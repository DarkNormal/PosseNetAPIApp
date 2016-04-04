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
        public async Task<IHttpActionResult> PostEvent(Event @event)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (@event.EventImage != null)
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

                byte[] imageBytes = Convert.FromBase64String(@event.EventImage);

                // create or overwrite the blob named "image_" and the current date and time 
                blockBlob.UploadFromByteArray(imageBytes, 0, imageBytes.Length);

                @event.EventImage = getImageURL();
            }
            else
            {
                @event.EventImage = "https://posseup.blob.core.windows.net/profile-pictures/event_image_ab089d5f-0b47-4b56-a8c5-8b2e6ce71e70";
            }
            if (@event.EventVenue == null)
            {
                @event.EventVenue = new Place();
            }
            ApplicationUser host = await UserManager.FindByEmailAsync(@event.EventHost);
            @event.EventAttendees.Add(new UserBasicDetailsModel(host.UserName));

            db.Events.Add(@event);
            db.SaveChanges();

            return CreatedAtRoute("DefaultApi", new { id = @event.EventID }, @event);
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

        //Add username to the list of attendees for the event
        //Checks if the username exists
        [Route("Attend/{id}")]
        [HttpPost]
        public async Task<IHttpActionResult> AttendEvent(int id, string username)
        {
            ApplicationUser user = await UserManager.FindByNameAsync(username);
            if(user != null)
            {
                var e = db.Events.Find(id);
                UserBasicDetailsModel attendee = e.EventAttendees.Where(x => x.Username.Equals(username,StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if(attendee != null)
                {
                    return Json(new { success = false, cause = "You are already attending this event" });
                }
                else
                {
                    
                    e.EventAttendees.Add(new UserBasicDetailsModel(username, user.ProfileImageURL));
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
                UserBasicDetailsModel attendee = e.EventAttendees.Where(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
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
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

namespace PosseNetAPIApp.Controllers
{
    [RoutePrefix("api/Event")]
    public class EventsController : ApiController
    {
        private ApplicationUserManager _userManager;

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
        public IHttpActionResult PostEvent(Event @event)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            db.Events.Add(@event);
            db.SaveChanges();

            return CreatedAtRoute("DefaultApi", new { id = @event.EventID }, @event);
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
                    
                    e.EventAttendees.Add(new UserBasicDetailsModel(username, "todo"));
                    db.Users.First(x => x.Id == user.Id).Events.Add(e);

                    try
                    {
                        db.SaveChanges();
                        return Ok();
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
                UserBasicDetailsModel attendee = e.EventAttendees.Where(x => x.Username == username).FirstOrDefault();
                if (attendee != null)
                {
                    e.EventAttendees.Remove(attendee);
                    db.Users.First(x => x.Id == user.Id).Events.Remove(e);
                    try
                    {
                        db.SaveChanges();
                        return Ok();
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
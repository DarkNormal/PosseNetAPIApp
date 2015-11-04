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
using System.Threading.Tasks;
using Microsoft.Owin.Testing;

namespace PosseNetAPIApp.Controllers
{
    public class AppUsersController : ApiController
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: api/AppUsers
        public IQueryable<AppUser> GetAppUsers()
        {
            return db.AppUsers;
        }
        

        // GET: api/AppUsers/5
        [ResponseType(typeof(AppUser))]
        public IHttpActionResult GetAppUser(int id)
        {
            AppUser appUser = db.AppUsers.Find(id);
            if (appUser == null)
            {
                return NotFound();
            }

            return Ok(appUser);
        }
        //GET: Users/Login/email/password
        [Route("Login/{email}/{password}")]
        [HttpGet]
        public async Task<IHttpActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                return BadRequest("Must have both fields entered!");
            }
            else
            {
                int checkCredentials = 0;
                bool isEmail = true;
                if (email.Contains("@")) {
                    checkCredentials = db.AppUsers.Where(s => s.Email.ToUpper() == email.ToUpper() && s.Password == password).Count();
                    isEmail = true;
                }
                else
                {
                    isEmail = false;
                    var users = db.AppUsers.Where(s => s.Username == email && s.Password == password);
                    if(users.Count() > 0)
                    {
                        checkCredentials = 1;
                    }
                    
                }
                if (checkCredentials == 0)
                {
                    return NotFound();
                }
                else
                {
                    if (isEmail)
                    {
                        var user = await db.AppUsers.Include(s => s.AttendEvents).Select(s =>
                        new AppUserDTO
                        {
                            UserID = s.UserID,
                            Name = s.Name,
                            Username = s.Username,
                            Email = s.Email,
                            Password = s.Password,
                            AttendEvents = s.AttendEvents.ToList()
                        }
                        ).SingleOrDefaultAsync(s => s.Email.ToUpper() == email.ToUpper());
                        return Ok(user);
                    }
                    else
                    {
                        var user = await db.AppUsers.Include(s => s.AttendEvents).Select(s =>
                        new AppUserDTO
                        {
                            UserID = s.UserID,
                            Name = s.Name,
                            Username = s.Username,
                            Email = s.Email,
                            Password = s.Password,
                            AttendEvents = s.AttendEvents.ToList()
                        }
                        ).SingleOrDefaultAsync(s => s.Username.ToUpper() == email.ToUpper());
                        return Ok(user);
                    }
                   


                }
            }
        }

        // PUT: api/AppUsers/5
        [ResponseType(typeof(void))]
        public IHttpActionResult PutAppUser(int id, AppUser appUser)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != appUser.UserID)
            {
                return BadRequest();
            }

            db.Entry(appUser).State = EntityState.Modified;

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AppUserExists(id))
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

        // POST: api/AppUsers ..Registering user
        [ResponseType(typeof(AppUser))]
        [Route("Register")]
        [HttpPost]
        public async Task<IHttpActionResult> RegisterUser(AppUser appUser)
        {
            if (appUser != null)
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                else
                {
                    int checkDuplicate = db.AppUsers.Where(u => u.Email.ToUpper() == appUser.Email.ToUpper()).Count();
                    if (checkDuplicate != 0)
                    {
                        return BadRequest("An account is already associated to this email");
                    }
                    else
                    {
                        db.AppUsers.Add(appUser);
                        await db.SaveChangesAsync();

                        String uri = Request.RequestUri.ToString() + "/Login/" + appUser.Email + "/" + appUser.Password;
                        return Created(uri, appUser);
                    }
                }
            }
            else
            {
                return BadRequest("User is null");
            }
        }

        // DELETE: api/AppUsers/5
        [ResponseType(typeof(AppUser))]
        public IHttpActionResult DeleteAppUser(int id)
        {
            AppUser appUser = db.AppUsers.Find(id);
            if (appUser == null)
            {
                return NotFound();
            }

            db.AppUsers.Remove(appUser);
            db.SaveChanges();

            return Ok(appUser);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool AppUserExists(int id)
        {
            return db.AppUsers.Count(e => e.UserID == id) > 0;
        }
    }
}
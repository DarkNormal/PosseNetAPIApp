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

namespace PosseNetAPIApp.Controllers
{
    public class FriendRelationshipsController : ApiController
    {
        private ApplicationDbContext db = new ApplicationDbContext();



        //// GET: api/FriendRelationships
        //public IQueryable<FriendRelationships> GetFriendRelationships()
        //{
        //    return db.FriendRelationships;
        //}

        // GET: api/FriendRelationships/5
        [ResponseType(typeof(List<FriendRelationships>))]
        public IHttpActionResult GetFriendRelationships(string username)
        {

            var fromList = db.FriendRelationships.Where(x => x.FromUsername == username).ToList(); //get connections where username is in the From Column
            var toList = db.FriendRelationships.Where(x => x.ToUsername == username).ToList();      //get connections where username is in the To Column
            List<FriendRelationships> friendRelationships = new List<FriendRelationships>();
            foreach(FriendRelationships friendship in fromList)         //foreach over both lists and add them to the main return list
            {
                friendRelationships.Add(friendship);
            }
            foreach (FriendRelationships friendship in toList)
            {
                friendRelationships.Add(friendship);
            }
            if (friendRelationships == null)
            {
                return NotFound();
            }

            return Ok(friendRelationships);
        }

        // PUT: api/FriendRelationships/5
        [ResponseType(typeof(void))]
        public IHttpActionResult PutFriendRelationships(int id, FriendRelationships friendRelationships)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != friendRelationships.FriendRelationshipsID)
            {
                return BadRequest();
            }

            db.Entry(friendRelationships).State = EntityState.Modified;

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FriendRelationshipsExists(id))
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

        // POST: api/FriendRelationships
        [ResponseType(typeof(FriendRelationships))]
        public IHttpActionResult PostFriendRelationships(FriendRelationships friendRelationships)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            else {
                var checkFromUser = db.Users.FirstOrDefault(x => x.UserName == friendRelationships.FromUsername);
                if (checkFromUser != null)
                {
                    var checkToUser = db.Users.FirstOrDefault(x => x.UserName == friendRelationships.ToUsername);
                    if (checkToUser != null)
                    {
                        //var alreadyAddedFriend1 = db.FriendRelationships.Where(x => x.FromUsername == friendRelationships.FromUsername).ToList();
                        //var alreadyAddedFriend2 = db.FriendRelationships.FirstOrDefault(x => x.ToUsername == friendRelationships.ToUsername);
                        //var alreadyAddedFriend3 = db.FriendRelationships.FirstOrDefault(x => x.FromUsername == friendRelationships.ToUsername);
                        //var alreadyAddedFriend4 = db.FriendRelationships.FirstOrDefault(x => x.ToUsername == friendRelationships.FromUsername);
                            db.FriendRelationships.Add(friendRelationships);
                            db.SaveChanges();
                            return CreatedAtRoute("DefaultApi", new { id = friendRelationships.FriendRelationshipsID }, friendRelationships);
                        }                   
                    }

                }
            return BadRequest(ModelState);
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
    }
}
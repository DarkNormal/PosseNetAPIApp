using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace PosseNetAPIApp.Models
{
    public class Event
    {
        public int EventID { get; set; }
        [Required(ErrorMessage = "Event name is required")]
        public string EventTitle { get; set; }
        [Required(ErrorMessage = "Event description is required")]
        public string EventDescription { get; set; }
        [Required(ErrorMessage = "Event host is required")]
        public string EventHost { get; set; }
        public DateTime EventStartTime { get; set; }
        public DateTime EventEndTime { get; set; }
        public bool EventAllDay { get; set; }
        //determines who can see the events
        public string EventVisibility { get; set; }

        public Place EventVenue { get; set; }

        public string EventImage { get; set; }
        public virtual ICollection<ApplicationUser> EventAttendees { get; set; }
        public virtual ICollection<ApplicationUser> ConfirmedGuests { get; set; }

    }
    public class Place
    {
        public Place() { }
        public string LocationName { get; set; }
        public string LocationAddress { get; set; }
        public double LocationLat { get; set; }
        public double LocationLng { get; set; }
        public List<int> LocationType { get; set; }
        public double LocationRating { get; set; }

    }
}

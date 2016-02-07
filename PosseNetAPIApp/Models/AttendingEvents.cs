using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PosseNetAPIApp.Models
{
    public class AttendingEvents
    {
        public AttendingEvents(string userid, Event e)
        {
            Event = e;
            UserID = userid;
        }
        public int AttendingEventsID { get; set; }

        public string UserID { get; set; }
        public virtual Event Event { get; set; }
        
    }
}
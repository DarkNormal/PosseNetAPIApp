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

        public double EventLocationLat { get; set; }
        public double EventLocationLng { get; set; }

    }
}

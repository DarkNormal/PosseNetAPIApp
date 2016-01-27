using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PosseNetAPIApp.Models
{
    public class FriendRelationships
    {

        public int FriendRelationshipsID { get; set; }

        public string FromUsername { get; set; }
        public string ToUsername { get; set; }

        public bool HasAccepted { get; set; }
    }
}
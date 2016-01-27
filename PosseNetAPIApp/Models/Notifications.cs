using Microsoft.Azure.NotificationHubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PosseNetAPIApp.Models
{
    public class Notifications
    {
        public static Notifications Instance = new Notifications();

        public NotificationHubClient Hub { get; set; }

        private Notifications()
        {
            Hub = NotificationHubClient.CreateClientFromConnectionString("Endpoint=sb://posseup-notificationhub.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=fUQkfD8ees4foTDJOXpFPaGpSzF0x5b39miXuhbPKGk=",
                                                                         "PosseUp-Notifications");
        }
    }
}
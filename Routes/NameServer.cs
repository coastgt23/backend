using Stella.Models;

namespace Stella.Routes
{
    public class NS
    {
        [ServerAPI.GET("/")]
        public NameServerResponse NameServer()
        {
            return new NameServerResponse
            {
                Accounts = "http://localhost:3003",
                API = "http://localhost:3003", // dont add /api because every call to this service to this already does
                Auth = "http://localhost:3003/auth",
                CDN = "http://localhost:3003/cdn",
                Chat = "http://localhost:3003/chat",
                Clubs = "http://localhost:3003/clubs",
                Commerce = "http://localhost:3003/commerce",
                DataCollection = "http://localhost:3003/datacollection",
                Images = "http://localhost:3003/img",
                Leaderboard = "http://localhost:3003",
                Link = "http://localhost:3003/link",
                Matchmaking = "http://localhost:3003/match",
                Moderation = "http://localhost:3003/moderation",
                Notifications = "http://localhost:3003/notify",
                PlatformNotifications = "http://localhost:3003/platformnotifications",
                RoomComments = "http://localhost:3003/roomcomments",
                Rooms = "http://localhost:3003/rooms",
                Storage = "http://localhost:3003/storage",
                WWW = "http://stellaonline.org"
            };
        }
    }
}
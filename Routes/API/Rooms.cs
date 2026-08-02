using System.Linq;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Stella.Models;

namespace Stella.Routes.API
{
    public class Rooms
    {
        [ServerAPI.GET("/api/rooms/v4/details/{RoomId}")]
        public RoomDetailsDTO ReturnGetRoomInfoById(long RoomId)
        {
            var room = MongoDB.roomsCollection.Find(u => u.Room.RoomId == RoomId).FirstOrDefault();

            return new RoomDetailsDTO
            {
                Room = room.Room,
                Scenes = room.Scenes,
                CoOwners = room.CoOwners,
                InvitedCoOwners = room.InvitedCoOwners,
                Moderators = room.Moderators,
                InvitedModerators = room.InvitedModerators,
                Hosts = room.Hosts,
                InvitedHosts = room.InvitedHosts,
                CheerCount = room.CheerCount,
                FavoriteCount = room.FavoriteCount,
                VisitCount = room.VisitCount,
                Tags = room.Tags
            };
        }

        // Seeds the RoomId == 29 "Maker Room" template that CreateDefaultRoom / CloneRoom
        // clone from. Safe to call multiple times — no-ops if the template already exists.
        //
        // Accessibility/WarningMask/RoomWarningMask values below were confirmed against a
        // live data export: RoomId 29 in that export has State:0, Accessibility:1. That
        // export doesn't include a RoomWarningMask field at all, so None=0 here is the
        // conventional-default assumption, not a confirmed value — verify against the
        // actual enum definition before relying on it.
        internal static void EnsureMakerRoomExists()
        {
            var existing = MongoDB.roomsCollection.Find(u => u.Room.RoomId == 29).FirstOrDefault();
            if (existing != null) return;

            var seedRoom = new MongoDB.RoomDetailsMongoDB
            {
                Room = new RoomDTO
                {
                    RoomId = 29,
                    Name = "Maker Room",
                    Description = "Default template room",
                    ImageName = "",
                    CreatorPlayerId = 0,
                    State = default,                        // matches State:0 seen in live data for RoomId 29
                    Accessibility = Accessibility.Public,    // = 1, matches Accessibility:1 seen in live data for RoomId 29
                    SupportsLevelVoting = false,
                    IsAGRoom = false,
                    IsDormRoom = false,
                    CloningAllowed = true,
                    SupportsVRLow = true,
                    SupportsMobile = true,
                    SupportsScreens = true,
                    SupportsWalkVR = true,
                    SupportsTeleportVR = true,
                    AllowsJuniors = true,
                    WarningMask = RoomWarningMask.None,       // assumed 0 by convention — not present in the live export, confirm against enum
                    RoomWarningMask = RoomWarningMask.None,   // same caveat as above
                    CustomRoomWarning = "",
                    DisableMicAutoMute = false
                },
                Scenes = new List<SceneDTO>
                {
                    new SceneDTO
                    {
                        RoomSceneId = 1,
                        RoomId = 29,
                        RoomSceneLocationId = "76d98498-60a1-430c-ab76-b54a29b7a163", // reused from ReturnNone in Matchmaking.cs — a known-valid location id in this codebase
                        Name = "Main",
                        IsSandbox = false,
                        DataBlobName = "",
                        MaxPlayers = 20,
                        CanMatchmakeInto = true,
                        DataModifiedAt = DateTime.UtcNow
                    }
                },
                CoOwners = new List<int>(),
                InvitedCoOwners = new List<int>(),
                Moderators = new List<int>(),
                InvitedModerators = new List<int>(),
                Hosts = new List<int>(),
                InvitedHosts = new List<int>(),
                CheerCount = 0,
                FavoriteCount = 0,
                VisitCount = 0,
                Tags = new List<TagDTO>()
            };

            MongoDB.roomsCollection.InsertOne(seedRoom);
        }

        // Seeds the public room catalogue (RecCenter, Paintball, Dodgeball, etc.) so that
        // GetHotRooms has Accessibility.Public rooms to return on a fresh database. Safe to
        // call multiple times — no-ops once more than just the maker-room template exists.
        //
        // NOTE: source data has one entry with RoomId 29 ("PublicSandbox") that collides with
        // the maker-room template RoomId also used by EnsureMakerRoomExists. That entry is
        // skipped below rather than overwriting the maker room — confirm whether RoomId 29 in
        // the seed data is actually meant to be a distinct room (in which case renumber it
        // upstream in the JSON) or is a duplicate/placeholder that should stay excluded.
        internal static async Task EnsureHotRoomsSeeded()
        {
            var count = await MongoDB.roomsCollection.CountDocumentsAsync(FilterDefinition<MongoDB.RoomDetailsMongoDB>.Empty);
            if (count > 1) return; // already seeded beyond just the maker room

            string json = await File.ReadAllTextAsync("Data/hotrooms_seed.json");
            var rawRooms = JsonSerializer.Deserialize<List<RoomDTO>>(json)!;

            var docs = rawRooms
                .Where(r => r.RoomId != 29) // avoid colliding with the maker-room template id
                .Select(r => new MongoDB.RoomDetailsMongoDB
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Room = r,
                    Scenes = new List<SceneDTO>(),
                    CoOwners = new List<int>(),
                    InvitedCoOwners = new List<int>(),
                    Moderators = new List<int>(),
                    InvitedModerators = new List<int>(),
                    Hosts = new List<int>(),
                    InvitedHosts = new List<int>(),
                    CheerCount = 0,
                    FavoriteCount = 0,
                    VisitCount = 0,
                    Tags = new List<TagDTO>()
                }).ToList();

            if (docs.Count > 0)
                await MongoDB.roomsCollection.InsertManyAsync(docs);
        }

        [ServerAPI.GET("/api/rooms/v2/baserooms")]
        public async Task<List<RoomDTO>> GetBaseRooms(int id)
        {
            var roomDoc = await MongoDB.roomsCollection
                .Find(u => u.Room.RoomId == 29)
                .FirstOrDefaultAsync();

            return new List<RoomDTO> { roomDoc.Room };
        }

        [ServerAPI.GET("/api/rooms/v2/{RoomId}")]
        public RoomDTO ReturnGetRoomById(long RoomId)
        {
            var room = MongoDB.roomsCollection.Find(u => u.Room.RoomId == RoomId).FirstOrDefault();

            return room.Room;
        }

        [ServerAPI.GET("/api/rooms/v2/myrooms")]
        [ServerAPI.UseAuthorization]
        public async Task<List<RoomDTO>> ReturnMyRooms(MongoDB.User user)
        {
            int userIdInt = (int)user.AccountId;

            var myRooms = await MongoDB.roomsCollection.Find(r => (r.Room.CreatorPlayerId == userIdInt || r.CoOwners.Contains(userIdInt)) && !r.Room.IsDormRoom).ToListAsync();

            return myRooms.Select(r => r.Room).ToList();
        }

        // Returns Accessibility.Public rooms for the hotlist. Empty results return an empty
        // array (not `new {}`) — the Unity/RecNet client's deserializer expects a list shape
        // here; an empty anonymous object was serializing to `{}` and triggering "malformed
        // response" errors client-side. Still unconfirmed whether the client wants a bare
        // array or a wrapped object (e.g. { "Items": [] }) — compare against RecNet's
        // DeserializeResponse code, or another working hotlist-shaped response, if the error
        // persists after this change.
        [ServerAPI.GET("/api/rooms/v2/hot")]
        public async Task<dynamic> GetHotRooms(string? roomScoreType, string? tags)
        {
            var filter = Builders<MongoDB.RoomDetailsMongoDB>.Filter.Empty;

            var rooms = await MongoDB.roomsCollection.Find(filter).Limit(50).ToListAsync();

            if (rooms.Count == 0)
            {
                return new List<RoomOrPlaylist>();
            }

            var filtered = rooms.Where(r => r != null && r.Room.Accessibility == Accessibility.Public).Select(r => r.Room).ToList();

            if (filtered.Count == 0)
            {
                return new List<RoomOrPlaylist>();
            }

            return filtered.Select(r => new RoomOrPlaylist
            {
                RoomOrPlaylistId = r.RoomId,
                Name = r.Name,
                State = r.State,
                SupportsLevelVoting = r.SupportsLevelVoting,
                SupportsMobile = r.SupportsMobile,
                SupportsScreens = r.SupportsScreens,
                SupportsTeleportVR = r.SupportsTeleportVR,
                SupportsVRLow = r.SupportsVRLow,
                SupportsWalkVR = r.SupportsWalkVR,
                Accessibility = r.Accessibility,
                AllowsJuniors = r.AllowsJuniors,
                CloningAllowed = r.CloningAllowed,
                CreatorPlayerId = r.CreatorPlayerId,
                CustomRoomWarning = r.CustomRoomWarning,
                Description = r.Description,
                DisableMicAutoMute = r.DisableMicAutoMute,
                ImageName = r.ImageName,
                IsAGRoom = r.IsAGRoom,
                IsDormRoom = r.IsDormRoom,
                RoomWarningMask = r.WarningMask,
                Type = RoomOrPlaylistType.Room
            });
        }

        [ServerAPI.GET("/api/rooms/v2/name/{RoomName}")]
        [ServerAPI.UseAuthorization]
        public dynamic ReturnGetRoomInfoByName(string RoomName, MongoDB.User user)
        {
            RoomName = RoomName.Replace("+", " ");

            MongoDB.RoomDetailsMongoDB room;
            if (RoomName.Equals("dormroom", StringComparison.CurrentCultureIgnoreCase))
            {
                room = MongoDB.roomsCollection.Find(u => u.Room.RoomId == user.OtherData.DormRoomId).FirstOrDefault();

                if (room == null)
                {
                    room = CreateDefaultRoom((int)user.OtherData.DormRoomId, "DormRoom", (int)user.AccountId);
                    MongoDB.roomsCollection.InsertOne(room);
                }
                else
                {
                    room.Room.Name = "DormRoom";
                }
            }
            else
            {
                room = MongoDB.roomsCollection.Find(u => u.Room.Name == RoomName).FirstOrDefault();

                if (room == null)
                {
                    var max = MongoDB.roomsCollection.Find(_ => true).SortByDescending(r => r.Room.RoomId).FirstOrDefault();
                    int newId = (int)((max?.Room.RoomId ?? 29) + 1);

                    room = CreateDefaultRoom(newId, RoomName, (int)user.AccountId);
                    MongoDB.roomsCollection.InsertOne(room);
                }
            }

            return room.Room;
        }

        internal static MongoDB.RoomDetailsMongoDB CreateDefaultRoom(int roomId, string name, int creatorPlayerId)
        {
            var original = MongoDB.roomsCollection.Find(u => u.Room.RoomId == 29).FirstOrDefault();

            if (original == null)
            {
                // Template missing (e.g. startup seed never ran, or RoomId 29 got deleted).
                // Seed it now instead of failing the whole request.
                EnsureMakerRoomExists();
                original = MongoDB.roomsCollection.Find(u => u.Room.RoomId == 29).FirstOrDefault();

                if (original == null)
                {
                    throw new Exception("couldnt find maker room template");
                }
            }

            string json = JsonSerializer.Serialize(original);
            var newRoomDoc = JsonSerializer.Deserialize<MongoDB.RoomDetailsMongoDB>(json)!;

            newRoomDoc.Id = ObjectId.GenerateNewId().ToString();
            newRoomDoc.Room.RoomId = roomId;
            newRoomDoc.Room.CreatorPlayerId = creatorPlayerId;
            newRoomDoc.Room.Name = name;

            foreach (var scene in newRoomDoc.Scenes)
            {
                scene.RoomId = roomId;
                scene.DataModifiedAt = DateTime.UtcNow;
            }

            newRoomDoc.VisitCount = 0;
            newRoomDoc.CheerCount = 0;
            newRoomDoc.FavoriteCount = 0;

            return newRoomDoc;
        }

        [ServerAPI.POST("/api/rooms/v1/clone")]
        [ServerAPI.UseAuthorization]
        public async Task<object> CloneRoom(HttpContext ctx, MongoDB.User user)
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();

            var json1 = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);

            var roomName = json1?["Name"].GetString();

            var original = await MongoDB.roomsCollection.Find(u => u.Room.RoomId == 29).FirstOrDefaultAsync();

            var bannedJson = await File.ReadAllTextAsync("Data/bannednames.json");
            var bannedObj = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(bannedJson);
            var bannedWords = bannedObj?["banned"] ?? new List<string>();

            if (!string.IsNullOrWhiteSpace(roomName) && bannedWords.Any(word => roomName.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                return new
                {
                    Result = 1
                };
            }

            if (original == null)
            {
                // Same self-heal as CreateDefaultRoom above.
                Rooms.EnsureMakerRoomExists();
                original = await MongoDB.roomsCollection.Find(u => u.Room.RoomId == 29).FirstOrDefaultAsync();

                if (original == null)
                {
                    throw new Exception("couldnt find maker room");
                }
            }

            var max = await MongoDB.roomsCollection.Find(_ => true).SortByDescending(r => r.Room.RoomId).FirstOrDefaultAsync();

            int newId = (int)((max?.Room.RoomId ?? 29) + 1);

            string json = JsonSerializer.Serialize(original);
            var newRoomDoc = JsonSerializer.Deserialize<MongoDB.RoomDetailsMongoDB>(json)!;

            newRoomDoc.Id = ObjectId.GenerateNewId().ToString();

            newRoomDoc.Room.RoomId = newId;
            newRoomDoc.Room.CreatorPlayerId = (int)user.AccountId;
            newRoomDoc.Room.Name = roomName;

            foreach (var scene in newRoomDoc.Scenes)
            {
                scene.RoomId = newId;
                scene.DataModifiedAt = DateTime.UtcNow;
            }

            newRoomDoc.VisitCount = 0;
            newRoomDoc.CheerCount = 0;
            newRoomDoc.FavoriteCount = 0;

            await MongoDB.roomsCollection.InsertOneAsync(newRoomDoc);

            return new
            {
                Result = 0,
                RoomDetails = newRoomDoc
            };
        }

        [ServerAPI.POST("/api/rooms/v1/roomRolePermissions")]
        [ServerAPI.UseAuthorization]
        public dynamic ReturnRoomRolePermissions(MongoDB.User user)
        {
            return Results.Ok();
        }
    }
}
using MongoDB.Driver;
using Stella.Models;
using Stella.Routes.API;
using System.Text;
using System.Text.Json;

namespace Stella.Routes
{
    public static class AuthTokenErrors
    {
        public const string InvalidRequest = "invalid_request";
        public const string InvalidClient = "invalid_client";
        public const string InvalidGrant = "invalid_grant";
        public const string UnauthorizedClient = "unauthorized_client";
        public const string UnsupportedGrantType = "unsupported_grant_type";
        public const string UnsupportedResponseType = "unsupported_response_type";
        public const string InvalidScope = "invalid_scope";
        public const string AuthorizationPending = "authorization_pending";
        public const string AccessDenied = "access_denied";
        public const string SlowDown = "slow_down";
        public const string ExpiredToken = "expired_token";

        public static class InvalidGrantErrorDescriptions
        {
            public const string InvalidUsernameOrPassword = "invalid_username_or_password";
            public const string InvalidTime = "invalid time";
            public const string PlatformVerificationFailed = "platform verification failed";
            public const string InvalidPlatform = "invalid platform";
            public const string InvalidDeviceClass = "invalid device class";
        }
    }

    public class Auth
    {
        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

        private static void Log(string endpoint, string message)
            => Console.WriteLine($"[AUTH] [{endpoint}] {message}");

        private static void LogResponse(string endpoint, object response)
            => Console.WriteLine($"[AUTH] [{endpoint}] RESPONSE JSON:\n{JsonSerializer.Serialize(response, _jsonOpts)}");

        private static void LogError(string endpoint, string reason)
            => Console.WriteLine($"[AUTH] [{endpoint}] ERROR: {reason}");

        [ServerAPI.POST("/api/player/photon")]
        [ServerAPI.GET("/api/player/photon")]
        public async Task<dynamic> ReturnPhoton(HttpContext ctx)
        {
            const string ep = "POST /api/player/photon";

            if (ctx.Request.Method == "GET")
            {
                var resp = new { ResultCode = 1, Message = "" };
                LogResponse("GET /api/player/photon", resp);
                return resp;
            }

            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            Log(ep, $"Body: {body}");

            var jsonDoc = JsonDocument.Parse(body);
            var root = jsonDoc.RootElement;

            string accountId = root.GetProperty("accountId").GetString()!;
            string accessToken = root.GetProperty("accessToken").GetString()!;
            Log(ep, $"accountId={accountId}");

            MongoDB.User user = MongoDB.usersCollection.Find(u => u.AccountId == long.Parse(accountId)).FirstOrDefault();

            IDictionary<string, object>? claims = JsonWebToken.VerifyAndDecode(accessToken);

            if (claims == null)
            {
                LogError(ep, "JWT claims null — token invalid or expired");
                var resp = new { ResultCode = 0, Message = "" };
                LogResponse(ep, resp);
                return resp;
            }

            if (!claims.TryGetValue("sub", out var subValue))
            {
                LogError(ep, "JWT missing 'sub' claim");
                var resp = new { ResultCode = 0, Message = "" };
                LogResponse(ep, resp);
                return resp;
            }

            if ((string)subValue != accountId)
            {
                LogError(ep, $"JWT sub '{subValue}' does not match accountId '{accountId}'");
                var resp = new { ResultCode = 0, Message = "" };
                LogResponse(ep, resp);
                return resp;
            }

            var successResp = new { UserId = accountId, Nickname = user.Username, ResultCode = 1, Message = "" };
            LogResponse(ep, successResp);
            return successResp;
        }

        private static LoginResponseDTO GenerateLoginResponse(string clientId, string accountId, bool isDeveloper)
        {
            long authTime = DateTime.UtcNow.ToUnixTime();

            Dictionary<string, object?> BuildClaims() => new()
            {
                { "iss", "https://auth.stellaonline.org" },
                { "client_id", clientId },
                { "role", isDeveloper ? "developer" : "player" },
                { "sub", accountId },
                { "auth_time", authTime },
                { "idp", "local" },
                { "jti", Guid.NewGuid().ToString("N").ToUpper() },
                { "sid", Guid.NewGuid().ToString("N").ToUpper() },
                { "iat", DateTime.UtcNow.ToUnixTime() },
                { "scope", new List<string> { "screenshare" } },
                { "amr", new List<string> { "pwd" } }
            };

            return new LoginResponseDTO
            {
                AccessToken = JsonWebToken.Generate(BuildClaims(), TimeSpan.FromHours(6)),
                RefreshToken = JsonWebToken.Generate(BuildClaims(), TimeSpan.FromHours(6))
            };
        }

        [ServerAPI.POST("/auth/connect/token")]
        public dynamic ReturnLoginAccount(HttpContext ctx)
        {
            const string ep = "POST /auth/connect/token";

            string? grantType    = ctx.Request.Form["grant_type"];
            string? clientId     = ctx.Request.Form["client_id"];
            string? accountIdStr = ctx.Request.Form["account_id"];
            string? platformIdStr = ctx.Request.Form["platform_id"];

            Log(ep, $"grant_type={grantType} client_id={clientId} account_id={accountIdStr} platform_id={platformIdStr}");

            if (string.IsNullOrWhiteSpace(accountIdStr) || !long.TryParse(accountIdStr, out long accountId))
            {
                LogError(ep, $"Missing or invalid account_id: '{accountIdStr}'");
                var errResp = new LoginResponseDTO { Error = AuthTokenErrors.InvalidRequest, ErrorDescription = "missing or invalid account_id" };
                LogResponse(ep, errResp);
                return Results.Json(errResp, statusCode: 400);
            }

            MongoDB.User user = MongoDB.usersCollection.Find(u => u.AccountId == accountId).FirstOrDefault();

            if (user == null)
            {
                LogError(ep, $"No user found for account_id={accountId}");
                var errResp = new LoginResponseDTO { Error = AuthTokenErrors.InvalidGrant, ErrorDescription = AuthTokenErrors.InvalidGrantErrorDescriptions.InvalidUsernameOrPassword };
                LogResponse(ep, errResp);
                return Results.Json(errResp, statusCode: 400);
            }

            Log(ep, $"User found: accountId={user.AccountId} username={user.Username} platformId={user.PlatformId}");

            if (!string.IsNullOrWhiteSpace(platformIdStr) &&
                ulong.TryParse(platformIdStr, out ulong platformId) &&
                user.PlatformId != platformId)
            {
                LogError(ep, $"Platform ID mismatch — request={platformId} db={user.PlatformId}");
                var errResp = new LoginResponseDTO { Error = AuthTokenErrors.InvalidGrant, ErrorDescription = AuthTokenErrors.InvalidGrantErrorDescriptions.PlatformVerificationFailed };
                LogResponse(ep, errResp);
                return Results.Json(errResp, statusCode: 400);
            }

            if (user.OtherData.DormRoomId == null)
            {
                Log(ep, $"User {accountId} has no dorm room — creating one");
                long id = MongoDB.roomsCollection.CountDocuments(_ => true) + 1;

                MongoDB.RoomDetailsMongoDB roomDetails = new()
                {
                    Room = new()
                    {
                        RoomId = id,
                        Name = $"@{user.Username}'s Dorm",
                        Description = "A private room.",
                        CreatorPlayerId = user.AccountId,
                        ImageName = "ca673ff19c054158a15ff00f0b844ba7",
                        State = 0,
                        Accessibility = Accessibility.Unlisted,
                        SupportsLevelVoting = false,
                        IsAGRoom = false,
                        IsDormRoom = true,
                        CloningAllowed = false,
                        SupportsScreens = true,
                        SupportsWalkVR = true,
                        SupportsTeleportVR = true,
                        AllowsJuniors = true,
                        WarningMask = 0,
                        RoomWarningMask = 0,
                        CustomRoomWarning = null,
                        DisableMicAutoMute = true
                    },
                    Scenes =
                    [
                        new()
                        {
                            RoomSceneId = 1,
                            RoomId = id,
                            RoomSceneLocationId = "76d98498-60a1-430c-ab76-b54a29b7a163",
                            Name = "Home",
                            IsSandbox = true,
                            DataBlobName = "",
                            MaxPlayers = 4,
                            CanMatchmakeInto = true,
                            DataModifiedAt = DateTime.UtcNow
                        }
                    ],
                    CoOwners = [],
                    InvitedCoOwners = [],
                    Moderators = [],
                    InvitedModerators = [],
                    Hosts = [],
                    InvitedHosts = [],
                    CheerCount = 0,
                    FavoriteCount = 0,
                    VisitCount = 0,
                    Tags = []
                };

                MongoDB.roomsCollection.InsertOne(roomDetails);
                MongoDB.usersCollection.UpdateOne(
                    u => u.AccountId == user.AccountId,
                    Builders<MongoDB.User>.Update.Set(u => u.OtherData.DormRoomId, id)
                );
                Log(ep, $"Dorm room created with id={id}");
            }

            if (grantType == "cached_login" ||
                grantType == "platform_login" ||
                grantType == "password")
            {
                Log(ep, $"Generating login token pair for grant_type={grantType}");
                var resp = GenerateLoginResponse(clientId ?? "recroom", accountIdStr, user.IsDeveloper);
                Console.WriteLine($"[AUTH] [{ep}] RESPONSE JSON:\n{{\n  \"access_token\": \"{resp.AccessToken?[..20]}...\",\n  \"refresh_token\": \"{resp.RefreshToken?[..20]}...\"\n}}");
                return resp;
            }

            if (grantType == "refresh_token")
            {
                string? refreshToken = ctx.Request.Form["refresh_token"];
                Log(ep, $"refresh_token grant — token present: {!string.IsNullOrWhiteSpace(refreshToken)}");

                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    LogError(ep, "refresh_token field is missing from request");
                    var errResp = new LoginResponseDTO { Error = AuthTokenErrors.InvalidGrant, ErrorDescription = "missing refresh_token" };
                    LogResponse(ep, errResp);
                    return Results.Json(errResp, statusCode: 400);
                }

                var decoded = JsonWebToken.VerifyAndDecode(refreshToken);
                if (decoded == null)
                {
                    LogError(ep, "refresh_token failed JWT verification — expired or tampered");
                    var errResp = new LoginResponseDTO { Error = AuthTokenErrors.ExpiredToken, ErrorDescription = "refresh_token is invalid or expired" };
                    LogResponse(ep, errResp);
                    return Results.Json(errResp, statusCode: 400);
                }

                Log(ep, "refresh_token valid — issuing new token pair");
                var resp = GenerateLoginResponse(clientId ?? "recroom", accountIdStr, user.IsDeveloper);
                Console.WriteLine($"[AUTH] [{ep}] RESPONSE JSON:\n{{\n  \"access_token\": \"{resp.AccessToken?[..20]}...\",\n  \"refresh_token\": \"{resp.RefreshToken?[..20]}...\"\n}}");
                return resp;
            }

            LogError(ep, $"Unsupported grant_type='{grantType}'");
            var unsupportedResp = new LoginResponseDTO { Error = AuthTokenErrors.UnsupportedGrantType, ErrorDescription = $"grant_type '{grantType}' is not supported" };
            LogResponse(ep, unsupportedResp);
            return Results.Json(unsupportedResp, statusCode: 400);
        }

        [ServerAPI.GET("/auth/cachedlogin/forplatformid/{platformType}/{platformId}")]
        public List<CachedLoginDTO> CachedLogins(PlatformType platformType, ulong platformId)
        {
            const string ep = "GET /auth/cachedlogin/forplatformid";
            Log(ep, $"platformType={platformType} platformId={platformId}");

            List<MongoDB.User> users = MongoDB.usersCollection.Find(u => u.PlatformId == platformId).ToList();
            Log(ep, $"Found {users.Count} user(s) for platformId={platformId}");

            var result = users.Select(u => new CachedLoginDTO
            {
                AccountId = u.AccountId,
                Account = new AccountDTO
                {
                    AccountId = u.AccountId,
                    CreatedAt = u.CreatedAt,
                    DisplayName = u.DisplayName,
                    Username = u.Username,
                    ProfileImage = u.ProfileImage ?? "DefaultProfilePicture",
                    IsJunior = u.IsJunior,
                    PlatformMask = 0
                },
                LastLoginTime = DateTime.MinValue,
                Platform = platformType,
                PlatformId = platformId,
                RequirePassword = false
            }).ToList();

            LogResponse(ep, result);
            return result;
        }

        [ServerAPI.GET("/auth/account/me/haspassword")]
        [ServerAPI.UseAuthorization]
        public IResult ReturnHasPassword(MongoDB.User user)
        {
            const string ep = "GET /auth/account/me/haspassword";
            bool hasPassword = user.Password != null;
            Log(ep, $"accountId={user.AccountId} hasPassword={hasPassword}");
            return Results.Json(hasPassword);
        }

        [ServerAPI.GET("/auth/role/developer/{accountId}")]
        public IResult ReturnHasDeveloper(long accountId)
        {
            const string ep = "GET /auth/role/developer";
            MongoDB.User user = MongoDB.usersCollection.Find(u => u.AccountId == accountId).FirstOrDefault();
            Log(ep, $"accountId={accountId} isDeveloper={user?.IsDeveloper}");
            return Results.Json(user.IsDeveloper);
        }

        [ServerAPI.POST("/auth/account/me/changepassword")]
        [ServerAPI.UseAuthorization]
        public dynamic ReturnChangePassword(MongoDB.User user, HttpContext ctx)
        {
            const string ep = "POST /auth/account/me/changepassword";
            Log(ep, $"accountId={user.AccountId} hasExistingPassword={user.Password != null}");

            if (user.Password == null)
            {
                MongoDB.usersCollection.UpdateOne(
                    u => u.AccountId == user.AccountId,
                    Builders<MongoDB.User>.Update.Set(u => u.Password, ((string?)ctx.Request.Form["newPassword"]).ToSHA256())
                );
                Log(ep, "Password set (was null)");
            }
            else
            {
                if (user.Password != ((string?)ctx.Request.Form["oldPassword"]).ToSHA256())
                {
                    LogError(ep, "Old password mismatch — returning 403");
                    return Results.Forbid();
                }

                MongoDB.usersCollection.UpdateOne(
                    u => u.AccountId == user.AccountId,
                    Builders<MongoDB.User>.Update.Set(u => u.Password, ((string?)ctx.Request.Form["newPassword"]).ToSHA256())
                );
                Log(ep, "Password updated successfully");
            }

            var resp = new ValueResponse { Value = null, Error = "", Success = true };
            LogResponse(ep, resp);
            return resp;
        }
    }
}
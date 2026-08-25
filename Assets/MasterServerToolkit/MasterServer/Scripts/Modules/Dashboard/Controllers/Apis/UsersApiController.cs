using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public class UsersApiController : WebController
    {
        private const int DefaultPage = 0;
        private const int DefaultPageSize = 100;
        private const int MaxPage = 100000;
        private const int MaxPageSize = 200;
        private const int DefaultBlockHistoryPageSize = 100;
        private const int MaxBlockHistoryPageSize = 100;

        protected AuthModule authModule;
        protected ProfilesModule profilesModule;
        protected RoomsModule roomsModule;

        protected IAccountsDatabaseAccessor accountsDatabaseAccessor;
        protected IProfilesDatabaseAccessor profilesDatabaseAccessor;

        public override void Initialize(HttpServerModule webServer)
        {
            base.Initialize(webServer);

            authModule = Server.GetModule<AuthModule>();
            profilesModule = Server.GetModule<ProfilesModule>();
            roomsModule = Server.GetModule<RoomsModule>();

            if (authModule != null)
                accountsDatabaseAccessor = authModule.DatabaseAccessor;

            if (profilesModule != null)
                profilesDatabaseAccessor = profilesModule.DatabaseAccessor;

            // Users API
            webServer.RegisterGetHandler("api/v1/users", GetUsersHandler, UseCredentials); 
            webServer.RegisterPutHandler("api/v1/users/profiles", GetUsersByProfilesHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/users/info", GetUsersInfoHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/users/id", GetUserByIdHandler, UseCredentials);
            webServer.RegisterPutHandler("api/v1/users/profile-property", UpdateUserProfilePropertyHandler, UseCredentials);
            webServer.RegisterPutHandler("api/v1/users/block", BlockUserByIdHandler, UseCredentials);
            webServer.RegisterDeleteHandler("api/v1/users/block", UnblockUserByIdHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/users/block/history", GetUserBlockHistoryHandler, UseCredentials);
        }

        private async Task<MstJson> CreateUserJsonAsync(IAccountInfoData account)
        {
            var userExtension = authModule.GetLoggedInUserById(account.Id);
            var activeBlock = await accountsDatabaseAccessor.GetActiveAccountBlockAsync(account.Id);
            bool isBlocked = activeBlock != null && activeBlock.IsActive();

            var userJson = account.ToJson();
            var extras = userJson["extras"];
            extras.AddField("isOnline", userExtension != null);
            extras.AddField("isInRoom", userExtension != null && userExtension.HasJoinedRoom());
            userJson.AddField("isBlocked", isBlocked);
            userJson.AddField("blockedUntil", isBlocked
                ? activeBlock.BlockedUntil.ToString("O",
                    CultureInfo.InvariantCulture)
                : string.Empty);
            userJson.AddField("blockReason", isBlocked ? activeBlock.BlockReason : string.Empty);
            return userJson;
        }

        private MstJson CreateAccountBlockHistoryJson(DatabaseEntriesInfo<IAccountBlockData> history)
        {
            MstJson json = MstJson.CreateObject();
            json.AddField("total", history?.total ?? 0);
            json.AddField("filtered", history?.filtered ?? 0);
            json.AddField("entries", MstJson.CreateArray());

            if (history?.entries == null)
                return json;

            foreach (var block in history.entries)
            {
                if (block == null)
                    continue;

                json["entries"].Add(block.ToJson());
            }

            return json;
        }

        private static bool TryParseBlockUntil(string rawValue, out DateTime blockUntil)
        {
            blockUntil = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(rawValue) ||
                !DateTime.TryParse(rawValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal, out DateTime parsed))
            {
                return false;
            }

            if (parsed.TimeOfDay == TimeSpan.Zero)
            {
                blockUntil = parsed.Date == DateTime.MaxValue.Date
                    ? DateTime.SpecifyKind(DateTime.MaxValue,
                        DateTimeKind.Utc)
                    : parsed.Date.AddDays(1).AddTicks(-1);
            }
            else
            {
                blockUntil = parsed;
            }

            return blockUntil > DateTime.UtcNow;
        }

        private bool TryCreateSearchFilter(HttpListenerRequest request, out Dictionary<string, object> filter, out IHttpResult error)
        {
            filter = new Dictionary<string, object>();

            foreach (var key in request.QueryString.AllKeys.Where(s => !string.IsNullOrEmpty(s)))
                filter[key] = request.QueryString[key];

            if (!TryApplyIntRange(filter, MstParamKeys.QS_PARAM_PAGE, DefaultPage, 0, MaxPage, out error))
                return false;

            if (!TryApplyIntRange(filter, MstParamKeys.QS_PARAM_SIZE, DefaultPageSize, 1, MaxPageSize, out error))
                return false;

            return true;
        }

        private bool TryCreateBlockHistoryFilter(HttpListenerRequest request, out int page, out int size, out IHttpResult error)
        {
            var filter = new Dictionary<string, object>();
            page = DefaultPage;
            size = DefaultBlockHistoryPageSize;

            foreach (var key in request.QueryString.AllKeys.Where(s => !string.IsNullOrEmpty(s)))
                filter[key] = request.QueryString[key];

            if (!TryApplyIntRange(filter, MstParamKeys.QS_PARAM_PAGE, DefaultPage, 0, MaxPage, out error))
                return false;

            if (!TryApplyIntRange(filter, MstParamKeys.QS_PARAM_SIZE, DefaultBlockHistoryPageSize, 1, MaxBlockHistoryPageSize, out error))
                return false;

            page = int.Parse(filter[MstParamKeys.QS_PARAM_PAGE].ToString());
            size = int.Parse(filter[MstParamKeys.QS_PARAM_SIZE].ToString());
            return true;
        }

        private bool TryReadJsonBody(HttpListenerRequest request, out MstJson data, out IHttpResult error)
        {
            data = MstJson.CreateObject();
            error = null;

            string body;

            using (StreamReader stream = new(request.InputStream))
            {
                body = stream.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                error = new BadRequestJson("Request body cannot be empty");
                return false;
            }

            try
            {
                data = new MstJson(body);
                return true;
            }
            catch (Exception)
            {
                error = new BadRequestJson("Request body must be valid JSON");
                return false;
            }
        }

        private bool TryApplyIntRange(
            Dictionary<string, object> filter,
            string key,
            int defaultValue,
            int minValue,
            int maxValue,
            out IHttpResult error)
        {
            error = null;

            if (!filter.TryGetValue(key, out var rawValue) || rawValue == null)
            {
                filter[key] = defaultValue.ToString();
                return true;
            }

            string value = rawValue.ToString();

            if (!int.TryParse(value, out int parsedValue))
            {
                error = new BadRequestJson($"Query parameter '{key}' must be an integer");
                return false;
            }

            if (parsedValue < minValue || parsedValue > maxValue)
            {
                error = new BadRequestJson($"Query parameter '{key}' must be between {minValue} and {maxValue}");
                return false;
            }

            filter[key] = parsedValue.ToString();
            return true;
        }

        private bool TryEnsureAuthModule(out IHttpResult error)
        {
            error = null;

            if (authModule != null)
                return true;

            error = CreateDependencyError(nameof(authModule));
            return false;
        }

        private bool TryEnsureUsersApiAvailable(out IHttpResult error)
        {
            if (!TryEnsureAuthModule(out error))
                return false;

            if (accountsDatabaseAccessor != null)
                return true;

            error = CreateDependencyError(nameof(accountsDatabaseAccessor));
            return false;
        }

        private bool TryEnsureProfilesApiAvailable(out IHttpResult error)
        {
            error = null;

            if (profilesModule == null)
            {
                error = CreateDependencyError(nameof(profilesModule));
                return false;
            }

            if (profilesDatabaseAccessor == null)
            {
                error = CreateDependencyError(nameof(profilesDatabaseAccessor));
                return false;
            }

            return true;
        }

        private IHttpResult CreateDependencyError(string dependencyName)
        {
            string message = $"{dependencyName} not found";
            logger.Error(message);
            return new InternalServerErrorJson(message);
        }

        #region HANDLERS

        /// <summary>
        /// Gets a number of users. Use users?page=0&size=100
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected virtual async Task<IHttpResult> GetUsersHandler(HttpListenerRequest request)
        {
            try
            {
                if (!TryEnsureUsersApiAvailable(out var dependencyError))
                    return dependencyError;

                if (!TryCreateSearchFilter(request, out var filter, out var error))
                    return error;

                var users = await accountsDatabaseAccessor.Search(filter);

                MstJson json = MstJson.CreateObject();
                json.AddField("info", MstJson.CreateObject());
                json["info"].AddField("loggedIn", authModule.LoggedInUsers.Count());
                json["info"].AddField("inRooms", authModule.LoggedInUsers.Where(u => u.HasJoinedRoom()).Count());
                json.AddField("total", users.total);
                json.AddField("filtered", users.filtered);
                json.AddField("entries", MstJson.CreateArray());

                foreach (var account in users.entries)
                {
                    json["entries"].Add(await CreateUserJsonAsync(account));
                }

                return new JsonResult(json);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets online users from both auth module and rooms module
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected virtual Task<IHttpResult> GetUsersInfoHandler(HttpListenerRequest request)
        {
            try
            {
                if (!TryEnsureAuthModule(out var dependencyError))
                    return Task.FromResult(dependencyError);

                MstJson json = MstJson.CreateObject();

                if (authModule != null)
                    json = authModule.Details();

                return Task.FromResult<IHttpResult>(new JsonResult(json));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets user by its id. Use users/id?id=145179e8-a679-4f2d-84ed-f0a555b3d06e
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected virtual async Task<IHttpResult> GetUserByIdHandler(HttpListenerRequest request)
        {
            try
            {
                if (!TryEnsureUsersApiAvailable(out var dependencyError))
                    return dependencyError;

                string id = request.QueryString[MstParamKeys.QS_PARAM_ID];

                if (string.IsNullOrEmpty(id))
                    return new BadRequestJson("User id cannot be null. Use '?id=user-id-here' to get user by its id");

                IUserPeerExtension userExtension = authModule.GetLoggedInUserById(id);
                IAccountInfoData account;

                if (userExtension != null)
                {
                    account = userExtension.Account;
                }
                else
                {
                    account = await accountsDatabaseAccessor.GetAccountByIdAsync(id);
                }

                if (account == null)
                    return new NotFoundJson($"User {id} not found");

                // Account
                var userJson = await CreateUserJsonAsync(account);

                // Profile
                if (profilesModule != null && profilesDatabaseAccessor != null)
                {
                    ObservableServerProfile profile;

                    if (userExtension != null && userExtension.Peer.TryGetExtension(out ProfilePeerExtension profileExtension))
                    {
                        profile = profileExtension.Profile;
                    }
                    else
                    {
                        profile = profilesModule.CreateProfile(account.Id);
                        await profilesDatabaseAccessor.RestoreProfileAsync(profile);
                    }

                    userJson.AddField("profile", profile.ToJson());
                    userJson.AddField("profileSchema", profile.ToInferredSchema());
                }

                return new JsonResult(userJson);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        protected virtual async Task<IHttpResult> UpdateUserProfilePropertyHandler(HttpListenerRequest request)
        {
            try
            {
                if (!TryEnsureUsersApiAvailable(out var dependencyError))
                    return dependencyError;

                if (!TryEnsureProfilesApiAvailable(out dependencyError))
                    return dependencyError;

                string id = request.QueryString[MstParamKeys.QS_PARAM_ID];
                string key = request.QueryString[MstParamKeys.QS_PARAM_KEY];

                if (string.IsNullOrEmpty(id))
                    return new BadRequestJson("User id cannot be null. Use '?id=user-id-here' to update user profile property");

                if (string.IsNullOrWhiteSpace(key))
                    return new BadRequestJson("Profile property key cannot be empty");

                if (authModule.GetLoggedInUserById(id) != null)
                    return new BadRequestJson("User profile can be edited only while user is offline");

                if (!TryReadJsonBody(request, out var data, out var error))
                    return error;

                if (!data.HasField("value"))
                    return new BadRequestJson("[value] parameter is not defined");

                var account = await accountsDatabaseAccessor.GetAccountByIdAsync(id);

                if (account == null)
                    return new NotFoundJson($"User {id} not found");

                var newValue = data.GetField("value");
                IObservableProperty property = null;
                IHttpResult updateError = null;
                ObservableServerProfile profile = await profilesModule.TryUpdateOfflineProfileAsync(
                    account.Id, restoredProfile =>
                    {
                        if (!restoredProfile.TryGet(key, out property) || property == null)
                        {
                            updateError = new NotFoundJson($"Profile property {key} not found");
                            return false;
                        }

                        var propertySchema = property.ToInferredSchema();

                        if (!newValue.TryValidateAgainstInferredSchema(
                            propertySchema, out string validationError))
                        {
                            updateError = new BadRequestJson(
                                $"Profile property '{key}' value is invalid: {validationError}");
                            return false;
                        }

                        try
                        {
                            property.FromJson(newValue);
                            return true;
                        }
                        catch (Exception propertyError)
                        {
                            logger.Error(propertyError);
                            updateError = new BadRequestJson(
                                $"Profile property '{key}' value is invalid");
                            return false;
                        }
                    });

                if (profile == null)
                    return new BadRequestJson("User profile can be edited only while user is offline");

                if (updateError != null)
                {
                    profile.Dispose();
                    return updateError;
                }

                try
                {
                    MstJson json = MstJson.CreateObject();
                    json.AddField("profile", profile.ToJson());
                    json.AddField("profileSchema", profile.ToInferredSchema());
                    json.AddField("propertyKey", key);
                    json.AddField("propertyValue", property.ToJson());
                    return new JsonResult(json);
                }
                finally
                {
                    profile.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        protected virtual async Task<IHttpResult> BlockUserByIdHandler(HttpListenerRequest request)
        {
            try
            {
                if (!TryEnsureUsersApiAvailable(out var dependencyError))
                    return dependencyError;

                string id = request.QueryString[MstParamKeys.QS_PARAM_ID];
                string blockReason = request.QueryString[MstParamKeys.QS_PARAM_BLOCK_REASON];
                string blockUntilRaw = request.QueryString[MstParamKeys.QS_PARAM_BLOCK_UNTIL];

                if (string.IsNullOrEmpty(id))
                    return new BadRequestJson("User id cannot be null. Use '?id=user-id-here' to get user by its id");

                if (string.IsNullOrWhiteSpace(blockReason))
                    return new BadRequestJson("Block reason is required");

                if (!TryParseBlockUntil(blockUntilRaw, out DateTime blockUntil))
                    return new BadRequestJson("Block date is required and must be in the future");

                var user = await accountsDatabaseAccessor.GetAccountByIdAsync(id);

                if (user != null)
                {
                    var block = accountsDatabaseAccessor.CreateAccountBlockInstance();
                    block.AccountId = user.Id;
                    block.BlockReason = blockReason;
                    block.BlockedUntil = blockUntil;
                    block.CreatedAt = DateTime.UtcNow;
                    block.RemovedAt = null;
                    block.RemoveReason = string.Empty;

                    await accountsDatabaseAccessor.InsertAccountBlockAsync(block);

                    if (authModule.TryGetLoggedInUserById(user.Id, out var userExtension))
                    {
                        if (userExtension.HasJoinedRoom() &&
                            (roomsModule == null || !roomsModule.TryNotifyAccountBlocked(userExtension)))
                        {
                            logger.Warn(
                                $"Blocked account room notification was not sent. AccountId={user.Id}, RoomId={userExtension.JoinedRoomID}");
                        }

                        userExtension.Peer.Disconnect("Account is blocked");
                        authModule.SignOut(userExtension.Account.Username);
                    }

                    return new JsonResult(MstJson.Create(true));
                }
                else
                {
                    return new NotFoundJson($"User {id} not found");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        protected virtual async Task<IHttpResult> UnblockUserByIdHandler(HttpListenerRequest request)
        {
            try
            {
                if (!TryEnsureUsersApiAvailable(out var dependencyError))
                    return dependencyError;

                string id = request.QueryString[MstParamKeys.QS_PARAM_ID];
                string removeReason = request.QueryString[MstParamKeys.QS_PARAM_REMOVE_REASON];

                if (string.IsNullOrEmpty(id))
                    return new BadRequestJson("User id cannot be null. Use '?id=user-id-here' to get user by its id");

                var user = await accountsDatabaseAccessor.GetAccountByIdAsync(id);

                if (user == null)
                    return new NotFoundJson($"User {id} not found");

                bool unblocked = await accountsDatabaseAccessor.UnblockAccountAsync(user.Id, removeReason);

                return new JsonResult(MstJson.Create(unblocked));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        protected virtual async Task<IHttpResult> GetUserBlockHistoryHandler(HttpListenerRequest request)
        {
            try
            {
                if (!TryEnsureUsersApiAvailable(out var dependencyError))
                    return dependencyError;

                string id = request.QueryString[MstParamKeys.QS_PARAM_ID];

                if (string.IsNullOrEmpty(id))
                    return new BadRequestJson("User id cannot be null. Use '?id=user-id-here' to get user block history");

                if (!TryCreateBlockHistoryFilter(request, out int page, out int size, out var error))
                    return error;

                var user = await accountsDatabaseAccessor.GetAccountByIdAsync(id);

                if (user == null)
                    return new NotFoundJson($"User {id} not found");

                var history = await accountsDatabaseAccessor.GetAccountBlockHistoryAsync(user.Id, size, page);
                return new JsonResult(CreateAccountBlockHistoryJson(history));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        protected virtual async Task<IHttpResult> GetUsersByProfilesHandler(HttpListenerRequest request)
        {
            try
            {
                if (!TryEnsureUsersApiAvailable(out var dependencyError))
                    return dependencyError;

                if (!TryCreateSearchFilter(request, out var filter, out var error))
                    return error;

                MstJson json = MstJson.CreateObject();
                json.AddField("info", MstJson.CreateObject());
                json["info"].AddField("loggedIn", authModule.LoggedInUsers.Count());
                json["info"].AddField("inRooms", authModule.LoggedInUsers.Where(u => u.HasJoinedRoom()).Count());
                json.AddField("total", 0);
                json.AddField("filtered", 0);
                json.AddField("entries", MstJson.CreateArray());

                if (profilesDatabaseAccessor == null)
                    return new JsonResult(json);

                var profileEntries = await profilesDatabaseAccessor.Search(filter);

                json.SetField("total", profileEntries.total);
                json.SetField("filtered", profileEntries.filtered);

                var accountIds = string.Join(',', profileEntries.entries.Select(e => e.AccountId));

                if (!filter.ContainsKey(MstParamKeys.QS_PARAM_VALUE))
                    filter.Add(MstParamKeys.QS_PARAM_VALUE, string.Empty);

                filter[MstParamKeys.QS_PARAM_VALUE] = accountIds;

                var accounts = await accountsDatabaseAccessor.Search(filter);

                foreach (var profile in profileEntries.entries)
                {
                    var account = accounts.entries.FirstOrDefault(e => e.Id == profile.AccountId);

                    if (account == null)
                        continue;

                    var userJson = await CreateUserJsonAsync(account);
                    userJson.AddField("profileProperties", MstJson.CreateArray());

                    foreach (var propertyEntry in profileEntries.entries.Where(e => e.AccountId == account.Id))
                    {
                        userJson["profileProperties"].Add(propertyEntry.ToJson());
                    }

                    json["entries"].Add(userJson);
                }

                return new JsonResult(json);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        #endregion
    }
}

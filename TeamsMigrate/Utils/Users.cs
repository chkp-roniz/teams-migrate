using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using TeamsMigrate.ViewModels;
using System.Linq;

namespace TeamsMigrate.Utils
{
    public class Users
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Users));

        private static Dictionary<string, string> users = new Dictionary<string, string>();

        private static HashSet<string> MissingUsers = new HashSet<string>();

        private static Dictionary<string, string> DeletedUsers { get; set; }

        public static List<ViewModels.SimpleUser> ScanUsers(string combinedPath)
        {
            var simpleUserList = new List<ViewModels.SimpleUser>();
            using (FileStream fs = new FileStream(combinedPath, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            using (JsonTextReader reader = new JsonTextReader(sr))
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        JObject obj = JObject.Load(reader);

                        // SelectToken returns null not an empty string if nothing is found
                        var userId = (string)obj.SelectToken("id");
                        var emailToken = obj.SelectToken("profile.email");

                        // if user has no email, use name instead
                        var email = emailToken != null ? (string)emailToken : (string)obj.SelectToken("name");

                        var is_bot = (bool)obj.SelectToken("is_bot");
                        var name = !is_bot ? email.Split("@")[0] : (string)obj.SelectToken("name");
                        var real_name = (string)obj.SelectToken("profile.real_name_normalized");

                        log.DebugFormat("Scanned user {0} ({1}) {2}", name, email, (string)obj.SelectToken("real_name"));

                        simpleUserList.Add(new ViewModels.SimpleUser()
                        {
                            userId = userId,
                            name = name,
                            email = email,
                            real_name = real_name,
                            is_bot = is_bot,
                        });
                    }
                }
            }

            return simpleUserList;
        }

        internal static string GetUserIdByName(string messageSender)
        {
            string principalName = messageSender + "@" + Program.CmdOptions.Domain;
            return GetUserId(principalName);
        }

        internal static string GetUserId(string id)
        {

            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var url = string.Format("{0}users/{1}", O365.MsGraphEndpoint, id);
            log.Debug("GET " + url);
            var httpResponseMessage =
    Helpers.httpClient.GetAsync(url).Result;
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.DebugFormat("User '{0}' not exist", id);
                log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result.ToString());
                return "";
            }

            dynamic user = JObject.Parse(httpResponseMessage.Content.ReadAsStringAsync().Result);
            return user.id;
        }

        internal static string GetOrCreateId(SimpleUser simpleUser, string domain)
        {
            var existUser = Users.GetUserIdByName(simpleUser.name);
            if (!"".Equals(existUser))
            {
                return existUser;
            }

            log.DebugFormat("Missing user: {0}({1})", simpleUser.real_name, simpleUser.name);

            if (!Program.CmdOptions.CreateMissingUsers)
            {
                return existUser;
            }

            var userPrincipalName = simpleUser.name + "@" + domain;
            if (DeletedUsers == null)
            {
                DeletedUsers = GetDeletedUsers();
            }

            if (DeletedUsers.ContainsKey(userPrincipalName))
            {
                return RestoreUser(DeletedUsers[userPrincipalName]);
            }

            dynamic newUser = new JObject();
            newUser.accountEnabled = true;
            newUser.displayName = simpleUser.real_name;
            newUser.mailNickname = simpleUser.name;
            newUser.userPrincipalName = userPrincipalName;
            dynamic passwordProfile = new JObject();
            passwordProfile.forceChangePasswordNextSignIn = true;
            passwordProfile.password = "xWwvJ]6NMw+bWH-d";
            newUser.passwordProfile = passwordProfile;

            var createTeamsPutData = JsonConvert.SerializeObject(newUser);

            var url = string.Format("{0}users", O365.MsGraphEndpoint);

            log.DebugFormat("POST {0}\n{1}", url, createTeamsPutData);

            if (Program.CmdOptions.ReadOnly)
            {
                log.Debug("skip operation due to readonly mode");
                return "";
            }

            var httpResponseMessage =
                Helpers.httpClient.PostAsync(url,
                    new StringContent(createTeamsPutData, Encoding.UTF8, "application/json")).Result;

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result.ToString());
                return "";
            }



            MissingUsers.Add(userPrincipalName);
            dynamic user = JObject.Parse(httpResponseMessage.Content.ReadAsStringAsync().Result);
            log.DebugFormat("Created user: {0}({1})", userPrincipalName, user.id);
            return user.id;

        }

        private static string RestoreUser(string userId)
        {

            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


            var url = O365.MsGraphEndpoint + "directory/deletedItems/" + userId + "/restore";
            log.Debug("POST " + url);

            if (Program.CmdOptions.ReadOnly)
            {
                log.Debug("skip operation due to readonly mode");
                return "";
            }

            var httpResponseMessage =
      Helpers.httpClient.PostAsync(url,
          new StringContent("", Encoding.UTF8, "application/json")).Result;

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Debug("Failed to restore user");
                log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
                return "";
            }

            dynamic user = JObject.Parse(httpResponseMessage.Content.ReadAsStringAsync().Result);
            string id = user.id;
            var existUser = Users.GetUserId(id);
            MissingUsers.Add(existUser);
            return existUser;
        }

        private static Dictionary<string, string> GetDeletedUsers()
        {
            Dictionary<string, string> deleted = new Dictionary<string, string>();

            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var url = O365.MsGraphEndpoint + "directory/deletedItems/microsoft.graph.user";
            log.Debug("GET " + url);


            var httpResponseMessage =
      Helpers.httpClient.GetAsync(url).Result;

            log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Debug("Failed to retrive deleted users");
                return deleted;
            }

            dynamic users = JObject.Parse(httpResponseMessage.Content.ReadAsStringAsync().Result);
            foreach (var user in users.value)
            {
                string userPrincipalName = user.userPrincipalName;
                string id = user.id;
                string principalName = userPrincipalName.Replace(id.Replace("-", ""), "");
                log.DebugFormat("Found deleted user: {0}({1})", principalName, id);
                if (!deleted.ContainsKey(principalName))
                    deleted.Add(principalName, id);
            }

            return deleted;
        }

        public static string GetOrCreateId(string messageSender, List<SimpleUser> slackUserList, string domain)
        {
            try
            {
                if (users.ContainsKey(messageSender))
                {
                    return users[messageSender];
                }

                SimpleUser simpleUser = slackUserList.FirstOrDefault(w => w.name == messageSender);
                if (simpleUser == null)
                {
                    simpleUser = new SimpleUser();
                    simpleUser.real_name = messageSender;
                    simpleUser.name = messageSender;
                }
                string id = GetOrCreateId(simpleUser, domain);
                //Console.WriteLine("id: " + id);
                users.Add(messageSender, id);
                return id;
            }
            catch (Exception ex)
            {
                log.Debug("Failed to get user");
                log.Debug("Failure", ex);
                return "";
            }
        }

        internal static void AddOwner(string selectedTeamId, string userId)
        {
            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            dynamic newUserObject = new JObject();
            newUserObject.roles = new JArray("owner");
            newUserObject.Add("@odata.type", "#microsoft.graph.aadUserConversationMember");
            newUserObject.Add("user@odata.bind", "https://graph.microsoft.com/v1.0/users('" + userId + "')");

            var url = O365.MsGraphBetaEndpoint + "teams/" + selectedTeamId + "/members";
            log.Debug("POST " + url);

            var addUserToTeamPostData = JsonConvert.SerializeObject(newUserObject);
            log.Debug(addUserToTeamPostData);

            if (Program.CmdOptions.ReadOnly)
            {
                log.Debug("skip operation due to readonly mode");
                return;
            }

            var httpResponseMessage =
      Helpers.httpClient.PostAsync(url,
          new StringContent(addUserToTeamPostData, Encoding.UTF8, "application/json")).Result;

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Error("Teams Membership could not be updated");
                log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
        }

        internal static void UsersCleanup()
        {
            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            foreach (var user in MissingUsers)
            {
                var url = O365.MsGraphEndpoint + "users/" + user;
                log.Debug("DELETE " + url);

                var httpResponseMessage =
          Helpers.httpClient.DeleteAsync(url).Result;

                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    log.Error("Failed to delete " + user);
                    log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
                }
            }
        }

        internal static bool AddMemberTeam(string selectedTeamId, string userId)
        {
            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            dynamic newUserObject = new JObject();
            newUserObject.roles = new JArray("member");
            newUserObject.Add("@odata.type", "#microsoft.graph.aadUserConversationMember");
            newUserObject.Add("user@odata.bind", "https://graph.microsoft.com/v1.0/users('" + userId + "')");

            var url = O365.MsGraphEndpoint + "teams/" + selectedTeamId + "/members";
            log.Debug("POST " + url);

            var addUserToTeamPostData = JsonConvert.SerializeObject(newUserObject);
            log.Debug(addUserToTeamPostData);

            if (Program.CmdOptions.ReadOnly)
            {
                log.Debug("skip operation due to readonly mode");
                return true;
            }

            var httpResponseMessage =
      Helpers.httpClient.PostAsync(url,
          new StringContent(addUserToTeamPostData, Encoding.UTF8, "application/json")).Result;

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Error("Could not add user " + userId + "  to team");
                log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
            return httpResponseMessage.IsSuccessStatusCode;
        }

        internal static void AddMemberChannel(string selectedTeamId, string channelId, string userId)
        {
            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            dynamic newUserObject = new JObject();
            newUserObject.roles = new JArray("member");
            newUserObject.Add("@odata.type", "#microsoft.graph.aadUserConversationMember");
            newUserObject.Add("user@odata.bind", "https://graph.microsoft.com/v1.0/users('" + userId + "')");

            var url = O365.MsGraphEndpoint + "teams/" + selectedTeamId + "/channels/" + channelId + "/members";
            log.Debug("POST " + url);

            var addUserToTeamPostData = JsonConvert.SerializeObject(newUserObject);
            log.Debug(addUserToTeamPostData);

            if (Program.CmdOptions.ReadOnly)
            {
                log.Debug("skip operation due to readonly mode");
                return;
            }

            var httpResponseMessage =
      Helpers.httpClient.PostAsync(url,
          new StringContent(addUserToTeamPostData, Encoding.UTF8, "application/json")).Result;

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Error("Could not add user " + userId + "  to channel " + channelId);
                log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
        }
    }
}

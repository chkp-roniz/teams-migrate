using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using TeamsMigrate.Models;
using static TeamsMigrate.Models.MsTeams;
using System.Linq;

namespace TeamsMigrate.Utils
{
    public class Channels
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Channels));
        public static List<Slack.Channels> ScanSlackChannelsJson(string combinedPath, string membershipType = "standard")
        {
            List<Slack.Channels> slackChannels = new List<Slack.Channels>();

            using (FileStream fs = new FileStream(combinedPath, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            using (JsonTextReader reader = new JsonTextReader(sr))
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        JObject obj = JObject.Load(reader);

                        // don't force use of the Slack channel id field in a channels.json only creation operation
                        // i.e. we're not importing from a Slack archive but simply bulk creating new channels
                        // this means we must check if "id" is null, otherwise we get an exception

                        var channelId = (string)obj.SelectToken("id");
                        if (channelId == null)
                        {
                            channelId = "";
                        }

                        slackChannels.Add(new Models.Slack.Channels()
                        {
                            channelId = channelId,
                            channelName = obj["name"].ToString(),
                            channelDescription = obj["purpose"]["value"].ToString(),
                            membershipType = membershipType,
                            members = obj["members"].ToObject<List<string>>()
                        });
                    }
                }
            }
            return slackChannels;
        }

        internal static void DeleteChannel(string selectedTeamId, string channelId)
        {
            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var url = String.Format("{0}teams/{1}/channels/{2}", O365.MsGraphEndpoint, selectedTeamId, channelId);
            var httpResponseMessage = Helpers.httpClient.DeleteAsync(url).Result;
            if (httpResponseMessage.IsSuccessStatusCode)
            {
                log.InfoFormat("Channel {0} deleted", selectedTeamId);
            }
        }

        public static List<MsTeams.Channel> GetExistingChannelsInMsTeams(string teamId)
        {
            MsTeams.Team msTeamsTeam = new MsTeams.Team();

            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var url = O365.MsGraphBetaEndpoint + "teams/" + teamId + "/channels";
            log.Debug("GET " + url);
            var httpResponseMessage = Helpers.httpClient.GetAsync(url).Result;
            log.Debug(httpResponseMessage);
            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var httpResultString = httpResponseMessage.Content.ReadAsStringAsync().Result;
                log.Debug(httpResultString);
                msTeamsTeam = JsonConvert.DeserializeObject<MsTeams.Team>(httpResultString);
            }

            return msTeamsTeam.value;
        }

        public static List<Combined.ChannelsMapping> CreateChannelsInMsTeams(string teamId, List<Slack.Channels> slackChannels, string basePath)
        {
            List<Combined.ChannelsMapping> combinedChannelsMapping = new List<Combined.ChannelsMapping>();

            // Get the list of existing channels in this team, so we don't try to re-create them
            List<MsTeams.Channel> msTeamsChannel = GetExistingChannelsInMsTeams(teamId);
            if (msTeamsChannel != null)
            {
                log.DebugFormat("Found {0} existing channels", msTeamsChannel.Count);
                log.Debug(JsonConvert.SerializeObject(msTeamsChannel));
            }


            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            int i = 1;
            foreach (var v in slackChannels)
            {
                if (!Directory.Exists(Path.Combine(basePath, v.channelName)))
                {
                    i++;
                    log.DebugFormat("Channel {0} if missing in path {1}. Skipping...", v.channelName, Path.Combine(basePath, v.channelName));
                    continue;
                }

                if (v.membershipType.Equals("private") && (v.members == null || v.members.Count.Equals(0)))
                {
                    i++;
                    log.DebugFormat("Channel {0} has no members. Skipping...", v.channelName);
                    continue;
                }

                if (Program.CmdOptions.ReadOnly)
                {
                    log.Debug("skip operation due to readonly mode");
                    combinedChannelsMapping.Add(new Combined.ChannelsMapping()
                    {
                        id = v.channelId,
                        displayName = v.channelName,
                        description = v.channelDescription,
                        slackChannelId = v.channelId,
                        slackChannelName = v.channelName,
                        folderId = "",
                        members = new List<string>(v.members)
                    });
                    continue;
                }

                if (msTeamsChannel != null)
                {
                    var existingMsTeams = msTeamsChannel.Find(w => String.Equals(w.displayName, v.channelName, StringComparison.CurrentCultureIgnoreCase));

                    // if a channel with the same name exists, don't attempt a create
                    // however, read that channel's metadata so you can map it to its Slack equivalent

                    if (existingMsTeams != null)
                    {
                        log.DebugFormat("This channel already exists in MS Teams: {0}({1})", existingMsTeams.displayName, existingMsTeams.id);

                        // get the existing folder id or create and get the folder id by making an api call
                        // the function below handles both the check for existing and creation of new folder if needed

                        var channelDriveItemId = CreateMsTeamsChannelFolder(teamId, existingMsTeams.displayName);

                        combinedChannelsMapping.Add(new Combined.ChannelsMapping()
                        {
                            id = existingMsTeams.id,
                            displayName = v.channelName,
                            description = existingMsTeams.description,
                            slackChannelId = v.channelId,
                            slackChannelName = v.channelName,
                            folderId = channelDriveItemId,
                            members = new List<string>(v.members)
                        });
                        continue;
                    }
                }

                log.InfoFormat("Creating channel '{0}' ('{1}') [{2}] ({3} out of {4})", v.channelName, v.channelDescription, v.membershipType, i++, slackChannels.Count);

                MsTeams.Channel createdMsTeamsChannel = CreateChannel(v, teamId);
                if (createdMsTeamsChannel != null)
                {
                    var channelDriveItemId = CreateMsTeamsChannelFolder(teamId, createdMsTeamsChannel.displayName);

                    log.DebugFormat("Created Channel {0}({1})", createdMsTeamsChannel.displayName, createdMsTeamsChannel.id);

                    combinedChannelsMapping.Add(new Combined.ChannelsMapping()
                    {
                        id = createdMsTeamsChannel.id,
                        displayName = createdMsTeamsChannel.displayName,
                        description = createdMsTeamsChannel.description,
                        slackChannelId = v.channelId,
                        slackChannelName = v.channelName,
                        folderId = channelDriveItemId,
                        members = new List<string>(v.members)
                    });
                }
                Thread.Sleep(2000); // pathetic attempt to prevent throttling
            }

            CreateCombinedChannelsMappingFile(combinedChannelsMapping, teamId, basePath);
            return combinedChannelsMapping;
        }

        internal static MsTeams.Channel CreateChannel(Slack.Channels channel, string teamId)
        {
            dynamic slackChannelAsMsChannelObject = new JObject();
            slackChannelAsMsChannelObject.displayName = channel.channelName;
            slackChannelAsMsChannelObject.description = channel.channelDescription;
            slackChannelAsMsChannelObject.Add("@odata.type", "#Microsoft.Graph.channel");
            slackChannelAsMsChannelObject.Add("@microsoft.graph.channelCreationMode", "migration");
            slackChannelAsMsChannelObject.membershipType = channel.membershipType;
            slackChannelAsMsChannelObject.createdDateTime = "2010-01-01T00:00:00.000Z";
            slackChannelAsMsChannelObject.isFavoriteByDefault = true;

            var createTeamsChannelPostData = JsonConvert.SerializeObject(slackChannelAsMsChannelObject);

            log.Debug("POST " + O365.MsGraphBetaEndpoint + "teams/" + teamId + "/channels");
            log.Debug(createTeamsChannelPostData);

            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var httpResponseMessage =
                Helpers.httpClient.PostAsync(O365.MsGraphBetaEndpoint + "teams/" + teamId + "/channels",
                    new StringContent(createTeamsChannelPostData, Encoding.UTF8, "application/json")).Result;

            log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Error("Failed to create channel '" + channel.channelName + "'");
                return null;
            }

            var createdMsTeamsChannel = JsonConvert.DeserializeObject<MsTeams.Channel>(httpResponseMessage.Content.ReadAsStringAsync().Result);
            return createdMsTeamsChannel;
        }

        internal static void AssignChannelsMembership(string selectedTeamId, List<Combined.ChannelsMapping> msTeamsChannelsWithSlackProps, List<ViewModels.SimpleUser> slackUserList)
        {

            List<MsTeams.Channel> msTeamsChannel = GetExistingChannelsInMsTeams(selectedTeamId);
            var teamUsers = new HashSet<string>();
            foreach (var channel in msTeamsChannel)
            {
                var existingMsTeams = msTeamsChannelsWithSlackProps.Find(w => String.Equals(w.displayName, channel.displayName, StringComparison.CurrentCultureIgnoreCase));
                if (existingMsTeams == null)
                {
                    continue;
                }
                int i = 1;
                using (var progress = new ProgressBar(String.Format("Update '{0}' membership", channel.displayName)))
                {
                    foreach (var member in existingMsTeams.members)
                    {

                        progress.Report((double)i++ / existingMsTeams.members.Count);
                        if (String.IsNullOrEmpty(member))
                        {
                            continue;
                        }
                        var user = slackUserList.FirstOrDefault(u => member.Equals(u.userId));
                        if (user != null)
                        {
                            var userId = Users.GetUserIdByName(user.name);
                            if (String.IsNullOrEmpty(userId))
                            {
                                log.DebugFormat("Missing user {0}", user.name + "@" + Program.CmdOptions.Domain);
                                continue;
                            }
                            if (!teamUsers.Contains(member))
                            {
                                log.DebugFormat("Add {0} to team {1}", user.name, selectedTeamId);
                                if (Users.AddMemberTeam(selectedTeamId, userId))
                                {
                                    teamUsers.Add(member);
                                }
                            }
                            if (!channel.membershipType.Equals("standard"))
                            {
                                log.DebugFormat("Add {0} to channel {1}", user.name, channel.id);
                                Users.AddMemberChannel(selectedTeamId, channel.id, userId);
                            }
                        }
                        else
                        {
                            log.DebugFormat("Missing member {0}", member);
                        }
                    }
                }
            }
        }

        static void CreateCombinedChannelsMappingFile(List<Models.Combined.ChannelsMapping> channelsMapping, string selectedTeamId, string basePath)
        {
            var jsonFileName = Path.Combine(basePath, "combinedChannelsMapping.json");
            using (FileStream fs = new FileStream(jsonFileName, FileMode.Create))
            {
                using (StreamWriter w = new StreamWriter(fs, Encoding.UTF8))
                {
                    w.WriteLine(JsonConvert.SerializeObject(channelsMapping));
                }
            }
            Utils.FileAttachments.UploadFileToTeamsChannel(selectedTeamId, jsonFileName, "/channelsurf/combinedChannelsMapping.json").Wait();
        }

        public static string CreateMsTeamsChannelFolder(string teamId, string channelName)
        {

            Tuple<string, string> fileExists = Utils.FileAttachments.CheckIfFileExistsOnTeamsChannel(teamId, "/" + channelName);
            if (fileExists.Item1 != "")
            {
                log.Debug("Channel folder exists " + fileExists);
                return fileExists.Item1;
            }

            var authHelper = new O365.AuthenticationHelper() { AccessToken = TeamsMigrate.Utils.Auth.AccessToken };
            Microsoft.Graph.GraphServiceClient gcs = new Microsoft.Graph.GraphServiceClient(authHelper);

            Microsoft.Graph.DriveItem driveItem = new Microsoft.Graph.DriveItem();
            driveItem.Name = channelName;
            var folder = new Microsoft.Graph.Folder();
            driveItem.Folder = folder;

            try
            {
                var result = gcs.Groups[teamId].Drive.Root.Children.Request().AddAsync(driveItem).Result;
                log.Debug("Folder ID is " + result.Id + " with path " + result.WebUrl);
                return result.Id;
            }
            catch (Exception ex)
            {
                log.Error("Folder creation failure. Retry");
                log.Error("Failure", ex);
                Console.WriteLine(ex.ToString());
            }

            try
            {
                var result = gcs.Groups[teamId].Drive.Root.Children.Request().AddAsync(driveItem).Result;
                log.Debug("Folder ID is " + result.Id + " with path " + result.WebUrl);
                return result.Id;
            }
            catch (Exception ex)
            {
                log.Error("Folder creation failure");
                log.Error("Failure", ex);
                return "";
            }
        }
        public static void CompleteChannelMigration(string selectedTeamId, string channelId)
        {
            try
            {
                Helpers.httpClient.DefaultRequestHeaders.Clear();
                Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
                Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                log.Debug("POST " + O365.MsGraphBetaEndpoint + "teams/" + selectedTeamId + "/channels/" + channelId + "/completeMigration");

                if (Program.CmdOptions.ReadOnly)
                {
                    log.Debug("skip operation due to readonly mode");
                }

                var completeMigrationResponseMessage =
                        Helpers.httpClient.PostAsync(O365.MsGraphBetaEndpoint + "teams/" + selectedTeamId + "/channels/" + channelId + "/completeMigration", new StringContent("", Encoding.UTF8, "application/json")).Result;

                if (!completeMigrationResponseMessage.IsSuccessStatusCode)
                {
                    log.Error("Failed to complete channel migration");
                    log.Debug(completeMigrationResponseMessage.Content.ReadAsStringAsync().Result);
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to complete channel migration");
                log.Debug("Failure", ex);

            }
        }

        internal static void CompleteTeamMigration(string selectedTeamId)
        {
            if (Program.CmdOptions.ReadOnly)
            {
                log.Debug("skip operation due to readonly mode");
                return;
            }
            var channels = GetExistingChannelsInMsTeams(selectedTeamId);
            int i = 1;
            using (var progress = new ProgressBar("Complete migration"))
            {
                foreach (Channel channel in channels)
                {
                    CompleteChannelMigration(selectedTeamId, channel.id);
                    progress.Report((double)i++ / channels.Count);
                }
            }

            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            log.Debug("POST " + O365.MsGraphBetaEndpoint + "teams/" + selectedTeamId + "/completeMigration");



            var httpResponseMessage =
                    Helpers.httpClient.PostAsync(O365.MsGraphBetaEndpoint + "teams/" + selectedTeamId + "/completeMigration", new StringContent("", Encoding.UTF8, "application/json")).Result;

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Error("Failed to complete team migration");
                log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
        }

        public static string CreateNewTeam(string newGroupAndTeamName = "")
        {
            if ("".Equals(newGroupAndTeamName))
            {
                Console.Write("Enter your new Team name: ");
                newGroupAndTeamName = Console.ReadLine();
            }
            log.InfoFormat("Creating {0} team", newGroupAndTeamName.Trim());
            var newTeamId = Groups.CreateGroupAndTeam(newGroupAndTeamName.Trim());
            return newTeamId;
        }

        internal static void AssignTeamOwnerships(string selectedTeamId)
        {
            Console.Write("Do you want to assign ownership? (y|n): ");
            var completeMigration = Console.ReadLine();
            if (completeMigration.StartsWith("y", StringComparison.CurrentCultureIgnoreCase))
            {

                if (String.IsNullOrEmpty(TeamsMigrate.Utils.Auth.UserToken))
                {
                    TeamsMigrate.Utils.Auth.UserLogin();
                }

                Helpers.httpClient.DefaultRequestHeaders.Clear();
                Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.UserToken);
                Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Users.AddOwner(selectedTeamId, O365.getUserGuid(TeamsMigrate.Utils.Auth.UserToken, "me"));
            }
        }
    }
}

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;

namespace TeamsMigrate.Utils
{
    public class Groups
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Groups));
        public static string CreateGroupAndTeam(string newMSGroupAndTeamName) 
        {
            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TeamsMigrate.Utils.Auth.AccessToken);
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            log.DebugFormat("Find Team '{0}'", newMSGroupAndTeamName);
            var getTeamUrl = String.Format("{0}groups/?$filter=resourceProvisioningOptions/Any(x:x eq 'Team')", O365.MsGraphBetaEndpoint, newMSGroupAndTeamName);
            log.DebugFormat("Get {0}", getTeamUrl);
            var httpResponseMessage = Helpers.httpClient.GetAsync(getTeamUrl).Result;
            
            if (httpResponseMessage.IsSuccessStatusCode)
            {
                try
                {
var teams = JsonConvert.DeserializeObject<Models.MsTeams.Team>(httpResponseMessage.Content.ReadAsStringAsync().Result);
                var team = teams.value.FirstOrDefault(t => t.displayName == newMSGroupAndTeamName);
                    log.InfoFormat("Found Team '{0}'", team.id);
                    return team.id;
                }
                catch
                {
                    log.InfoFormat("Team '{0}' not exist. Create new team", newMSGroupAndTeamName);
                }
            }
            else
            {
                log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
                return "";
            }

            dynamic newTeamsObject = new JObject();
            newTeamsObject.Add("@microsoft.graph.teamCreationMode", "migration");
            newTeamsObject.createdDateTime = "2010-01-01T00:00:00.000Z";
            newTeamsObject.Add("template@odata.bind", "https://graph.microsoft.com/beta/teamsTemplates('standard')");
            newTeamsObject.displayName = newMSGroupAndTeamName;
            newTeamsObject.description = "";
            newTeamsObject.visibility = "private";

            var createTeamsPutData = JsonConvert.SerializeObject(newTeamsObject);

            if (Program.CmdOptions.ReadOnly)
            {
                log.Debug("skip operation due to readonly mode");
                return Guid.NewGuid().ToString();
            }
            var url = O365.MsGraphBetaEndpoint + "teams";
            log.Debug("POST " + url);
            log.Debug(createTeamsPutData);

            httpResponseMessage =
    Helpers.httpClient.PostAsync(url,
        new StringContent(createTeamsPutData, Encoding.UTF8, "application/json")).Result;
            log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                log.Error("Group could not be converted into Team: " + newTeamsObject);
                log.Info("RETRY");

                httpResponseMessage =
    Helpers.httpClient.PostAsync(O365.MsGraphBetaEndpoint + "teams",
        new StringContent(createTeamsPutData, Encoding.UTF8, "application/json")).Result;
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    throw new Exception(httpResponseMessage.Content.ReadAsStringAsync().Result);
                }
                    
            }

            var contentType = httpResponseMessage.Content.Headers.GetValues("Content-Location").First();
            string newGroupId = contentType.Split('\'')[1];

            return newGroupId;
        }
    }
}
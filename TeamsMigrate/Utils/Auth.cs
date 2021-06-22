using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Globalization;

namespace TeamsMigrate.Utils
{
    class Auth
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Auth));

        public static string AccessToken { get; internal set; }

        public static string UserToken { get; internal set; }

        internal static string Login(bool retry = true)
        {
            
            Helpers.httpClient.DefaultRequestHeaders.Clear();
            Helpers.httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            string token = String.Empty;
            var payload = String.Format("grant_type=client_credentials&client_id={0}&resource={1}&client_secret={2}", Program.CmdOptions.ClientId, Program.aadResourceAppId, Program.CmdOptions.ClientSecret);
            var url = String.Format("https://login.microsoftonline.com/{0}/oauth2/token", Program.CmdOptions.TenantId);
            var httpResponseMessage = Helpers.httpClient.PostAsync(url, new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded")).Result;
            log.Debug("POST " + url);
            log.Debug(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var httpResultString = httpResponseMessage.Content.ReadAsStringAsync().Result;
                token = Newtonsoft.Json.JsonConvert.DeserializeObject<TeamsMigrate.Models.Auth>(httpResultString).access_token;
                log.Debug("Authenticated: "+ AccessToken);

            }
            else
            {
                log.Error("Authentication Failure");
                if (retry)
                {
                    token = Login(false);
                }
                
            }

            return token;
        }

        internal static void UserLogin()
        {
            var authenticationContext = new AuthenticationContext
                    (String.Format(CultureInfo.InvariantCulture, Program.CmdOptions.AadInstance, Program.CmdOptions.TenantId));
            authenticationContext.TokenCache.Clear();

            DeviceCodeResult deviceCodeResult = authenticationContext.AcquireDeviceCodeAsync(Program.aadResourceAppId, (Program.CmdOptions.ClientId)).Result;
            log.Info(deviceCodeResult.Message);
            UserToken = authenticationContext.AcquireTokenByDeviceCodeAsync(deviceCodeResult).Result.AccessToken;
        }
    }
}

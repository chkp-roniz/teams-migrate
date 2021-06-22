using System.Collections.Generic;

namespace TeamsMigrate.Models
{
    public class MsTeams
    {
        public class Team
        {
            public List<Channel> value { get; set; }
        }

        public class Channel
        {
            public string id { get; set; }
            public string displayName { get; set; }
            public string description { get; set; } = "";
            public string folderId { get; set; } = "";
            public string membershipType { get; set; } = "standard";
        }
    }
}

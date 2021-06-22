
using System;
using System.Collections.Generic;

namespace TeamsMigrate.ViewModels
{
    public class ExternalMessage
    {
        public ExternalMessage(SimpleMessage message)
        {
            createdDateTime = message.ts;
            body = new ExternalBody() { content = message.text, contentType = "html" };
            from = new ExtrenalFrom() { user = new ExternalUser() { id = message.userId, displayName = message.user, userIdentityType = "aadUser" } };
            attachments = new List<Attachment>();

            foreach (var attachment in message.fileAttachments)
            {
                string id = Guid.NewGuid().ToString();// Guid.Parse(attachment.spoId).ToString();
                attachments.Add(new Attachment() 
                {
                    id = id,
                    contentUrl = attachment.spoUrl, 
                    name = attachment.originalName, 
                    contentType = "reference",
                    //"@odata.type": "#microsoft.graph.fileAttachment",

                });
                body.content += String.Format("<attachment id=\"{0}\"></attachment>", id);
            }
        }


        public string createdDateTime { get; set; }
        public ExtrenalFrom from { get; set; }
        public ExternalBody body { get; set; }
        public List<Attachment> attachments { get; set; }
    }

    public class Attachment
    {
        public string id { get; set; }
        public string contentType { get; set; }
        public string contentUrl { get; set; }
        public string name { get; set; }
    }

    public class ExternalBody
    {

        public string content { get; set; }
        public string contentType { get; set; }
    }

    public class ExtrenalFrom
    {
        public ExternalUser user { get; set; }
    }

    public class ExternalUser
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public string userIdentityType { get; set; }
    }
}

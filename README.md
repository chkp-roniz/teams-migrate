# Migrate Slack Workspace to Microsoft Teams 
*based on the great [Channel Surf](https://github.com/tamhinsf/ChannelSurf) util*

Moving to Microsoft Teams from Slack or starting fresh?  You've come to the right place.  Here's what this tool can do for you:

* Migrate Slack workspace channels structure in Teams
* Migrate team and channel members
* Migrate all messages including:
  * Thread hierarchy
  * File attachments
* Migrate users

## Slack Archive

You can create a Slack Team export on a self-service basis as a Slack Team Owner or Admin at this page [https://my.slack.com/services/export](https://my.slack.com/services/export).  Download the export file and tell Channel Surf its location.   We'll scan it and re-create the Slack channel structure in Teams - and give you the option to do more.  


## Setup a development environment 

* Clone this GitHub repository.
* Install Visual Studio 2019.  Don't have it?  Download the free [Visual Studio Community Edition](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx)
* Don't want to use Visual Studio?  Project was written using .NET Core and runs on Windows, macOS, and Linux.  Instead of using Visual Studio, you can simply download the SDK necessary to build and run this application.
  * https://www.microsoft.com/net/download/core

## Identify a test user account

* Sign in to your Office 365 environment as an administrator at [https://portal.office.com/admin/default.aspx](https://portal.office.com/admin/default.aspx)
* Ensure you have enabled Microsoft Teams for your organization [https://portal.office.com/adminportal/home#/Settings/ServicesAndAddIns](https://portal.office.com/adminportal/home#/Settings/ServicesAndAddIns)  
* Identify a user whose account you'd like to use 
  * Alternatively, you can choose to use your Office 365 administrator account 

## Azure App registrations

You must register this application in the Azure Active Directory tenant associated with your Office 365 organization.  

* Sign in to your Azure Management Portal at https://portal.azure.com
    * Or, from the Office 365 Admin center select "Azure AD"
* Within the Azure Portal, select Azure Active Directory -> App registrations -> New application registration  
    * Name: TeamsMigrationCli (anything will work - we suggest you keep this value)
    * Application type: Native
    * Redirect URI: https://migrator-cli (anything else will work if you want to change it)
     * NOTE: In earlier versions of this code, we hard-coded this value in Program.cs.  It's now been moved to appsettings.json and we've agained defaulted it to https://migrator-cli
    * Click Create
* Once Azure has created your app, copy your Application Id and give your application access to the required Microsoft Graph API permissions.  
   * Click your app's name (i.e. TeamsMigrationCli) from the list of applications
   * Copy the Application Id
   * All settings -> Required permissions
     * Click Add  
     * Select an API -> Microsoft Graph -> Select (button)
     * Select permissions:

      |API / Permissions name|Type|Description|
      |---|---|---|
      |Channel.Create|Application|Create channels|
      |Channel.ReadBasic.All|Application|Read the names and descriptions of all channels|
      |ChannelMember.ReadWrite.All|Application|Add and remove members from all channels|
      |ChannelMessage.Read.All|Application|Read all channel messages|
      |ChatMessage.Read.All|Application|Read all chat messages|
      |Group.Create|Application|Create groups|
      |Group.ReadWrite.All|Application|Read and write all groups|
      |GroupMember.ReadWrite.All|Delegated|Read and write group memberships|
      |GroupMember.ReadWrite.All|Application|Read and write all group memberships|
      |profile|Delegated|View users' basic profile|
      |Team.Create|Application|Create teams|
      |Team.ReadBasic.All|Delegated|Read the names and descriptions of teams|
      |TeamMember.ReadWrite.All|Delegated|Add and remove members from teams|
      |TeamMember.ReadWrite.All|Application|Add and remove members from all teams|
      |Teamwork.Migrate.All|Application|Create chat and channel messages with anyone's identity and with any timestamp|
      |User.Read|Delegated|Sign in and read user profile|
      |User.ReadWrite.All|Application|Read and write all users' full profiles|
     
	
  * If you plan to run Channel Surf as a non-administrator: applications built using the Graph API permissions above require administrative consent before non-administrative users can sign in - which fortunately, you'll only need to do once.  
    * You can immediately provide consent to all users in your organization using the Azure Portal. Click the "Grant permissions" button, which you can reach via your app's "Required permissions" link.
      * Here's the full path to "Grant permissions": Azure Active Directory -> App registrations -> Your app (i.e. ChannelSurfCli) -> All settings ->  Required permissions -> Grant permissions
    * Or, whenever you successfully launch ChannelSurfCli, we'll show you a URL that an administrative user can visit to provide consent.
      * Note: if you've configured the re-direct URL to be the same value as we've shown you on this page (i.e. https://channelsurf-cli), you'll be sent to an invalid page after successfully signing in.  Don't worry!
* Take note of your tenant name, which is typically in the form of your-domain.onmicrosoft.com.  You'll need to supply this when building or running ChannelSurfCli.

## Usage
 
* `n`/`name` _[string]_ (required) - Team name
* `m`/`messages` _[boolean]_ - Migrate channel messages
* `f`/`files` _[boolean]_ - Migrate channel files
* `e`/`export` _[string]_ (required) - Export file path
* `c`/`client` _[string]_ (required) - Application (client) ID
* `t`/`tenant` _[string]_ (required) - Directory (tenant) ID
* `r`/`redirect` _[string]_ (required) - Redirect URI
* `a`/`authority` _[string]_ - Authentication authority URL. Default 'https://login.microsoftonline.com/{0}'
* `d`/`domain` _[string]_ (required) - Users domain
* `s`/`secret` _[string]_ (required) - Client Secret token
* `r`/`readonly` _[boolean]_ - Readonly mode
* `v`/`verbose` _[boolean]_ - Verbose

For example:
```
TeamsMigrate.exe -v -e myworkspace.zip -d myworkspace.onmicrosoft.com -c <client id> -t <tenant> -r 'https://migrator-cli'
```

## Questions and comments

We'd love to get your feedback about this sample. You can send your questions and suggestions to us in the Issues section of this repository.

Questions about Microsoft Graph development in general should be posted to [Stack Overflow](http://stackoverflow.com/questions/tagged/microsoftgraph). Make sure that your questions or comments are tagged with [microsoftgraph].

## Additional resources

* [Import third-party platform messages to Teams using Microsoft Graph](https://docs.microsoft.com/en-us/microsoftteams/platform/graph-api/import-messages/import-external-messages-to-teams)
* [Use the Microsoft Graph API to work with Microsoft Teams](https://developer.microsoft.com/en-us/graph/docs/api-reference/beta/resources/teams_api_overview)
* [Microsoft Graph Beta Endpoint Reference](https://developer.microsoft.com/en-us/graph/docs/api-reference/beta/beta-overview)
* [Microsoft Graph API Explorer](https://developer.microsoft.com/en-us/graph/graph-explorer)
* [Overview - Microsoft Graph](https://developer.microsoft.com/en-us/graph/docs)
* [Microsoft Teams - Dev Center](https://dev.office.com/microsoft-teams)
* [Channel Surf](https://github.com/tamhinsf/ChannelSurf)


### Disclaimer ###
**THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.**

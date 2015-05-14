using System;
using System.Security.Permissions;
using MaxMelcher.AzureSearch.DataHub;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Hubs;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.Workflow;

namespace MaxMelcher.AzureSearch.TweetReceiver
{
    /// <summary>
    /// List Item Events
    /// </summary>
    public class TweetReceiver : SPItemEventReceiver
    {
        /// <summary>
        /// An item is being added.
        /// </summary>
        public override async void ItemAdding(SPItemEventProperties properties)
        {
            base.ItemAdding(properties);

            var listitem = properties.AfterProperties;
            Tweet tweet = new Tweet();
            tweet.Mention = (string) listitem["Mention"];
            tweet.Score = double.Parse((string) listitem["Score"]);
            tweet.Sentiment = (string) listitem["Sentiment"];
            tweet.Text = (string)listitem["Title"];
            tweet.Url = (string) listitem["Url"];

            var connection = new HubConnection("http://localhost:4242"); 
            var hubProxy = connection.CreateHubProxy("TweetHub");
            await connection.Start();
            await hubProxy.Invoke("NewSPTweet", tweet);
        }
    }
}
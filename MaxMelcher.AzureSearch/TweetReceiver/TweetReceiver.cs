using System;
using System.Globalization;
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

            var afterProperties = properties.AfterProperties;
            Tweet tweet = new Tweet();
            tweet.Mention = (string) afterProperties["Mention"];
            
            double score;
            double.TryParse((string) afterProperties["Score"], out score);
            tweet.Score = score;

            tweet.Sentiment = (string) afterProperties["Sentiment"];
            tweet.Text = (string)afterProperties["Title"];
            tweet.Url = (string) afterProperties["Url"];
            tweet.StatusId = (string) afterProperties["StatusId"];

            try
            {
                var connection = new HubConnection("http://localhost:4242");
                var hubProxy = connection.CreateHubProxy("TweetHub");
                await connection.Start();
                await hubProxy.Invoke("NewSPTweet", tweet);
            }
            catch (Exception)
            {
                //nothing to do here
            }
        }
    }
}
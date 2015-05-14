using Microsoft.AspNet.SignalR;

namespace MaxMelcher.AzureSearch.DataHub
{
    public class TweetHub : Hub
    {
        public void NewSPTweet(Tweet tweet)
        {
            Clients.All.NewTweet(tweet);
        }
    }
}
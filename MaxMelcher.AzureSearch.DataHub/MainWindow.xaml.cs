using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LinqToTwitter;
using MahApps.Metro.Controls;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Owin.Hosting;
using Microsoft.SharePoint.Client;
using Field = Microsoft.SharePoint.Client.Field;
using ASField = Microsoft.Azure.Search.Models.Field;
using List = Microsoft.SharePoint.Client.List;

namespace MaxMelcher.AzureSearch.DataHub
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        private IDisposable _webapp;
        private string _twitterHashtag = "#gntm";
        private readonly Regex _regexSentiment = new Regex(".*Score\":(\\d\\.(\\d)*)*}$", RegexOptions.Multiline);


        private bool _stopTwitter = true;
        private int _signalRCount;
        private int _sharePointCount;
        private int _twitterCount;
        private bool _stopSharePoint = true;
        private bool _sentimentEnabled;
        private SearchServiceClient _searchServiceClient;
        private ObservableCollection<Tweet> _searchResults = new ObservableCollection<Tweet>();
        private double _searchTime;
        private FixedSizeObservableCollection<Tweet> _tweets = new FixedSizeObservableCollection<Tweet>(3);
        private bool _azureSearchEnabled;
        private int _azureSearchCount;

        public double SearchTime
        {
            get { return _searchTime; }
            set { _searchTime = value; NotifyPropertyChanged("SearchTime"); }
        }

        public ObservableCollection<Tweet> SearchResults
        {
            get { return _searchResults; }
            set { _searchResults = value; }
        }

        public FixedSizeObservableCollection<Tweet> Tweets
        {
            get { return _tweets; }
        }

        public string SearchText { get; set; }

        public string TwitterHashtag
        {
            get { return _twitterHashtag; }
            set { _twitterHashtag = value; NotifyPropertyChanged("TwitterHashtag"); }
        }

        public SearchServiceClient SearchServiceClient
        {
            get
            {
                if (_searchServiceClient == null)
                {
                    string searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
                    string apiKey = ConfigurationManager.AppSettings["SearchServiceApiKey"];

                    _searchServiceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));
                }
                return _searchServiceClient;
            }
            set { _searchServiceClient = value; }
        }

        public int AzureSearchCount
        {
            get { return _azureSearchCount; }
            set { _azureSearchCount = value; NotifyPropertyChanged("AzureSearchCount"); }
        }

        public int TwitterCount
        {
            get { return _twitterCount; }
            set { _twitterCount = value; NotifyPropertyChanged("TwitterCount"); }
        }

        public int SharePointCount
        {
            get { return _sharePointCount; }
            set { _sharePointCount = value; NotifyPropertyChanged("SharePointCount"); }
        }

        public int SignalRCount
        {
            get { return _signalRCount; }
            set { _signalRCount = value; NotifyPropertyChanged("SignalRCount"); }
        }

        public bool SentimentEnabled
        {
            get { return _sentimentEnabled; }
            set { _sentimentEnabled = value; NotifyPropertyChanged("SentimentEnabled"); }
        }

        public bool AzureSearchEnabled
        {
            get { return _azureSearchEnabled; }
            set { _azureSearchEnabled = value; NotifyPropertyChanged("AzureSearchEnabled"); }
        }

        public MainWindow()
        {
            InitializeComponent();
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");


            Grid.DataContext = this;
        }

        private async void Button_StartHub(object sender, RoutedEventArgs e)
        {
            _webapp = WebApp.Start<Startup>("http://*:4242/");
            btnStartHub.IsEnabled = false;
            btnStopHub.IsEnabled = true;

            var connection = new HubConnection("http://localhost:4242");
            var hubProxy = connection.CreateHubProxy("TweetHub");

            hubProxy.On<Tweet>("NewTweet", (tweet) =>
            {
                SignalRCount++;
            });

            await connection.Start();


        }

        private void Button_StopHub(object sender, RoutedEventArgs e)
        {
            _webapp.Dispose();
            btnStartHub.IsEnabled = true;
            btnStopHub.IsEnabled = false;
        }

        private async void btnStartTwitter_Click(object sender, RoutedEventArgs e)
        {

            //auth
            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new SingleUserInMemoryCredentialStore
                {
                    ConsumerKey = ConfigurationManager.AppSettings["consumerKey"],
                    ConsumerSecret = ConfigurationManager.AppSettings["consumerSecret"],
                    AccessToken = ConfigurationManager.AppSettings["accessToken"],
                    AccessTokenSecret = ConfigurationManager.AppSettings["accessTokenSecret"]
                }
            };

            //connect
            var twitterCtx = new TwitterContext(auth);

            //buttons
            btnStartTwitter.IsEnabled = false;
            btnStopTwitter.IsEnabled = true;
            _stopTwitter = false;

            //subscribe to stream
            TwitterCount = 0;
            await twitterCtx.Streaming.Where(strm => strm != null && strm.Type == StreamingType.Filter && strm.Track == TwitterHashtag).StartAsync(async strm =>
                 {
                     await NewTweet(strm);
                 });
        }

        private async Task NewTweet(StreamContent strm)
        {
            //check if we should stop
            if (_stopTwitter)
            {
                strm.CloseStream();
                return;
            }
            
            //increase the count
            TwitterCount++;
            
            //check if we are rate limited - DEMO GODS, PLEASE!!!
            if (strm.EntityType == StreamEntityType.Limit)
            {
                MessageBox.Show("Rate Limit");
                Thread.Sleep(5000);
                return;
            }

            //we are only interested in Status 
            if (strm.EntityType != StreamEntityType.Status) return;

            Status status = (Status) strm.Entity;
            
            //get the tweet
            var tweet = await GetTweet(status);

            //upload to SharePoint
            UploadSharePoint(tweet);

            //push it to Azure
            PushTweetToAzure(tweet);
            
            //Add it to the recent 3 tweets grid
            Dispatcher.InvokeAsync(() => { Tweets.Add(tweet); });

            //sleep
            Thread.Sleep(1000);
        }

        private async Task<Tweet> GetTweet(Status status)
        {
            Tweet tweet = new Tweet();
            tweet.Text = status.Text;
            tweet.Url = string.Format("https://twitter.com/{0}/status/{1}", status.User.ScreenNameResponse, status.StatusID);
            tweet.Mention = string.Join("; ", status.Entities.UserMentionEntities.Select(x => string.Concat("@", x.ScreenName)));
            tweet.StatusId = status.StatusID.ToString();

            var score = await GetSentiment(status);

            tweet.Score = score;

            string sentiment = "";
            if ((int) score == -1)
            {
                //do nothing
            }
            else if (score < 0.1)
            {
                sentiment = "very bad";
            }
            else if (score < 0.3)
            {
                sentiment = "bad";
            }
            else if (score < 0.6)
            {
                sentiment = "neutral";
            }
            else if (score < 0.8)
            {
                sentiment = "good";
            }
            else if (score >= 0.8)
            {
                sentiment = "awesome";
            }
            tweet.Sentiment = sentiment;
            return tweet;
        }

        private async void UploadSharePoint(Tweet tweet)
        {
            if (_stopSharePoint) return;

            string siteUrl = "https://intranet.demo.com/sites/azuresearch";

            ClientContext clientContext = new ClientContext(siteUrl);
            List oList = clientContext.Web.Lists.GetByTitle("Tweets");

            ListItemCreationInformation itemCreateInfo = new ListItemCreationInformation();
            ListItem oListItem = oList.AddItem(itemCreateInfo);
            oListItem["Title"] = tweet.Text;
            oListItem["Url"] = tweet.Url;
            oListItem["Mention"] = tweet.Mention;
            oListItem["StatusId"] = tweet.StatusId;

            oListItem["Score"] = tweet.Score;

            oListItem["Sentiment"] = tweet.Sentiment;

            oListItem.Update();
            clientContext.ExecuteQuery();
            SharePointCount++;
        }

        private async Task<double> GetSentiment(Status content)
        {
            if (!SentimentEnabled) return -1;
            //url https://api.datamarket.azure.com/data.ashx/amla/text-analytics/v1/GetSentiment?Text=<TextToAnalyse>

            WebClient client = new WebClient();
            var token = Base64.EncodeTo64(string.Format("AccountKey:{0}", ConfigurationManager.AppSettings["accountKey"]));
            client.Headers.Add("Authorization", string.Format("Basic {0}", token));

            var url = string.Format("https://api.datamarket.azure.com/data.ashx/amla/text-analytics/v1/GetSentiment?Text={0}", content.Text);
            string sentiment = await client.DownloadStringTaskAsync(new Uri(url));

            //{"odata.metadata":"https://api.datamarket.azure.com/data.ashx/amla/text-analytics/v1/$metadata#TextAnalytics.FrontEndService.Models.SentimentResult","Score":0.8359476}
            sentiment = sentiment.Replace("\r\n", "");
            var value = _regexSentiment.Match(sentiment).Groups[1].Value;

            return double.Parse(value);
        }

        private void btnStopTwitter_Click(object sender, RoutedEventArgs e)
        {
            btnStartTwitter.IsEnabled = true;
            btnStopTwitter.IsEnabled = false;
            _stopTwitter = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void btnStartSharePoint_Click(object sender, RoutedEventArgs e)
        {
            _stopSharePoint = false;
            SharePointCount = 0;
            btnStartSharePoint.IsEnabled = false;
            btnStopSharePoint.IsEnabled = true;
        }

        private void btnStopSharePoint_Click(object sender, RoutedEventArgs e)
        {
            _stopSharePoint = true;
            btnStartSharePoint.IsEnabled = true;
            btnStopSharePoint.IsEnabled = false;
        }

        private void btnCreateSchema_Click(object sender, RoutedEventArgs e)
        {
            CreateIndex();
        }

        private void CreateIndex()
        {
            // Create the Azure Search index based on the included schema
            try
            {
                var definition = new Index
                {
                    Name = "twittersearch",
                    Fields = new[]
                    {
                        new ASField("Text", DataType.String) {IsKey = false, IsSearchable = true, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                        new ASField("Mention", DataType.String) {IsKey = false, IsSearchable = true, IsFilterable = true, IsSortable = false, IsFacetable = true, IsRetrievable = true},
                        new ASField("Created", DataType.DateTimeOffset) {IsKey = false, IsSearchable = false, IsFilterable = true, IsSortable = true, IsFacetable = false, IsRetrievable = true},
                        new ASField("Url", DataType.String) {IsKey = false, IsSearchable = true, IsFilterable = false, IsSortable = false, IsFacetable = true, IsRetrievable = true},
                        new ASField("StatusId", DataType.String) {IsKey = true, IsSearchable = true, IsFilterable = false, IsSortable = true, IsFacetable = true, IsRetrievable = true},
                        new ASField("Sentiment", DataType.String) {IsKey = false, IsSearchable = true, IsFilterable = false, IsSortable = true, IsFacetable = true, IsRetrievable = true},
                        new ASField("Score", DataType.Double),
                    }
                };

                SearchServiceClient.Indexes.Create(definition);
            }
            catch (Exception ex)
            {
            }
        }

        private void btnDeleteIndex_Click(object sender, RoutedEventArgs e)
        {
            DeleteIndex();
        }

        private void DeleteIndex()
        {
            SearchServiceClient.Indexes.Delete("twittersearch");
        }

        private void btnReindex_Click(object sender, RoutedEventArgs e)
        {
            DeleteIndex();
            CreateIndex();
            string siteUrl = "https://intranet.demo.com/sites/azuresearch";
            ClientContext clientContext = new ClientContext(siteUrl);
            List oList = clientContext.Web.Lists.GetByTitle("Tweets");

            var query = CamlQuery.CreateAllItemsQuery(1000);
            ListItemCollection items = oList.GetItems(query);

            clientContext.Load(items);
            clientContext.ExecuteQuery();

            List<Tweet> tweets = new List<Tweet>();
            foreach (var item in items)
            {
                var tweet = new Tweet
                {
                    Mention = (string)item["Mention"],
                    Text = (string)item["Title"],
                    Sentiment = (string)item["Sentiment"],
                    Score = (double)item["Score"],
                    StatusId = (string)item["StatusId"],
                    Url = ((FieldUrlValue)item["Url"]).Url
                };
                tweets.Add(tweet);
            }

            SearchIndexClient indexClient = SearchServiceClient.Indexes.GetClient("twittersearch");

            try
            {
                indexClient.Documents.Index(IndexBatch.Create(tweets.Select(doc => IndexAction.Create(doc))));
            }
            catch (IndexBatchException ex)
            {
            }

        }

        private void SearchDocuments()
        {
            try
            {

                // Execute search based on search text and optional filter
                var sp = new SearchParameters();

                SearchIndexClient indexClient = SearchServiceClient.Indexes.GetClient("twittersearch");
                DocumentSearchResponse<Tweet> response = indexClient.Documents.Search<Tweet>(SearchText, sp);
                foreach (SearchResult<Tweet> result in response)
                {
                    Console.WriteLine(result.Document);
                    SearchResults.Add(result.Document);
                }

            }
            catch (Exception ex)
            {

            }
        }

        private async void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchResults.Clear();
            Stopwatch watch = new Stopwatch();
            watch.Start();
            SearchDocuments();
            watch.Stop();

            SearchTime = watch.ElapsedMilliseconds;
        }


        private void btnStopAzureSearch_Click(object sender, RoutedEventArgs e)
        {
            AzureSearchEnabled = false;
            btnStartAzureSearch.IsEnabled = true;
            btnStopSAzureSearch.IsEnabled = false;
        }

        private void btnStartAzureSearch_Click(object sender, RoutedEventArgs e)
        {
            AzureSearchEnabled = true;
            btnStartAzureSearch.IsEnabled = false;
            btnStopSAzureSearch.IsEnabled = true;
        }

        public void PushTweetToAzure(Tweet tweet)
        {
            if (!AzureSearchEnabled) return;
            SearchIndexClient indexClient = SearchServiceClient.Indexes.GetClient("twittersearch");

            try
            {
                indexClient.Documents.Index(IndexBatch.Create(IndexAction.Create(tweet)));
                AzureSearchCount++;
            }
            catch (IndexBatchException ex)
            {
            }
        }
    }
}

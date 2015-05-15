using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LinqToTwitter;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.Owin.Hosting;
using Microsoft.SharePoint.Client;
using List = Microsoft.SharePoint.Client.List;
using ListItem = Microsoft.SharePoint.Client.ListItem;

namespace MaxMelcher.AzureSearch.DataHub
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private IDisposable webapp;
        private string _twitterHashtag = "#gntm";
        private readonly Regex regexSentiment = new Regex(".*Score\":(\\d\\.(\\d)*)*}$", RegexOptions.Multiline);


        private bool _stopTwitter = true;
        private int _signalRCount;
        private int _sharePointCount;
        private int _twitterCount;
        private bool _stopSharePoint = true;
        private bool _sentimentEnabled;

        public string TwitterHashtag
        {
            get { return _twitterHashtag; }
            set { _twitterHashtag = value; NotifyPropertyChanged("TwitterHashtag"); }
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

        public MainWindow()
        {
            InitializeComponent();
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            Grid.DataContext = this;
        }

        private async void Button_StartHub(object sender, RoutedEventArgs e)
        {
            webapp = WebApp.Start<Startup>("http://*:4242/");
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
            webapp.Dispose();
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
            await twitterCtx.Streaming.Where(strm => strm.Type == StreamingType.Filter && strm.Track == TwitterHashtag).StartAsync(async strm =>
                 {
                     Debugger.Log(1, "Twitter", strm.Content);
                     TwitterCount++;

                     if (_stopTwitter) { strm.CloseStream(); }

                     if (strm.EntityType == StreamEntityType.Status)
                     {
                         UploadSharePoint((Status)strm.Entity);
                     }
                 });
        }

        private async void UploadSharePoint(Status content)
        {
            if (_stopSharePoint) return;

            string siteUrl = "https://intranet.demo.com/sites/azuresearch";

            ClientContext clientContext = new ClientContext(siteUrl);
            List oList = clientContext.Web.Lists.GetByTitle("Tweets");

            ListItemCreationInformation itemCreateInfo = new ListItemCreationInformation();
            ListItem oListItem = oList.AddItem(itemCreateInfo);
            oListItem["Title"] = content.Text;
            oListItem["Url"] = string.Format("https://twitter.com/{0}/status/{1}", content.User.ScreenNameResponse, content.StatusID);
            oListItem["Mention"] = string.Join("; ", content.Entities.UserMentionEntities.Select(x => string.Concat("@", x.ScreenName)));
            var score = await GetSentiment(content);
            if (score > -1)
            {
                oListItem["Score"] = score;
            }

            string sentiment = "";
            if (score == -1)
            {
                //do nothing
            }
            if (score < 0.1)
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
            oListItem["Sentiment"] = sentiment;

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
            var value = regexSentiment.Match(sentiment).Groups[1].Value;

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
    }
}

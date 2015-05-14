using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LinqToTwitter;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Hosting;
using Owin;

namespace MaxMelcher.AzureSearch.DataHub
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private IDisposable webapp;
        private string _twitterHashtag = "#gntm";
        
        
        private bool _stopTwitter = true;
        private int _signalRCount;
        private int _sharePointCount;
        private int _twitterCount;
        private bool _stopSharePoint = true;

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

        public MainWindow()
        {
            InitializeComponent();
            Grid.DataContext = this;
        }

        private void Button_StartHub(object sender, RoutedEventArgs e)
        {
            webapp = WebApp.Start<Startup>("http://*:4242/");
            btnStartHub.IsEnabled = false;
            btnStopHub.IsEnabled = true;
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

                     if (_stopTwitter) {  strm.CloseStream();}

                     UploadSharePoint(strm.Content);
                 });
        }

        private void UploadSharePoint(string content)
        {
            if (_stopSharePoint) return;

            SharePointCount++;
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

    class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
    public class MyHub : Hub
    {
        public void Send(string name, string message)
        {
            Clients.All.addMessage(name, message);
        }
    }
}

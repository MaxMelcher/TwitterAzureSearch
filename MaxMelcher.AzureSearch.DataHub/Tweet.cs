namespace MaxMelcher.AzureSearch.DataHub
{
    public class Tweet
    {
        public string Text { get; set; }
        public string Mention { get; set; }
        public string Url { get; set; }
        public string Sentiment { get; set; }
        public double Score { get; set; }
    }
}
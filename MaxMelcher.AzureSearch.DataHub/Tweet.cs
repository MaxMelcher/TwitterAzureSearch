using System;

namespace MaxMelcher.AzureSearch.DataHub
{
    public class Tweet
    {
        public Tweet()
        {
            Created = DateTime.Now;
        }

        public string StatusId { get; set; }
        public string Text { get; set; }
        public string Mention { get; set; }
        public string Url { get; set; }
        public string Sentiment { get; set; }
        public double Score { get; set; }
        private DateTime Created { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
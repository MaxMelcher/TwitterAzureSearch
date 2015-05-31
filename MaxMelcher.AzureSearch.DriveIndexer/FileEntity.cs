using MaxMelcher.AzureSearch.DataHub;

namespace MaxMelcher.AzureSearch.DriveIndexer
{
    public class FileEntity
    {
        public string FileName { get; set; }
        public string Path { get; set; }

        public string Key
        {
            get { return Base64.EncodeTo64(FileName); }
        }
    }
}
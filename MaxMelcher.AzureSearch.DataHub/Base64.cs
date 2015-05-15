using System.Text;

namespace MaxMelcher.AzureSearch.DataHub
{
    public static class Base64
    {
        public static string EncodeTo64(string toEncode)
        {
            byte[] toEncodeAsBytes = UTF8Encoding.UTF8.GetBytes(toEncode);
            return System.Convert.ToBase64String(toEncodeAsBytes);
        }

        public static string DecodeFrom64(string encodedData)
        {
            byte[] encodedDataAsBytes = System.Convert.FromBase64String(encodedData);

            return UTF8Encoding.UTF8.GetString(encodedDataAsBytes);
        }
    }
}
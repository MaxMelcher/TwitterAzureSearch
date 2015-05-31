using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace MaxMelcher.AzureSearch.DriveIndexer
{
    class Program
    {
        private static SearchServiceClient _searchServiceClient;

        static void Main(string[] args)
        {
            ResetIndex();
            IndexFiles();
        }

        public static SearchServiceClient SearchServiceClient
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

        private static void ResetIndex()
        {
            Console.WriteLine("Resetting index");
            SearchServiceClient.Indexes.Delete("drivesearch");
            CreateIndex();
            Console.WriteLine("Press any key to continue");
            Console.ReadKey(true);
        }

        private static void CreateIndex()
        {
            // Create the Azure Search index based on the included schema
            try
            {
                var definition = new Index
                {
                    Name = "drivesearch",
                    Fields = new[]
                    {
                        new Field("FileName", DataType.String) {IsKey = false, IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = false, IsRetrievable = true},
                        new Field("Path", DataType.String) {IsKey = false, IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = false, IsRetrievable = true},
                        new Field("Key", DataType.String) {IsKey = true, IsRetrievable = true},
                    }
                };
                SearchServiceClient.Indexes.Create(definition);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ex: {0}", ex.Message);
            }
        }
        private static void IndexFiles()
        {
            Stopwatch w = new Stopwatch();
            w.Start();
            DirectoryInfo dir = new DirectoryInfo("C:\\Users\\mmelcher\\Desktop");
            Console.WriteLine("Enumerating " + dir.FullName);
            var files = dir.EnumerateFiles("*.*", SearchOption.AllDirectories).AsParallel();

            List<FileEntity> entities = new List<FileEntity>();
            
            foreach (var fileInfo in files)
            {
                Console.WriteLine("File: {0}, Path: {1}", fileInfo.Name, fileInfo.FullName);
                entities.Add(new FileEntity{FileName = fileInfo.Name, Path = fileInfo.FullName});
            }
            w.Stop();
            Console.WriteLine("Enumerated in {0}ms", w.ElapsedMilliseconds);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey(true);


            SearchIndexClient indexClient = SearchServiceClient.Indexes.GetClient("drivesearch");

            w.Restart();
            try
            {
                for (int i = 0; i < entities.Count/1000; i++)
                {
                    indexClient.Documents.Index(IndexBatch.Create(entities.Skip(i*1000).Take(1000).Select(doc => IndexAction.Create(doc))));
                }
            }
            catch (IndexBatchException ex)
            {
            }
            w.Stop();
            Console.WriteLine("Sent to Azure Search in {0}ms", w.ElapsedMilliseconds);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey(true);

        }
    }
}

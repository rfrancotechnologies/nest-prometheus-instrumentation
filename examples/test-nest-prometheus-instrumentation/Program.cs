using System;
using System.Collections.Generic;
using System.Threading;
using Com.RFranco.Nest.Instrumentation;
using Elasticsearch.Net;
using Nest;
using Prometheus;

namespace Com.Rfranco.Nest.Instrumentation.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, ev) =>
            {
                exitEvent.Set();
                ev.Cancel = true;
            };


            MetricServer metricServer = new MetricServer(port: 1234);   //  http://localhost:1234/metrics
            metricServer.Start();

            var conf = new ElasticSearchConfiguration()
            {
                EsIndex = "dummy-index",
                EsNodes = new List<string>(){"http://localhost:9200"}
            };

            var client = GetElasticClient(conf);
            
            var id = Guid.NewGuid().ToString();
            //  Test Save
            var dummyDoc = new DummyDoc()
            {
                Id = id,
                Subject = "It is a dummy doc"
            };
            var response = client.Index(new IndexRequest<DummyDoc>(dummyDoc, index: conf.EsIndex,
                type: typeof(DummyDoc).Name, id: dummyDoc.Id));

            if (!response.IsValid)
            {
                Console.Error.WriteLine(response.OriginalException.Message);
            }

            Console.WriteLine($"Dummy doc with id {id} saved on index {conf.EsIndex}");

            //Test Get
            var response2 = client.Get<DummyDoc>(new GetRequest(index: conf.EsIndex,
                type: typeof(DummyDoc).Name, id: id));

            if (!response2.IsValid)
            {
                Console.Error.WriteLine(response2.OriginalException.Message);
            }

            Console.WriteLine($"Dummy doc retrieved {response2.Found}");
            Console.WriteLine("curl http://localhost:1234/metrics to check elasticsearch_* metrics");
            Console.WriteLine("Press Ctrl+C to exit");

            exitEvent.WaitOne();

            if (metricServer != null)
                    metricServer.StopAsync().Wait();            
        }


        private static IElasticClient GetElasticClient(ElasticSearchConfiguration elasticConfiguration)
        {
            var pool = new StaticConnectionPool(elasticConfiguration.EsNodeUris);
            var settings = new ConnectionSettings(pool).DefaultIndex(elasticConfiguration.EsIndex)
                .DefaultMappingFor<DummyDoc>(m => m.IndexName(elasticConfiguration.EsIndex).TypeName(typeof(DummyDoc).Name))
                .EnablePrometheus();    //  Enable Prometheus instrumentation
            

            return new ElasticClient(settings);
            
        }
    }

    public class DummyDoc
    {
        public string Id {get;set;}
        public string Subject {get;set;}
    }

    public class ElasticSearchConfiguration {
        public IEnumerable<string> EsNodes { get; set; }

        public string EsIndex { get; set; }

        public IEnumerable<Uri> EsNodeUris { 
            get {
                if (EsNodes == null)
                    return null;

                var result = new List<Uri>();
                foreach (var esNode in EsNodes)
                    result.Add(new Uri(esNode));
                return result;
            }
        }
    }
}

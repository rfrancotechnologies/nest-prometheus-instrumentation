# nest-prometheus-instrumentation

Prometheus instrumentation for NEST-Elasticsearch 6.X clients.

An example of client instrumentation:

```
var pool = new StaticConnectionPool(<EsNodeUris>);
var settings = new ConnectionSettings(pool).EnablePrometheus();
var client = new ElasticClient(settings);
```

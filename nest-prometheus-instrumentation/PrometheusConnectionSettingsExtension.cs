using System;
using System.Linq;
using Nest;
using Prometheus;

namespace Com.RFranco.Nest.Instrumentation
{
    /// <summary>
    /// Prometheus ConnectionSettings extension
    /// </summary>
    public static class PrometheusConnectionSettingsExtension
    {
        private static Counter ErrorRequestsProcessed = Prometheus.Metrics.CreateCounter("elasticsearch_request_error_total", "Number of unsuccessfull processed elasticsearch requests.", "method", "error_code");
        private static Gauge OngoingRequests = Prometheus.Metrics.CreateGauge("elasticsearch_request_in_progress", "Number of ongoing  elasticsearch requests.", "method");
        private static Histogram RequestResponseHistogram = Prometheus.Metrics.CreateHistogram("elasticsearch_request_duration_seconds", "Histogram of  elasticsearch request duration in seconds.",
                new HistogramConfiguration()
                {
                    LabelNames = new string[] { "method" },
                    Buckets = new double[] { .005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10 }
                });


        /// <summary>
        /// Prometheus metrics instrumentation:
        ///     - elasticsearch_request_error_total: Number of unsuccessfull processed elasticsearch requests.
        ///     - elasticsearch_request_in_progress: Number of ongoing  elasticsearch requests.
        ///     - elasticsearch_request_duration_seconds: Histogram of  elasticsearch request duration in seconds.
        /// </summary>
        /// <param name="settings">ConnectionSettings instance to instrument with prometheus</param>
        /// <param name="CustomMethodLabelExtractor">Custom method to extract NEST method used as label (NEST Apps can generate big label entrophy). PathAndQuery as <paramref name="CustomMethodLabelExtractor"/>. Return the method label to use for the request/ response. </param>
        /// <returns>Instance ConnectionSettinfo instrumentated</returns>
        public static ConnectionSettings EnablePrometheus(this ConnectionSettings settings, Func<string,string> CustomMethodLabelExtractor = null)
        {
            settings.OnRequestDataCreated(r =>
            {
                var method = $"{r.Method} {(CustomMethodLabelExtractor != null ? CustomMethodLabelExtractor(r.PathAndQuery) : DefaultMethodLabelExtractor(r.PathAndQuery))}";
                OngoingRequests.Labels(method).Inc();
            });

            settings.OnRequestCompleted(r =>
            {
                var method = $"{r.HttpMethod} {(CustomMethodLabelExtractor != null ? CustomMethodLabelExtractor(r.Uri.PathAndQuery) : DefaultMethodLabelExtractor(r.Uri.PathAndQuery))}";

                if (!r.Success)
                {
                    ErrorRequestsProcessed.Labels(method, r.HttpStatusCode.ToString()).Inc();
                }

                RequestResponseHistogram.Labels(method).Observe((DateTime.UtcNow - r.AuditTrail.First().Started).TotalSeconds);
                OngoingRequests.Labels(method).Dec();
            });

            return settings;

        }

        /// <summary>
        /// Remove first /, ? query param and last path parameter.
        /// If the app generate high label entrophy, please override this method with a custom method label extractor.
        /// </summary>
        /// <param name="originalMethodName">PathAndQuery value of the current request / response</param>
        /// <returns>Method label extracted</returns>
        private static string DefaultMethodLabelExtractor(string originalMethodName)
        {
            //  
            //  Take care if app  generate too much labels is needed to 
            int start = originalMethodName.StartsWith("/") ? 1 : 0;
            int end = originalMethodName.Length;
            
            try
            {
                if(originalMethodName.IndexOf("?") > 0)
                    end = originalMethodName.IndexOf("?");
                else if(!KNWON_NEST_METHODS.Contains(originalMethodName.Substring(originalMethodName.LastIndexOf("/") + 1).ToLower()))
                    end = originalMethodName.LastIndexOf("/");
            
            } catch(ArgumentOutOfRangeException) {}
             
            return originalMethodName.Substring(start, (end - start));            
        }

        //  Elasticsearch.Net.xml , Nest.xml
        private static string [] KNWON_NEST_METHODS = new string [] {
            "scroll", "_bulk", "aliases","allocation","count","fielddata","health","_cat","indices","master","nodeattrs",
            "nodes","pending_tasks","plugins","recovery","repositories","segments","shards","snapshots","tasks",
            "templates","thread_pool","explain","settings","info","reroute","state","stats","_cluster","_count","_create",
            "_delete_by_query","_rethrottle","_scripts","_source","_explain","_field_caps","_analyze","clear",
            "_cache","_close","_flush","synced","_forcemerge","_alias","_mapping","_settings","_template","_upgrade",
            "_open","_mappings","_recovery","_refresh","_rollover","_segments","_shard_stores","_stats","_aliases",
            "query","pipeline","grok","_simulate","_mget","_msearch","template","_mtermvectors","hotthreads","_nodes",
            "reload_secure_settings","usage","_reindex","_execute","_search_shards","_snapshot","_restore","_status",
            "_verify","_cancel","_tasks","_termvectors","_update","_update_by_query","follow","auto_follow","pause_follow",
            "resume_follow","unfollow","_explore","policy","remove","retry","start","stop","_xpack","license",
            "basic_status","trial_status","start_basic","_delete_expired_data","_forecast","buckets","calendars","events",
            "categories","datafeeds","filters","influencers","anomaly_detectors","model_snapshots","overall_buckets",
            "records","info","_data","_preview","_revert","_start","_stop","_update","_validate","detector","deprecations",
            "assistance","job","data","_rollup_search","api_key","_authenticate","_password","_clear_cache","_disable",
            "_enable","privilege","role","role_mapping","token","user","_privileges","_has_privileges","sql","translate",
            "certificates","_ack","_activate","_deactivate","_restart"
        };

    }
}
# Grafana Cloud dashboards

- `podbridge-overview.json`: podcast-filtered refresh counters, cycle summaries, warning/error logs and refresh traces.
- `podbridge-dotnet.json`: runtime, garbage collection, thread pool and HTTP panels adapted from RaspiConfig's .NET Application Dashboard.
- Dashboard UIDs are stable: `podbridge-overview` and `podbridge-dotnet`.
- Default datasource UIDs are `grafanacloud-prom`, `grafanacloud-logs` and `grafanacloud-traces`; datasource variables allow selecting alternatives.
- Metrics use `job="podbridge"`; logs use `service_name="podbridge"`; traces use `resource.service.name="podbridge"` and `span.podcast.id`.

## Import

In Grafana, create a **PodBridge** folder, select **Dashboards > New > Import**, and upload each JSON file. Select the PodBridge folder before importing. No alert rules are included.

The definitions use Grafana's [Classic dashboard JSON model](https://grafana.com/docs/grafana/latest/dashboards/build-dashboards/view-dashboard-json-model/) and the existing RaspiConfig dashboard as the field-format reference. Query models are verified through Grafana's `/api/ds/query` endpoint with the Cloud Prometheus, Loki and Tempo datasources.

## Interpretation

- Refresh and episode counts use `increase` over the selected range. They are extrapolated estimates, can be fractional and cannot recover increments that occurred before the first exported counter sample.
- Episodes fetched means all retrieved episodes, including repeated retrievals; it is neither new episodes nor the current cache size.
- A zero-denominator success rate is undefined, not 100%. The dashboard displays no activity rather than claiming success.
- Podcast options come from refresh telemetry, not the database. A podcast that has emitted no refresh telemetry cannot appear yet.
- A missing failure series is treated as zero only when success samples exist for the selection.
- Cycle summaries cover all podcasts. Warning/error filtering also searches the message so global health-check failures mentioning a podcast remain discoverable.
- Current feed freshness and episode inventory require additional application metrics. Counter sample timestamps do not represent the last successful refresh.
- The exported thread pool gauges currently have `_total` suffixes; their values are displayed directly, without `rate`.
- Existing managed Cloud datasource correlations are used without modifying the datasources. The trace table also supplies an explicit Explore link.
- The trace query selects only `RefreshPodcast` spans and sets Tempo's `spss` (spans per span set) to 100 instead of the default three-span preview. Larger cycles remain available in the full trace.

AI-assisted with OpenCode.

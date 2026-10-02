using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Npgsql;

namespace Tests.SystemTests;

internal static class SystemTestsHelper
{
    public static async Task SeedPodcastAsync(string podcastId, string showId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(SystemTestsFixture.PostgresConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """INSERT INTO "Podcasts" ("PodcastId", "ShowId") VALUES (@podcastId, @showId)""";
        command.Parameters.AddWithValue("podcastId", podcastId);
        command.Parameters.AddWithValue("showId", showId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // The analyzer cannot follow the "return the live response, dispose it otherwise" control flow of this
    // polling loop: on the success path ownership of the still-open response is transferred to the caller,
    // on every other path it is disposed before the next attempt. Both are intentional, not a leak.
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP011:Don't return disposed instance", Justification = "Response is only disposed on the non-returning loop paths; the returned instance is always live.")]
    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP017:Prefer using", Justification = "A using declaration would dispose the response before it can be returned to the caller on the success path.")]
    public static async Task<HttpResponseMessage> WaitForFeedToBePopulatedAsync(HttpClient httpClient, string expectedTitleFragment, CancellationToken cancellationToken)
    {
        // The fixture podcasts are only seeded into Postgres after the container reports healthy, by which
        // point the worker's first refresh cycle (which runs immediately at startup, see
        // EpisodeRefreshWorker.ExecuteAsync) has already completed against an empty Podcasts table. The
        // podcasts are therefore only picked up on the *next* PeriodicTimer tick, up to a full
        // PodBridge__RefreshInterval=00:00:10 (10s) later - the wait window below must comfortably exceed
        // that to avoid flakiness.
        const int maxAttempts = 30;
        var delayBetweenAttempts = TimeSpan.FromSeconds(2);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await httpClient.GetAsync("/api/podcasts", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (content.Contains(expectedTitleFragment, StringComparison.Ordinal))
                {
                    return response; // Caller owns disposal
                }
            }

            response.Dispose();

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(delayBetweenAttempts, cancellationToken);
            }
        }

        var totalWaitSeconds = maxAttempts * delayBetweenAttempts.TotalSeconds;
        throw new TimeoutException($"Feed not populated with '{expectedTitleFragment}' after {maxAttempts.ToString(CultureInfo.InvariantCulture)} attempts over {totalWaitSeconds.ToString(CultureInfo.InvariantCulture)} seconds");
    }

    [SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP014:Use a single instance of HttpClient", Justification = "Short-lived system test clients targeting Testcontainers-assigned ports that only exist for the duration of these tests; a shared instance isn't feasible.")]
    public static HttpClient CreateAuthenticatedHttpClient()
    {
        var httpClient = new HttpClient { BaseAddress = SystemTestsFixture.PodBridgeBaseAddress };
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"systemtestuser:systemtestpass"));
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        return httpClient;
    }
}

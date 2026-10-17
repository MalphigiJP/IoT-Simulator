
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;

using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public static class FleetLoadTest
{

    private sealed record QueueSnapshot
    {
        public long Accepted { get; init; }
        public long Processed { get; init; }
        public long Failed { get; init; }
        public long Rejected { get; init; }

        public int Waiting { get; init; }
        public int Processing { get; init; }
        public int HighWatermark { get; init; }
    }

    private sealed record FleetDevice(
        SimulatedDevice Device,
        int SensorId
    );

    private sealed class LoadCounters
    {
        private long _attempted;
        private long _accepted;
        private long _queueFull;
        private long _rejected;
        private long _errors;

        public ConcurrentBag<double> LatenciesMs { get; }
            = new();

        public long Attempted =>
            Interlocked.Read(ref _attempted);

        public long Accepted =>
            Interlocked.Read(ref _accepted);

        public long QueueFull =>
            Interlocked.Read(ref _queueFull);

        public long Rejected =>
            Interlocked.Read(ref _rejected);

        public long Errors =>
            Interlocked.Read(ref _errors);

        public void RecordAttempt() =>
            Interlocked.Increment(ref _attempted);

        public void RecordResponse(
            HttpStatusCode status,
            double latencyMs)
        {
            LatenciesMs.Add(latencyMs);

            if (status == HttpStatusCode.Accepted)
            {
                Interlocked.Increment(ref _accepted);
            }
            else if (
                status == HttpStatusCode.TooManyRequests)
            {
                Interlocked.Increment(ref _queueFull);
            }
            else
            {
                Interlocked.Increment(ref _rejected);
            }
        }

        public void RecordError() =>
            Interlocked.Increment(ref _errors);
    }

    public static async Task<int> RunAsync(
        LoadTestSettings settings)
    {
        using var cancellation =
            new CancellationTokenSource();

        ConsoleCancelEventHandler handler =
            (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

        Console.CancelKeyPress += handler;

        try
        {
            return await ExecuteAsync(
                settings,
                cancellation.Token
            );
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            Console.WriteLine(
                "Load test cancelled by operator."
            );

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Load test failed: {ex.Message}"
            );

            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static async Task<int> ExecuteAsync(
        LoadTestSettings settings,
        CancellationToken token)
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(
                settings.ApiUrl.TrimEnd('/') + "/"
            ),
            Timeout = TimeSpan.FromSeconds(20)
        };

        var profiles = CreateProfiles(
            settings.DeviceCount
        );

        Console.WriteLine();
        Console.WriteLine(
            "========================================"
        );
        Console.WriteLine(
            "       SMART-X FLEET LOAD TEST"
        );
        Console.WriteLine(
            "========================================"
        );

        Console.WriteLine($"API: {settings.ApiUrl}");
        Console.WriteLine(
            $"Virtual devices: {settings.DeviceCount}"
        );
        Console.WriteLine(
            $"Target RPS: {settings.RequestsPerSecond}"
        );
        Console.WriteLine(
            $"Duration: {settings.DurationSeconds}s"
        );
        Console.WriteLine(
            $"HTTP workers: {settings.HttpWorkers}"
        );

        long plannedRequests =
            (long)settings.RequestsPerSecond *
            settings.DurationSeconds;

        Console.WriteLine(
            $"Planned requests: {plannedRequests}"
        );
        Console.WriteLine();

        Console.WriteLine(
            "[1/4] Registering/reusing virtual devices..."
        );

        var fleet = await RegisterFleetAsync(
            http,
            profiles,
            settings.RegistrationWorkers,
            token
        );

        Console.WriteLine(
            $"Registration complete: {fleet.Length} devices."
        );

        // Read baseline statistics after registration.
        var baseline = await GetQueueStatusAsync(
            http,
            token
        );

        var channel = Channel.CreateBounded<SimulatedTelemetry>(
            new BoundedChannelOptions(
                Math.Max(
                    100,
                    settings.RequestsPerSecond * 2
                )
            )
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            }
        );

        var counters = new LoadCounters();

        Console.WriteLine();
        Console.WriteLine(
            "[2/4] Starting measured HTTP load..."
        );

        // Observe backend queue depth while traffic
        // is arriving.
        using var observerStop =
            CancellationTokenSource.CreateLinkedTokenSource(
                token
            );

        int observedPeakWaiting = baseline.Waiting;

        var observer = ObserveAsync(
            http,
            counters,
            value =>
            {
                observedPeakWaiting = Math.Max(
                    observedPeakWaiting,
                    value
                );
            },
            observerStop.Token
        );

        // A fixed number of workers consume packets.
        var workers = Enumerable
            .Range(0, settings.HttpWorkers)
            .Select(_ => ConsumeAsync(
                channel.Reader,
                http,
                counters,
                token
            ))
            .ToArray();

        var sendWatch = Stopwatch.StartNew();

        try
        {
            await ProduceAsync(
                channel.Writer,
                fleet,
                plannedRequests,
                settings.RequestsPerSecond,
                token
            );
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        await Task.WhenAll(workers);

        sendWatch.Stop();

        observerStop.Cancel();
        await observer;

        Console.WriteLine();
        Console.WriteLine(
            "[3/4] Waiting for backend queue to drain..."
        );

        var drainWatch = Stopwatch.StartNew();

        QueueSnapshot finalQueue =
            await GetQueueStatusAsync(http, token);

        while (
            settings.DrainSeconds > 0 &&
            (
                finalQueue.Waiting > 0 ||
                finalQueue.Processing > 0
            ) &&
            drainWatch.Elapsed.TotalSeconds <
                settings.DrainSeconds
        )
        {
            await Task.Delay(500, token);

            finalQueue = await GetQueueStatusAsync(
                http,
                token
            );

            observedPeakWaiting = Math.Max(
                observedPeakWaiting,
                finalQueue.Waiting
            );
        }

        drainWatch.Stop();

        Console.WriteLine();
        Console.WriteLine(
            "[4/4] Performance report"
        );

        var latencies = counters.LatenciesMs.ToArray();
        Array.Sort(latencies);

        double elapsedSeconds = Math.Max(
            0.001,
            sendWatch.Elapsed.TotalSeconds
        );

        long processedDelta =
            finalQueue.Processed - baseline.Processed;

        long failedDelta =
            finalQueue.Failed - baseline.Failed;

        long acceptedDelta =
            finalQueue.Accepted - baseline.Accepted;

        Console.WriteLine();
        Console.WriteLine(
            "========================================"
        );
        Console.WriteLine(
            "       SMART-X LOAD TEST RESULTS"
        );
        Console.WriteLine(
            "========================================"
        );

        Console.WriteLine(
            $"Virtual devices: {fleet.Length}"
        );
        Console.WriteLine(
            $"Target RPS: {settings.RequestsPerSecond}"
        );
        Console.WriteLine(
            $"HTTP test duration: {elapsedSeconds:F2}s"
        );

        Console.WriteLine();
        Console.WriteLine("--- HTTP TRAFFIC ---");

        Console.WriteLine(
            $"Attempts: {counters.Attempted}"
        );
        Console.WriteLine(
            $"HTTP 202 accepted: {counters.Accepted}"
        );
        Console.WriteLine(
            $"HTTP 429 queue full: {counters.QueueFull}"
        );
        Console.WriteLine(
            $"Other HTTP responses: {counters.Rejected}"
        );
        Console.WriteLine(
            $"Network/time-out errors: {counters.Errors}"
        );

        Console.WriteLine(
            $"Achieved request rate: " +
            $"{counters.Attempted / elapsedSeconds:F2} req/s"
        );

        Console.WriteLine();
        Console.WriteLine("--- HTTP LATENCY ---");

        Console.WriteLine(
            $"P50: {Percentile(latencies, 0.50):F2} ms"
        );
        Console.WriteLine(
            $"P95: {Percentile(latencies, 0.95):F2} ms"
        );
        Console.WriteLine(
            $"P99: {Percentile(latencies, 0.99):F2} ms"
        );

        Console.WriteLine();
        Console.WriteLine("--- SERVER PROCESSING ---");

        Console.WriteLine(
            $"Queue accepted delta: {acceptedDelta}"
        );
        Console.WriteLine(
            $"SQL processed delta: {processedDelta}"
        );
        Console.WriteLine(
            $"Processing failures delta: {failedDelta}"
        );
        Console.WriteLine(
            $"Observed peak waiting: {observedPeakWaiting}"
        );
        Console.WriteLine(
            $"Final queue waiting: {finalQueue.Waiting}"
        );
        Console.WriteLine(
            $"Final processing: {finalQueue.Processing}"
        );

        bool drained =
            finalQueue.Waiting == 0 &&
            finalQueue.Processing == 0;

        Console.WriteLine(
            $"Queue drained: {(drained ? "YES" : "NO")}"
        );

        Console.WriteLine();
        Console.WriteLine(
            "NOTE: Queue counters are cumulative since " +
            "API startup; deltas require an otherwise idle API."
        );

        Console.WriteLine(
            "HTTP 202 means queued, not yet persisted."
        );

        return counters.Errors == 0 &&
               counters.Rejected == 0 &&
               drained
            ? 0
            : 1;
    }

    private static List<DeviceProfile> CreateProfiles(
        int count)
    {
        var profiles = new List<DeviceProfile>(count);

        for (int i = 1; i <= count; i++)
        {
            var kind = (DeviceKind)((i - 1) % 4);

            string category = kind switch
            {
                DeviceKind.EnvironmentalEsp32 =>
                    "Environmental",

                DeviceKind.SmartPlug =>
                    "SmartPlug",

                DeviceKind.IrrigationValve =>
                    "IrrigationValve",

                DeviceKind.PumpController =>
                    "PumpController",

                _ => "Unknown"
            };

            string mac =
                $"02:53:59:{(i >> 16) & 255:X2}:" +
                $"{(i >> 8) & 255:X2}:{i & 255:X2}";

            profiles.Add(new DeviceProfile
            {
                Name = $"Load Device {i:D4}",
                MacAddress = mac,
                Location = $"Load Test Zone {(i - 1) / 50 + 1}",
                Category = category,
                Kind = kind,
                Description = "Virtual load-test sensor",
                RandomSeed = 10000 + i
            });
        }

        return profiles;
    }

    private static async Task<FleetDevice[]>
        RegisterFleetAsync(
            HttpClient http,
            List<DeviceProfile> profiles,
            int concurrency,
            CancellationToken token)
    {

        var sensors = await http.GetFromJsonAsync<
            List<RegisteredSensor>>(
                "api/sensors",
                token
            ) ?? new();

        var known = sensors.ToDictionary(
            sensor => sensor.MacAddress.Trim(),
            sensor => sensor.Id,
            StringComparer.OrdinalIgnoreCase
        );

        var resolved = new ConcurrentDictionary<
            string,
            int
        >(StringComparer.OrdinalIgnoreCase);

        int progress = 0;

        await Parallel.ForEachAsync(
            profiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = concurrency,
                CancellationToken = token
            },
            async (profile, ct) =>
            {
                int sensorId;

                if (known.TryGetValue(
                    profile.MacAddress,
                    out var existingId))
                {
                    sensorId = existingId;
                }
                else
                {
                    var payload = new
                    {
                        macAddress = profile.MacAddress,
                        location = profile.Location,
                        category = profile.Category
                    };

                    using var response =
                        await http.PostAsJsonAsync(
                            "api/sensors",
                            payload,
                            ct
                        );

                    if (
                        response.StatusCode ==
                        HttpStatusCode.Created
                    )
                    {
                        var created =
                            await response.Content
                                .ReadFromJsonAsync<
                                    RegisteredSensor
                                >(cancellationToken: ct);

                        if (
                            created is null ||
                            created.Id <= 0
                        )
                        {
                            throw new InvalidOperationException(
                                "Registration returned an invalid sensor ID."
                            );
                        }

                        sensorId = created.Id;
                    }
                    else if (
                        response.StatusCode ==
                        HttpStatusCode.Conflict
                    )
                    {
                        // Another simulator may have
                        // registered the same MAC.
                        var refreshed =
                            await http.GetFromJsonAsync<
                                List<RegisteredSensor>
                            >("api/sensors", ct) ?? new();

                        var match = refreshed.FirstOrDefault(
                            sensor =>
                                string.Equals(
                                    sensor.MacAddress,
                                    profile.MacAddress,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );

                        if (match is null)
                        {
                            throw new InvalidOperationException(
                                "MAC conflict without a matching registered sensor."
                            );
                        }

                        sensorId = match.Id;
                    }
                    else
                    {
                        string body =
                            await response.Content
                                .ReadAsStringAsync(ct);

                        throw new HttpRequestException(
                            $"Registration failed: " +
                            $"{profile.MacAddress}, " +
                            $"HTTP {(int)response.StatusCode}: {body}"
                        );
                    }
                }

                resolved[profile.MacAddress] = sensorId;

                int completed =
                    Interlocked.Increment(ref progress);

                if (
                    completed % 50 == 0 ||
                    completed == profiles.Count
                )
                {
                    Console.WriteLine(
                        $"  Registered/reused: " +
                        $"{completed}/{profiles.Count}"
                    );
                }
            }
        );

        return profiles
            .Select(profile => new FleetDevice(
                new SimulatedDevice(profile),
                resolved[profile.MacAddress]
            ))
            .ToArray();
    }

    private static async Task ProduceAsync(
        ChannelWriter<SimulatedTelemetry> writer,
        FleetDevice[] fleet,
        long totalRequests,
        int targetRps,
        CancellationToken token)
    {
        var watch = Stopwatch.StartNew();

        for (long i = 0; i < totalRequests; i++)
        {
            double scheduledSeconds =
                (double)i / targetRps;

            TimeSpan delay =
                TimeSpan.FromSeconds(scheduledSeconds) -
                watch.Elapsed;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, token);
            }


            var device = fleet[(int)(i % fleet.Length)];

            var reading =
                device.Device.GenerateReading() with
                {
                    SensorId = device.SensorId
                };

            // Bounded channel applies backpressure.
            await writer.WriteAsync(reading, token);
        }
    }

    private static async Task ConsumeAsync(
        ChannelReader<SimulatedTelemetry> reader,
        HttpClient http,
        LoadCounters counters,
        CancellationToken token)
    {
        try
        {
            await foreach (var reading in
                reader.ReadAllAsync(token))
            {
                counters.RecordAttempt();

                var watch = Stopwatch.StartNew();

                try
                {
                    // No automatic retry: we want
                    // actual HTTP response statistics.
                    using var response =
                        await http.PostAsJsonAsync(
                            "api/telemetry",
                            reading,
                            token
                        );

                    watch.Stop();

                    counters.RecordResponse(
                        response.StatusCode,
                        watch.Elapsed.TotalMilliseconds
                    );
                }
                catch (OperationCanceledException)
                    when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpRequestException)
                {
                    counters.RecordError();
                }
                catch (TaskCanceledException)
                {
                    counters.RecordError();
                }
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
            // Normal operator cancellation.
        }
    }

    private static async Task ObserveAsync(
        HttpClient http,
        LoadCounters counters,
        Action<int> recordWaiting,
        CancellationToken token)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(2)
        );

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                try
                {
                    var status = await GetQueueStatusAsync(
                        http,
                        token
                    );

                    recordWaiting(status.Waiting);

                    Console.WriteLine(
                        $"[LOAD] " +
                        $"HTTP 202={counters.Accepted} | " +
                        $"429={counters.QueueFull} | " +
                        $"Queue waiting={status.Waiting} | " +
                        $"Processed={status.Processed}"
                    );
                }
                catch (OperationCanceledException)
                    when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[MONITOR] {ex.Message}"
                    );
                }
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
            // Monitoring stopped after the test.
        }
    }

    private static async Task<QueueSnapshot>
        GetQueueStatusAsync(
            HttpClient http,
            CancellationToken token)
    {
        return await http.GetFromJsonAsync<QueueSnapshot>(
            "api/telemetry/queue/status",
            token
        ) ?? throw new InvalidOperationException(
            "The API returned an empty queue status."
        );
    }

    private static double Percentile(
        double[] sortedValues,
        double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        int index = (int)Math.Ceiling(
            percentile * sortedValues.Length
        ) - 1;

        index = Math.Clamp(
            index,
            0,
            sortedValues.Length - 1
        );

        return sortedValues[index];
    }
}

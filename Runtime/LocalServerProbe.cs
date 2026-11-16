#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Unity.Services.CloudCode
{
    static class LocalServerProbe
    {
        internal enum Result
        {
            Healthy,
            NotRunning,
            TimedOut,
            Unexpected
        }

        internal static readonly TimeSpan k_Timeout = TimeSpan.FromMilliseconds(500);

        internal const string k_SettingsAssetPath = "Assets/CloudCode/CloudCodeLocalServerSettings.asset";
        const string k_YamlHeader = "%YAML";
        const string k_PortKey = "m_Port:";
        const string k_HealthPath = "/v1/lifecycle/health";
        const string k_HealthyStatus = "healthy";

        static readonly HttpClient s_HttpClient = new HttpClient(new HttpClientHandler { UseProxy = false });

        internal static ushort ReadPort(string settingsAssetPath, ushort defaultPort)
        {
            try
            {
                if (!File.Exists(settingsAssetPath))
                {
                    return defaultPort;
                }

                var assetText = File.ReadAllText(settingsAssetPath);
                if (!assetText.StartsWith(k_YamlHeader, StringComparison.Ordinal))
                {
                    UnityEngine.Debug.LogWarning(
                        $"{k_SettingsAssetPath} isn't saved as text, so Cloud Code checks for the local server on"
                        + $" port {defaultPort}. To use another port, set Asset Serialization Mode to Force Text"
                        + " in Project Settings > Editor.");
                    return defaultPort;
                }

                return ParsePort(assetText, defaultPort);
            }
            catch (IOException)
            {
                return defaultPort;
            }
            catch (UnauthorizedAccessException)
            {
                return defaultPort;
            }
        }

        internal static ushort ParsePort(string assetText, ushort defaultPort)
        {
            if (string.IsNullOrEmpty(assetText))
            {
                return defaultPort;
            }

            using var reader = new StringReader(assetText);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(k_PortKey, StringComparison.Ordinal))
                {
                    continue;
                }

                return ushort.TryParse(trimmed.Substring(k_PortKey.Length).Trim(), out var port) && port > 0
                    ? port
                    : defaultPort;
            }

            return defaultPort;
        }

        internal static Task<(Result result, string detail)> ProbeAsync(int port)
        {
            return ProbeAsync(s_HttpClient, port, k_Timeout, IsListeningAsync);
        }

        internal static async Task<(Result result, string detail)> ProbeAsync(
            HttpClient client, int port, TimeSpan timeout, Func<int, TimeSpan, Task<bool>> isListening)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            try
            {
                using var response = await client.GetAsync($"http://localhost:{port}{k_HealthPath}", cancellation.Token);
                if (!response.IsSuccessStatusCode)
                {
                    return (Result.Unexpected, $"HTTP {(int)response.StatusCode}");
                }

                var body = await response.Content.ReadAsStringAsync();
                return IsHealthy(body)
                    ? (Result.Healthy, null)
                    : (Result.Unexpected, "the health response didn't report healthy");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return await isListening(port, timeout)
                    ? (Result.TimedOut, null)
                    : (Result.NotRunning, "nothing accepted a connection");
            }
            catch (HttpRequestException e)
            {
                return (Result.NotRunning, e.Message);
            }
            catch (Exception e)
            {
                return (Result.Unexpected, e.Message);
            }
        }

        static async Task<bool> IsListeningAsync(int port, TimeSpan timeout)
        {
            var tcpClient = new TcpClient();
            try
            {
                var connect = tcpClient.ConnectAsync("localhost", port);
                if (await Task.WhenAny(connect, Task.Delay(timeout)) != connect)
                {
                    _ = connect.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    return false;
                }

                return connect.Status == TaskStatus.RanToCompletion;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                tcpClient.Dispose();
            }
        }

        static bool IsHealthy(string body)
        {
            try
            {
                return JObject.Parse(body).Value<string>("status") == k_HealthyStatus;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
#endif

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    static class LocalServerPort
    {
        static readonly TimeSpan k_ConnectTimeout = TimeSpan.FromMilliseconds(250);

        internal static string InUseMessage(int port)
        {
            return $"Port {port} is in use. Set a different port in the Cloud Code Local Server Settings asset.";
        }

        internal static async Task<bool> IsLoopbackListeningAsync(int port, CancellationToken cancellationToken)
        {
            return await IsListeningAsync(IPAddress.Loopback, port, cancellationToken)
                || await IsListeningAsync(IPAddress.IPv6Loopback, port, cancellationToken);
        }

        static async Task<bool> IsListeningAsync(IPAddress address, int port, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Socket socket;
            try
            {
                socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (SocketException)
            {
                return false;
            }

            using (socket)
            {
                var connect = socket.ConnectAsync(address, port);
                // Disposing the socket below faults an abandoned connect, and that failure must still be observed.
                _ = connect.ContinueWith(
                    t => _ = t.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);

                var timeout = Task.Delay(k_ConnectTimeout, cancellationToken);
                if (await Task.WhenAny(connect, timeout) == timeout)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return false;
                }

                try
                {
                    await connect;
                    return true;
                }
                catch (SocketException)
                {
                    return false;
                }
            }
        }
    }
}

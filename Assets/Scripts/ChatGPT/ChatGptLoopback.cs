using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dumbubu.ChatGPT
{
    // A loopback-only listener avoids Windows HTTP.sys URL reservations/admin rights.
    public sealed class ChatGptLoopback : IDisposable
    {
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        public string RedirectUri { get; }

        public ChatGptLoopback()
        {
            listener.Start();
            RedirectUri = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/auth/callback";
        }

        public async Task<Dictionary<string, string>> WaitAsync(string state, CancellationToken cancellation)
        {
            using (cancellation.Register(listener.Stop))
            {
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    TcpClient client;
                    try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                    catch (Exception) when (cancellation.IsCancellationRequested) { throw new OperationCanceledException(cancellation); }
                    using (client)
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                    using (timeout.Token.Register(client.Close))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(5));
                        try
                        {
                            var stream = client.GetStream();
                            // Bound the header, and don't accept absolute request targets or request bodies.
                            var header = new StringBuilder();
                            var one = new byte[1];
                            while (header.Length < 8192 && !header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                            {
                                if (await stream.ReadAsync(one, 0, 1, timeout.Token).ConfigureAwait(false) == 0) break;
                                header.Append((char)one[0]);
                            }
                            string[] request = header.ToString().Split('\n')[0].TrimEnd('\r').Split(' ');
                            Dictionary<string, string> query = null;
                            if (header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && request.Length == 3 && request[0] == "GET" &&
                                request[1].StartsWith("/auth/callback?", StringComparison.Ordinal))
                            {
                                query = ChatGptProtocol.ParseQuery(request[1].Substring("/auth/callback".Length));
                                if (!query.TryGetValue("state", out string returned) || returned != state) query = null;
                            }
                            string message = query == null ? "This sign-in callback is invalid. Return to Dumbubu and try again."
                                : "Sign-in received. You can close this tab and return to Dumbubu.";
                            byte[] body = Encoding.UTF8.GetBytes(message);
                            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 " + (query == null ? "400 Bad Request" : "200 OK") +
                                "\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: " + body.Length + "\r\n\r\n");
                            await stream.WriteAsync(response, 0, response.Length, timeout.Token).ConfigureAwait(false);
                            await stream.WriteAsync(body, 0, body.Length, timeout.Token).ConfigureAwait(false);
                            if (query != null) return query;
                        }
                        catch (Exception error) when (error is IOException || error is SocketException || error is OperationCanceledException || error is ChatGptException || error is ObjectDisposedException)
                        {
                            cancellation.ThrowIfCancellationRequested();
                        }
                    }
                }
            }
        }

        public void Dispose() { listener.Stop(); }
    }
}

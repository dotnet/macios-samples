// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HandlerSample;

sealed class ProxyCounts {
	public int Anonymous;
	public int Authenticated;
}

// Deliberately limited to this sample's HTTP GET and HTTPS CONNECT endpoints.
static class SampleProxy {
	public static async Task RunAsync (IPAddress address, int port, bool authenticate, Settings settings, ProxyCounts counts, CancellationToken cancellationToken)
	{
		var listener = new TcpListener (address, port);
		listener.Start (16);
		try {
			while (!cancellationToken.IsCancellationRequested) {
				using var client = await listener.AcceptTcpClientAsync (cancellationToken);
				using var timeout = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
				timeout.CancelAfter (TimeSpan.FromSeconds (45));
				try {
					await HandleAsync (client.GetStream (), authenticate, settings, counts, timeout.Token);
				} catch (Exception error) when (error is IOException or SocketException or InvalidDataException or OperationCanceledException) {
					if (!cancellationToken.IsCancellationRequested)
						Console.Error.WriteLine ($"Development proxy: {error.GetType ().Name}: {error.Message}");
				}
			}
		} catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
		} finally {
			listener.Stop ();
		}
	}

	static async Task HandleAsync (NetworkStream incoming, bool authenticate, Settings settings, ProxyCounts counts, CancellationToken token)
	{
		var header = new List<byte> ();
		var one = new byte [1];
		while (header.Count < 16384) {
			if (await incoming.ReadAsync (one, token) == 0)
				throw new IOException ("The proxy client closed before completing its headers.");
			header.Add (one [0]);
			if (header.Count >= 4 && header.TakeLast (4).SequenceEqual (new byte [] { 13, 10, 13, 10 }))
				break;
		}
		if (header.Count == 16384)
			throw new InvalidDataException ("Proxy headers exceed the sample limit.");
		var lines = Encoding.ASCII.GetString (header.ToArray ()).Split ("\r\n");
		var request = lines [0].Split (' ');
		if (request.Length != 3 || request [0] is not ("GET" or "CONNECT")) {
			await ReplyAsync (incoming, "405 Method Not Allowed", "", token);
			return;
		}
		var connect = request [0] == "CONNECT";
		if (!Uri.TryCreate (connect ? "https://" + request [1] : request [1], UriKind.Absolute, out var target)
			|| !string.Equals (target.Host, settings.Host, StringComparison.OrdinalIgnoreCase)
			|| target.UserInfo.Length != 0
			|| (connect ? target.Port != 5443 || target.Scheme != "https" : target.Port != 5080 || target.Scheme != "http")) {
			await ReplyAsync (incoming, "403 Forbidden", "", token);
			return;
		}
		var expected = "Basic " + Convert.ToBase64String (Encoding.UTF8.GetBytes ("sample:" + settings.Password));
		var authorization = lines.FirstOrDefault (line => line.StartsWith ("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase));
		if (authenticate && authorization? ["Proxy-Authorization:".Length..].Trim () != expected) {
			await ReplyAsync (incoming, "407 Proxy Authentication Required", "Proxy-Authenticate: Basic realm=\"handler-sample\"\r\n", token);
			return;
		}
		if (authenticate)
			Interlocked.Increment (ref counts.Authenticated);
		else
			Interlocked.Increment (ref counts.Anonymous);
		using var outgoing = new TcpClient ();
		await outgoing.ConnectAsync (IPAddress.Loopback, target.Port, token);
		using var remote = outgoing.GetStream ();
		if (connect) {
			await incoming.WriteAsync (Encoding.ASCII.GetBytes ("HTTP/1.1 200 Connection Established\r\n\r\n"), token);
			using var tunnel = CancellationTokenSource.CreateLinkedTokenSource (token);
			var upstream = incoming.CopyToAsync (remote, tunnel.Token);
			var downstream = remote.CopyToAsync (incoming, tunnel.Token);
			await Task.WhenAny (upstream, downstream);
			tunnel.Cancel ();
			try {
				await Task.WhenAll (upstream, downstream);
			} catch (OperationCanceledException) when (tunnel.IsCancellationRequested) {
			}
		} else {
			var forwarded = $"GET {target.PathAndQuery} HTTP/1.1\r\nHost: {target.Authority}\r\nVia: 1.1 handler-sample\r\nConnection: close\r\n\r\n";
			await remote.WriteAsync (Encoding.ASCII.GetBytes (forwarded), token);
			await remote.CopyToAsync (incoming, token);
		}
	}

	static Task ReplyAsync (NetworkStream stream, string status, string headers, CancellationToken token) =>
		stream.WriteAsync (Encoding.ASCII.GetBytes (
			$"HTTP/1.1 {status}\r\n{headers}Content-Length: 0\r\nConnection: close\r\n\r\n"), token).AsTask ();
}

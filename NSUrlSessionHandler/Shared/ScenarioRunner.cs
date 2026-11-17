// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Foundation;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace HandlerSample;

public sealed record ScenarioResult (string Name, string Outcome, string Detail) {
	public override string ToString () => $"{Outcome}: {Name}\n{Detail}\n";
}

public static class ScenarioRunner {
	public static string Summary (IReadOnlyCollection<ScenarioResult> results) =>
		$"Results: {results.Count (result => result.Outcome == "PASS")} passed, "
		+ $"{results.Count (result => result.Outcome == "FAIL")} failed, "
		+ $"{results.Count (result => result.Outcome == "LEGACY")} legacy, "
		+ $"{results.Count (result => result.Outcome == "UNAVAILABLE")} unavailable.";

	public static async Task<List<ScenarioResult>> RunAsync (Action<ScenarioResult> report)
	{
		var resources = NSBundle.MainBundle.ResourcePath;
		var settings = Settings.Load (resources);
		using var trust = new NativeTrust (settings, resources);
		var results = new List<ScenarioResult> ();

		async Task Run (string name, Func<Task> scenario, Func<string?>? compatibilityObservation = null)
		{
			ScenarioResult result;
			try {
				await scenario ();
				var legacy = compatibilityObservation?.Invoke ();
				result = new (name, legacy is null ? "PASS" : "LEGACY", legacy ?? "Observed the expected result.");
			} catch (ScenarioUnavailableException error) {
				result = new (name, "UNAVAILABLE", error.Message);
			} catch (Exception error) when (error is HttpRequestException or InvalidOperationException or IOException
				or OperationCanceledException or JsonException) {
				result = new (name, "FAIL", $"{error.GetType ().Name}: {error.Message}");
			}
			results.Add (result);
			report (result);
		}

		await Run ("Fixture readiness (not a certificate test)", async () => {
			using var client = Client (trust.CreateHandler ());
			Equal ("NSUrlSessionHandler local fixtures", await client.GetStringAsync (settings.Endpoint (5443)), "server response");
		});
		await Run ("Platform default rejects private CA", async () => {
			using var client = Client (trust.CreateHandler ("default"));
			await Rejected (client, settings.Endpoint (5443));
		});
		foreach (var (port, name) in new [] { (5443, "valid"), (5445, "expired"), (5446, "wrong host"), (5447, "unrelated CA") }) {
			await Run ($"Native trust: {name}", async () => {
				var rejected = false;
				using var client = Client (trust.CreateHandler (observeRejection: () => rejected = true));
				if (port == 5443)
					Equal ("NSUrlSessionHandler local fixtures", await client.GetStringAsync (settings.Endpoint (port)), "trusted response");
				else
					await Rejected (client, settings.Endpoint (port), () => rejected);
			});
		}
		foreach (var (port, pins, accepted, name) in new [] {
			(5443, new [] { settings.PrimaryPin }, true, "primary"),
			(5444, new [] { settings.PrimaryPin, settings.BackupPin }, true, "backup"),
			(5443, new [] { settings.BackupPin }, false, "mismatch"),
		}) {
			await Run ($"Certificate pin: {name}", async () => {
				var rejected = false;
				using var client = Client (trust.CreateHandler (pins: new HashSet<string> (pins, StringComparer.Ordinal), observeRejection: () => rejected = true));
				if (accepted)
					Equal ("NSUrlSessionHandler local fixtures", await client.GetStringAsync (settings.Endpoint (port)), "pinned response");
				else
					await Rejected (client, settings.Endpoint (port), () => rejected);
			});
		}
		foreach (var validation in new [] { "managed", "managed-reject", "bridge" }) {
			await Run ($"Certificate validation: {validation}", async () => {
				var calls = 0;
				var rejected = false;
				using var client = Client (trust.CreateHandler (validation, observeManaged: errors => Interlocked.Increment (ref calls), observeRejection: () => rejected = true));
				if (validation == "managed-reject")
					await Rejected (client, settings.Endpoint (5443), () => rejected);
				else
					Equal ("NSUrlSessionHandler local fixtures", await client.GetStringAsync (settings.Endpoint (5443)), "validated response");
				if (validation.StartsWith ("managed", StringComparison.Ordinal))
					Equal (1, calls, "managed callback count");
			});
		}
		await Run ("Native-to-managed bridge rejects wrong host", async () => {
			var rejected = false;
			using var client = Client (trust.CreateHandler ("bridge", observeRejection: () => rejected = true));
			await Rejected (client, settings.Endpoint (5446), () => rejected);
		});
		await Run ("Native callback takes precedence", async () => {
			var calls = 0;
			using var handler = trust.CreateHandler ();
			handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => {
				Interlocked.Increment (ref calls);
				return false;
			};
			using var client = Client (handler);
			await client.GetStringAsync (settings.Endpoint (5443));
			Equal (0, calls, "ignored managed callback count");
		});
		await Run ("Required client identity", async () => {
			var storage = OperatingSystem.IsMacOS () ? X509KeyStorageFlags.Exportable : X509KeyStorageFlags.DefaultKeySet;
			using var identity = X509CertificateLoader.LoadPkcs12FromFile (Path.Combine (resources, "client.pfx"), settings.Password, storage);
			using var handler = trust.CreateHandler ();
			handler.ClientCertificateOptions = ClientCertificateOption.Manual;
			handler.ClientCertificates.Add (identity);
			using var client = Client (handler);
			Equal ("client certificate supplied", await client.GetStringAsync (settings.Endpoint (5448, "/identity")), "mTLS response");
		});
		string? legacyIdentity = null;
		await Run ("Required client identity missing", async () => {
			using var client = Client (trust.CreateHandler ());
			try {
				await Rejected (client, settings.Endpoint (5448, "/identity"));
			} catch (HttpRequestException error) when (error.InnerException is NSErrorException native
				&& native.Error.Domain == NSError.NSUrlErrorDomain && (NSUrlError) (long) native.Error.Code == NSUrlError.NetworkConnectionLost) {
				legacyIdentity = "The server closed the connection without a client identity, but this workload did not report an explicit TLS authentication failure.";
			}
		}, () => legacyIdentity);
		await Run ("Optional client identity missing", async () => {
			using var client = Client (trust.CreateHandler ());
			Equal ("optional client certificate omitted", await client.GetStringAsync (settings.Endpoint (5449, "/identity")), "optional mTLS");
		});
		foreach (var (path, cacheTarget, status, name) in new [] {
			("/auth", false, HttpStatusCode.OK, "Basic authentication"),
			("/redirect", false, HttpStatusCode.Unauthorized, "same-origin redirect drops broad credentials"),
			("/redirect?cross=1", false, HttpStatusCode.Unauthorized, "cross-origin redirect drops broad credentials"),
			("/redirect?cross=1", true, HttpStatusCode.OK, "redirect uses explicitly scoped target credentials"),
		}) {
			string? legacy = null;
			await Run (name, async () => {
				using var handler = trust.CreateHandler ();
				handler.AllowAutoRedirect = true;
				if (cacheTarget) {
					var credentials = new CredentialCache ();
					credentials.Add (settings.Endpoint (5444), "Basic", new NetworkCredential ("sample", settings.Password));
					handler.Credentials = credentials;
				} else {
					handler.Credentials = new NetworkCredential ("sample", settings.Password);
				}
				using var client = Client (handler);
				using var response = await client.GetAsync (new Uri (settings.Endpoint (5443), path));
				if (response.StatusCode != status && path.StartsWith ("/redirect", StringComparison.Ordinal)
					&& response.StatusCode == (cacheTarget ? HttpStatusCode.Unauthorized : HttpStatusCode.OK))
					legacy = $"Observed {(int) response.StatusCode}: this workload has the older redirect credential behavior. Use a servicing release containing #25529 for the current behavior.";
				else
					Equal (status, response.StatusCode, "authentication status");
			}, () => legacy);
		}
		await Run ("Cookies round trip and synchronize managed container", async () => {
			using var handler = trust.CreateHandler ();
			handler.CookieContainer = new CookieContainer ();
			using var client = Client (handler);
			await client.GetStringAsync (settings.Endpoint (5443, "/cookie"));
			using var echo = await Json (client, settings.Endpoint (5443, "/echo"));
			Equal ("roundtrip", echo.RootElement.GetProperty ("cookie").GetString (), "echoed cookie");
			Equal ("roundtrip", handler.CookieContainer.GetCookies (settings.Endpoint (5443)) ["sample"]?.Value, "managed cookie");
		});
		await Run ("Ephemeral sessions isolate cookies", async () => {
			using var first = Client (trust.CreateHandler ());
			await first.GetStringAsync (settings.Endpoint (5443, "/cookie"));
			using var second = Client (trust.CreateHandler ());
			using var echo = await Json (second, settings.Endpoint (5443, "/echo"));
			Equal (JsonValueKind.Null, echo.RootElement.GetProperty ("cookie").ValueKind, "isolated cookie");
		});
		await Run ("Cookies disabled", async () => {
			using var handler = trust.CreateHandler ();
			handler.UseCookies = false;
			using var client = Client (handler);
			await client.GetStringAsync (settings.Endpoint (5443, "/cookie"));
			using var echo = await Json (client, settings.Endpoint (5443, "/echo"));
			Equal (JsonValueKind.Null, echo.RootElement.GetProperty ("cookie").ValueKind, "disabled cookie");
		});
		foreach (var (size, unknown) in new [] { (1024, false), (1025, false), (1024, true) }) {
			await Run ($"Upload: {size} bytes, unknown length = {unknown}", async () => {
				using var handler = trust.CreateHandler ();
				handler.MaxInputInMemory = 1024;
				using var client = Client (handler);
				var bytes = Encoding.UTF8.GetBytes (new string ('x', size));
				using var stream = new NonSeekableStream (bytes);
				using HttpContent content = unknown ? new StreamContent (stream) : new ByteArrayContent (bytes);
				using var response = await client.PostAsync (settings.Endpoint (5443, "/upload"), content);
				response.EnsureSuccessStatusCode ();
				using var json = JsonDocument.Parse (await response.Content.ReadAsStringAsync ());
				Equal (size, json.RootElement.GetProperty ("bytes").GetInt32 (), "uploaded byte count");
				Equal (Convert.ToHexString (SHA256.HashData (bytes)), json.RootElement.GetProperty ("hash").GetString (), "uploaded SHA-256");
				var length = json.RootElement.GetProperty ("length");
				if (size <= 1024 && !unknown)
					Equal (size, length.GetInt32 (), "buffered Content-Length at threshold");
				else
					Equal (JsonValueKind.Null, length.ValueKind, "streamed Content-Length");
			});
		}
		string? legacyHeaders = null;
		await Run ("Native decompression and headers", async () => {
			using var client = Client (trust.CreateHandler ());
			using var response = await client.GetAsync (settings.Endpoint (5443, "/compressed"));
			response.EnsureSuccessStatusCode ();
			Equal ("automatically decompressed", await response.Content.ReadAsStringAsync (), "decoded body");
			if (response.Content.Headers.ContentEncoding.SequenceEqual (new [] { "gzip" }))
				legacyHeaders = "The body was decoded, but this workload retains compressed headers. Use a servicing release containing #24924 for header cleanup.";
			else {
				Equal (false, response.Content.Headers.ContentEncoding.Any (), "stale Content-Encoding");
				Equal (false, response.Content.Headers.Contains ("Content-Length"), "stale Content-Length");
			}
		}, () => legacyHeaders);
		foreach (var (port, authenticate, https, expected) in new [] {
			(5888, false, false, HttpStatusCode.OK),
			(5888, false, true, HttpStatusCode.OK),
			(5889, true, true, HttpStatusCode.OK),
			(5889, true, false, HttpStatusCode.ProxyAuthenticationRequired),
		}) {
			await Run ($"Proxy: {port}, HTTPS = {https}", async () => {
				using var handler = trust.CreateHandler ();
				if (!handler.SupportsProxy)
					throw new ScenarioUnavailableException ("Install an Apple workload servicing release with custom proxy support.");
				handler.UseProxy = true;
				handler.Proxy = new WebProxy (new UriBuilder ("http", settings.Host, port).Uri);
				if (authenticate)
					handler.DefaultProxyCredentials = new NetworkCredential ("sample", settings.Password);
				using var observer = Client (trust.CreateHandler ());
				using var before = await Json (observer, settings.Endpoint (5443, "/proxy-counts"));
				using var client = Client (handler);
				var uri = https ? settings.Endpoint (5443, "/echo") : new UriBuilder ("http", settings.Host, 5080, "/echo").Uri;
				using var response = await client.GetAsync (uri);
				Equal (expected, response.StatusCode, "proxied response status");
				if (expected == HttpStatusCode.OK) {
					using var after = await Json (observer, settings.Endpoint (5443, "/proxy-counts"));
					var key = authenticate ? "authenticated" : "anonymous";
					Equal (true, after.RootElement.GetProperty (key).GetInt32 () > before.RootElement.GetProperty (key).GetInt32 (), "proxy observed request");
				}
			});
		}
		foreach (var useProxy in new [] { false, true }) {
			await Run (useProxy ? "Proxy bypass" : "Proxy disabled", async () => {
				using var handler = trust.CreateHandler ();
				if (!handler.SupportsProxy)
					throw new ScenarioUnavailableException ("Install an Apple workload servicing release with custom proxy support.");
				handler.UseProxy = useProxy;
				handler.Proxy = new WebProxy ("http://127.0.0.1:1") {
					BypassList = [$"^https?://{RegexEscape (settings.Host)}(?::[0-9]+)?(?:/|$)"],
				};
				using var client = Client (handler);
				await client.GetStringAsync (settings.Endpoint (5443));
			});
		}
		await Run ("Cancellation is distinct from trust failure", async () => {
			using var client = Client (trust.CreateHandler ());
			using var cancellation = new CancellationTokenSource (TimeSpan.FromMilliseconds (300));
			try {
				await client.GetStringAsync (settings.Endpoint (5443, "/delay"), cancellation.Token);
				throw new InvalidOperationException ("The delayed request unexpectedly completed.");
			} catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
			}
		});
		return results;
	}

	static string RegexEscape (string value) => System.Text.RegularExpressions.Regex.Escape (value);
	static HttpClient Client (NSUrlSessionHandler handler) => new (handler) { Timeout = TimeSpan.FromSeconds (20) };
	static async Task<JsonDocument> Json (HttpClient client, Uri uri) => JsonDocument.Parse (await client.GetStringAsync (uri));

	static async Task Rejected (HttpClient client, Uri uri, Func<bool>? validationRejected = null)
	{
		try {
			using var response = await client.GetAsync (uri);
		} catch (HttpRequestException error) when (validationRejected?.Invoke () == true || error.InnerException is AuthenticationException
			|| (error.InnerException is NSErrorException native
				&& (native.Error.Domain == NSError.NSUrlErrorDomain || native.Error.Domain == NSError.CFNetworkErrorDomain)
				&& (long) native.Error.Code is >= -1206 and <= -1200)) {
			return;
		}
		throw new InvalidOperationException ("The request reached HTTP instead of failing TLS authentication.");
	}

	static void Equal<T> (T expected, T actual, string description)
	{
		if (!EqualityComparer<T>.Default.Equals (expected, actual))
			throw new InvalidOperationException ($"{description}: expected '{expected}', received '{actual}'.");
	}

	sealed class ScenarioUnavailableException (string message) : Exception (message);

	sealed class NonSeekableStream (byte [] bytes) : MemoryStream (bytes) {
		public override bool CanSeek => false;
	}
}

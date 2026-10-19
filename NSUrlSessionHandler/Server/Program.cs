// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HandlerSample;
using Microsoft.AspNetCore.Server.Kestrel.Https;

var directory = Path.GetFullPath ("Development");
var hostArgument = args.SingleOrDefault (argument => argument.StartsWith ("--host=", StringComparison.Ordinal));
if (args.Contains ("--setup")) {
	try {
		Certificates.Generate (directory, hostArgument is null ? "localhost" : hostArgument ["--host=".Length..]);
	} catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException) {
		Console.Error.WriteLine ($"Fixture setup failed: {error.Message}");
		Environment.ExitCode = 1;
	}
	return;
}
var settings = Settings.Load (Path.Combine (directory, "Public"));
using var root = X509CertificateLoader.LoadCertificateFromFile (Path.Combine (directory, "Public", "root.cer"));
var certificates = new List<X509Certificate2> ();
var builder = WebApplication.CreateBuilder ();
builder.Logging.SetMinimumLevel (LogLevel.Warning);
var address = args.Contains ("--lan") ? IPAddress.Any : IPAddress.Loopback;
builder.WebHost.ConfigureKestrel (options => {
	options.Limits.MaxRequestBodySize = 1024 * 1024;
	options.Listen (address, 5080);
	foreach (var (port, name) in new [] {
		(5443, "primary"), (5444, "backup"), (5445, "expired"), (5446, "wrong-host"),
		(5447, "untrusted"), (5448, "primary"), (5449, "primary"),
	}) {
		var certificate = X509CertificateLoader.LoadPkcs12FromFile (Path.Combine (directory, name + ".pfx"), settings.Password);
		certificates.Add (certificate);
		options.Listen (address, port, listener => listener.UseHttps (https => {
			https.ServerCertificate = certificate;
			if (port is 5448 or 5449) {
				https.ClientCertificateMode = port == 5448 ? ClientCertificateMode.RequireCertificate : ClientCertificateMode.AllowCertificate;
				https.ClientCertificateValidation = (client, _, _) => {
					using var chain = new X509Chain ();
					chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
					chain.ChainPolicy.CustomTrustStore.Add (root);
					chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
					chain.ChainPolicy.ApplicationPolicy.Add (new Oid ("1.3.6.1.5.5.7.3.2"));
					return chain.Build (client);
				};
			}
		}));
	}
});
await using var app = builder.Build ();
var counts = new ProxyCounts ();
app.MapGet ("/", () => Results.Text ("NSUrlSessionHandler local fixtures"));
app.MapGet ("/echo", (HttpContext context) => Results.Json (
	new EchoResponse (context.Request.Cookies ["sample"], context.Request.Headers.Via.ToString ()),
	ServerJsonContext.Default.EchoResponse));
app.MapGet ("/cookie", (HttpContext context) => {
	context.Response.Cookies.Append ("sample", "roundtrip", new CookieOptions { Secure = true, HttpOnly = true });
	return Results.Text ("cookie set");
});
app.MapGet ("/auth", (HttpContext context) => {
	if (context.Request.Headers.Authorization != "Basic " + Convert.ToBase64String (Encoding.UTF8.GetBytes ("sample:" + settings.Password))) {
		context.Response.Headers.WWWAuthenticate = "Basic realm=\"handler-sample\"";
		return Results.Unauthorized ();
	}
	return Results.Text ("authenticated");
});
app.MapGet ("/redirect", (HttpContext context) => Results.Redirect (
	settings.Endpoint (context.Request.Query.ContainsKey ("cross") ? 5444 : 5443, "/auth").AbsoluteUri));
app.MapGet ("/identity", (HttpContext context) =>
	context.Connection.ClientCertificate is not null
		? Results.Text ("client certificate supplied")
		: context.Connection.LocalPort == 5448
			? Results.Unauthorized () : Results.Text ("optional client certificate omitted"));
app.MapPost ("/upload", async (HttpContext context) => {
	using var content = new MemoryStream ();
	await context.Request.Body.CopyToAsync (content, context.RequestAborted);
	return Results.Json (
		new UploadResponse (context.Request.ContentLength, content.Length, Convert.ToHexString (SHA256.HashData (content.ToArray ()))),
		ServerJsonContext.Default.UploadResponse);
});
app.MapGet ("/compressed", async (HttpContext context) => {
	var bytes = Encoding.UTF8.GetBytes ("automatically decompressed");
	using var buffer = new MemoryStream ();
	using (var gzip = new GZipStream (buffer, CompressionLevel.Fastest, leaveOpen: true))
		gzip.Write (bytes);
	context.Response.Headers.ContentEncoding = "gzip";
	context.Response.ContentLength = buffer.Length;
	await context.Response.Body.WriteAsync (buffer.ToArray (), context.RequestAborted);
});
app.MapGet ("/delay", async (HttpContext context) => {
	await Task.Delay (TimeSpan.FromSeconds (30), context.RequestAborted);
	return Results.Text ("finished");
});
app.MapGet ("/proxy-counts", () => Results.Json (
	new ProxyResponse (counts.Anonymous, counts.Authenticated), ServerJsonContext.Default.ProxyResponse));
Console.WriteLine ($"Development fixtures: https://{settings.Host}:5443 (binding {address}). Press Ctrl+C to stop.");
var services = new [] {
	app.RunAsync (),
	SampleProxy.RunAsync (address, 5888, false, settings, counts, app.Lifetime.ApplicationStopping),
	SampleProxy.RunAsync (address, 5889, true, settings, counts, app.Lifetime.ApplicationStopping),
};
try {
	await await Task.WhenAny (services);
} finally {
	await app.StopAsync ();
	try {
		await Task.WhenAll (services);
	} finally {
		foreach (var certificate in certificates)
			certificate.Dispose ();
	}
}

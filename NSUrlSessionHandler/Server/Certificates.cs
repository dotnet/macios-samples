// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace HandlerSample;

static class Certificates {
	public static void Generate (string directory, string host)
	{
		if (Uri.CheckHostName (host) == UriHostNameType.Unknown)
			throw new ArgumentException ("Use a DNS name or IP address for --host.");
		if (Directory.Exists (directory))
			throw new IOException ("Development already exists. Stop the server and remove that specific directory before generating new fixtures.");
		if (OperatingSystem.IsWindows ())
			Directory.CreateDirectory (directory);
		else
			Directory.CreateDirectory (directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		Directory.CreateDirectory (Path.Combine (directory, "Public"));
		var password = Convert.ToHexString (RandomNumberGenerator.GetBytes (24));
		using var root = Root ("NSUrlSessionHandler development CA");
		using var unrelated = Root ("Unrelated development CA");
		using var primary = Leaf (root, host);
		using var backup = Leaf (root, host);
		using var expired = Leaf (root, host, expired: true);
		using var wrongHost = Leaf (root, "wrong-host.invalid");
		using var untrusted = Leaf (unrelated, host);
		using var client = Leaf (root, "sample-client", client: true);
		File.WriteAllBytes (Path.Combine (directory, "Public", "root.cer"), root.RawData);
		File.WriteAllText (Path.Combine (directory, "Public", "root.pem"), root.ExportCertificatePem () + "\n");
		File.WriteAllBytes (Path.Combine (directory, "Public", "client.pfx"), client.Export (X509ContentType.Pfx, password));
		foreach (var (name, certificate) in new [] {
			("primary", primary), ("backup", backup), ("expired", expired),
			("wrong-host", wrongHost), ("untrusted", untrusted),
		})
			File.WriteAllBytes (Path.Combine (directory, name + ".pfx"), certificate.Export (X509ContentType.Pfx, password));
		var settings = new Settings (host, password, Pin (primary), Pin (backup));
		File.WriteAllText (Path.Combine (directory, "Public", "settings.json"),
			JsonSerializer.Serialize (settings, SampleJsonContext.Default.Settings) + "\n");
		Console.WriteLine ($"Generated DEVELOPMENT-ONLY certificates in {directory}. No global trust settings were changed.");
	}

	static string Pin (X509Certificate2 certificate) =>
		Convert.ToBase64String (SHA256.HashData (certificate.RawData));

	static X509Certificate2 Root (string name)
	{
		using var key = RSA.Create (2048);
		var request = new CertificateRequest ($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add (new X509BasicConstraintsExtension (true, false, 0, true));
		request.CertificateExtensions.Add (new X509KeyUsageExtension (X509KeyUsageFlags.KeyCertSign, true));
		request.CertificateExtensions.Add (new X509SubjectKeyIdentifierExtension (request.PublicKey, false));
		return request.CreateSelfSigned (DateTimeOffset.UtcNow.AddDays (-7), DateTimeOffset.UtcNow.AddDays (30));
	}

	static X509Certificate2 Leaf (X509Certificate2 issuer, string host, bool expired = false, bool client = false)
	{
		using var key = RSA.Create (2048);
		var request = new CertificateRequest ("CN=NSUrlSessionHandler development fixture", key,
			HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add (new X509BasicConstraintsExtension (false, false, 0, true));
		request.CertificateExtensions.Add (new X509SubjectKeyIdentifierExtension (request.PublicKey, false));
		request.CertificateExtensions.Add (X509AuthorityKeyIdentifierExtension.CreateFromCertificate (issuer, includeKeyIdentifier: true, includeIssuerAndSerial: false));
		request.CertificateExtensions.Add (new X509KeyUsageExtension (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
		request.CertificateExtensions.Add (new X509EnhancedKeyUsageExtension (
			new OidCollection { new Oid (client ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1") }, true));
		if (!client) {
			var san = new SubjectAlternativeNameBuilder ();
			if (IPAddress.TryParse (host, out var address))
				san.AddIpAddress (address);
			else
				san.AddDnsName (host);
			request.CertificateExtensions.Add (san.Build ());
		}
		using var signed = request.Create (issuer, DateTimeOffset.UtcNow.AddDays (-3),
			DateTimeOffset.UtcNow.AddDays (expired ? -1 : 7), RandomNumberGenerator.GetBytes (16));
		return signed.CopyWithPrivateKey (key);
	}
}

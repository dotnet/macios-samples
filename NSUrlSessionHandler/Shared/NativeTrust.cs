// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Foundation;
using Security;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HandlerSample;

sealed class NativeTrust : IDisposable {
	readonly Settings settings;
	readonly SecCertificate root;
	readonly X509Certificate2 managedRoot;

	public NativeTrust (Settings settings, string resources)
	{
		this.settings = settings;
		using var data = NSData.FromFile (Path.Combine (resources, "root.cer"));
		root = new SecCertificate (data);
		managedRoot = X509CertificateLoader.LoadCertificateFromFile (Path.Combine (resources, "root.cer"));
	}

	public NSUrlSessionHandler CreateHandler (string validation = "native", IReadOnlySet<string>? pins = null,
		Action<SslPolicyErrors>? observeManaged = null, Action? observeRejection = null)
	{
		bool Decide (bool accepted)
		{
			if (!accepted)
				observeRejection?.Invoke ();
			return accepted;
		}
		using var configuration = NSUrlSessionConfiguration.EphemeralSessionConfiguration;
		configuration.URLCredentialStorage = null;
		configuration.TimeoutIntervalForRequest = 15;
		using var noProxy = new NSDictionary ();
		configuration.ConnectionProxyDictionary = noProxy;
		var handler = new NSUrlSessionHandler (configuration) {
			AllowAutoRedirect = false,
			DisableCaching = true,
		};
		if (handler.SupportsProxy)
			handler.UseProxy = false;
		if (validation == "default")
			return handler;
		if (validation == "managed" || validation == "managed-reject") {
			handler.CertificateChainPolicy = DevelopmentPolicy ();
			handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => {
				observeManaged?.Invoke (errors);
				if (validation == "managed-reject" || request.RequestUri is null || certificate is null || chain is null
					|| !IsFixtureOrigin (request.RequestUri))
					return Decide (false);
				using var policy = SecPolicy.CreateSslPolicy (true, settings.Host);
				using var trust = new SecTrust (certificate, policy);
				return Decide (Evaluate (trust) && chain.Build (certificate));
			};
		} else {
			handler.TrustOverrideForUrl = (sender, url, trust) => {
				if (!Uri.TryCreate (url, UriKind.Absolute, out var uri) || !IsFixtureOrigin (uri) || !Evaluate (trust))
					return Decide (false);
				if (validation == "bridge" && !ValidateManagedChain (trust))
					return Decide (false);
				return Decide (pins is null || HasPin (trust, pins));
			};
		}
		return handler;
	}

	bool IsFixtureOrigin (Uri uri) =>
		uri.Scheme == "https" && string.Equals (uri.IdnHost, settings.Host, StringComparison.OrdinalIgnoreCase)
		&& uri.Port >= 5443 && uri.Port <= 5449;

	bool Evaluate (SecTrust trust)
	{
		var status = trust.SetAnchorCertificates ([root]);
		if (status == SecStatusCode.Success)
			status = trust.SetAnchorCertificatesOnly (true);
		if (status != SecStatusCode.Success) {
			Console.Error.WriteLine ($"Native trust setup failed: {status}");
			return false;
		}
		var result = trust.Evaluate (out var error);
		using (error)
			return result;
	}

	X509ChainPolicy DevelopmentPolicy ()
	{
		var policy = new X509ChainPolicy {
			TrustMode = X509ChainTrustMode.CustomRootTrust,
			RevocationMode = X509RevocationMode.NoCheck,
		};
		policy.CustomTrustStore.Add (managedRoot);
		policy.ApplicationPolicy.Add (new Oid ("1.3.6.1.5.5.7.3.1"));
		return policy;
	}

	bool ValidateManagedChain (SecTrust trust)
	{
		var native = trust.GetCertificateChain ();
		if (native is null)
			return false;
		var certificates = new List<X509Certificate2> ();
		try {
			foreach (var certificate in native)
				certificates.Add (certificate.ToX509Certificate2 ());
			if (certificates.Count == 0)
				return false;
			using var chain = new X509Chain { ChainPolicy = DevelopmentPolicy () };
			chain.ChainPolicy.ExtraStore.AddRange (certificates.ToArray ());
			return chain.Build (certificates [0]);
		} finally {
			foreach (var certificate in certificates)
				certificate.Dispose ();
			foreach (var certificate in native)
				certificate.Dispose ();
		}
	}

	static bool HasPin (SecTrust trust, IReadOnlySet<string> pins)
	{
		var certificates = trust.GetCertificateChain ();
		if (certificates is null)
			return false;
		try {
			if (certificates.Length == 0)
				return false;
			using var data = certificates [0].DerData;
			return pins.Contains (Convert.ToBase64String (SHA256.HashData (data.ToArray ())));
		} finally {
			foreach (var certificate in certificates)
				certificate.Dispose ();
		}
	}

	public void Dispose ()
	{
		root.Dispose ();
		managedRoot.Dispose ();
	}
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace PurchaseSample.Receipts;

public sealed class ReceiptValidator {
	readonly X509Certificate2 root;

	public ReceiptValidator (X509Certificate2 appleRoot)
	{
		root = appleRoot;
	}

	public bool Validate (byte [] receipt, string bundleId, string version, byte [] deviceId, string productId, string? transactionId = null)
	{
		if (receipt.Length == 0 || receipt.Length > 4 * 1024 * 1024)
			throw new CryptographicException ("The receipt is empty or too large.");
		var cms = new SignedCms ();
		cms.Decode (receipt);
		if (cms.Detached || cms.SignerInfos.Count != 1)
			throw new CryptographicException ("The receipt must have exactly one attached signature.");
		cms.CheckSignature (verifySignatureOnly: true);
		var attributes = Parse (cms.ContentInfo.Content);
		var created = Date (Required (attributes, 12));
		var signer = cms.SignerInfos [0].Certificate;
		if (signer is null)
			throw new CryptographicException ("The receipt signer certificate is missing.");

		using (var chain = new X509Chain ()) {
			chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
			chain.ChainPolicy.CustomTrustStore.Add (root);
			chain.ChainPolicy.ExtraStore.AddRange (cms.Certificates);
			chain.ChainPolicy.VerificationTime = created.UtcDateTime;
			// A receipt is verified as of its creation, not against today's revocation state.
			chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
			chain.ChainPolicy.DisableCertificateDownloads = true;
			if (!chain.Build (signer))
				throw new CryptographicException ("The receipt signature does not chain to the trusted Apple root at its creation date.");
		}

		var encodedBundle = Required (attributes, 2);
		if (Text (encodedBundle) != bundleId || Text (Required (attributes, 3)) != version)
			throw new CryptographicException ("The receipt belongs to a different app or app version.");
		var opaque = Required (attributes, 4);
		var expectedHash = Required (attributes, 5);
		if (deviceId.Length != 6 || expectedHash.Length != 20)
			throw new CryptographicException ("The receipt device identifier or hash has an invalid length.");
		var input = new byte [deviceId.Length + opaque.Length + encodedBundle.Length];
		deviceId.CopyTo (input, 0);
		opaque.CopyTo (input, deviceId.Length);
		encodedBundle.CopyTo (input, deviceId.Length + opaque.Length);
		// SHA-1 is required by Apple's receipt format, not used as a signature algorithm here.
		if (!CryptographicOperations.FixedTimeEquals (SHA1.HashData (input), expectedHash))
			throw new CryptographicException ("The receipt belongs to a different device.");
		if (attributes.TryGetValue (21, out var expires) && Date (expires.Single ()) <= DateTimeOffset.UtcNow)
			throw new CryptographicException ("The app receipt has expired.");

		if (!attributes.TryGetValue (17, out var purchases))
			return false;
		var entries = purchases.Select (Parse).ToArray ();
		var cancelled = entries.Where (v => v.ContainsKey (1712)).Select (v => Text (Required (v, 1705))).ToHashSet (StringComparer.Ordinal);
		return entries.Any (entry =>
			Text (Required (entry, 1702)) == productId &&
			!entry.ContainsKey (1712) &&
			!cancelled.Contains (Text (Required (entry, 1705))) &&
			(transactionId is null || Text (Required (entry, 1703)) == transactionId || Text (Required (entry, 1705)) == transactionId));
	}

	static Dictionary<int, List<byte []>> Parse (byte [] payload)
	{
		var reader = new AsnReader (payload, AsnEncodingRules.BER);
		var set = reader.ReadSetOf (skipSortOrderValidation: true);
		reader.ThrowIfNotEmpty ();
		var result = new Dictionary<int, List<byte []>> ();
		var count = 0;
		while (set.HasData) {
			if (++count > 4096)
				throw new CryptographicException ("The receipt contains too many attributes.");
			var attribute = set.ReadSequence ();
			if (!attribute.TryReadInt32 (out var type) || !attribute.TryReadInt32 (out var version))
				throw new CryptographicException ("The receipt contains an unsupported attribute.");
			var value = attribute.ReadOctetString ();
			attribute.ThrowIfNotEmpty ();
			if (type is not (2 or 3 or 4 or 5 or 12 or 17 or 21 or 1702 or 1703 or 1705 or 1712))
				continue;
			if (version != 1)
				throw new CryptographicException ("The receipt contains an unsupported attribute version.");
			if (!result.TryGetValue (type, out var values)) {
				values = [];
				result.Add (type, values);
			}
			if (values.Count > 0 && type != 17)
				throw new CryptographicException ("The receipt contains duplicate attributes.");
			values.Add (value);
		}
		return result;
	}

	static byte [] Required (Dictionary<int, List<byte []>> attributes, int type)
	{
		if (!attributes.TryGetValue (type, out var values) || values.Count != 1)
			throw new CryptographicException ($"The receipt is missing required attribute {type}.");
		return values [0];
	}

	static string Text (byte [] value)
	{
		var reader = new AsnReader (value, AsnEncodingRules.BER);
		var text = reader.ReadCharacterString (UniversalTagNumber.UTF8String);
		reader.ThrowIfNotEmpty ();
		return text;
	}

	static DateTimeOffset Date (byte [] value)
	{
		var reader = new AsnReader (value, AsnEncodingRules.BER);
		var text = reader.ReadCharacterString (UniversalTagNumber.IA5String);
		reader.ThrowIfNotEmpty ();
		if (!DateTimeOffset.TryParseExact (text, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
				CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
			throw new CryptographicException ("The receipt contains an invalid date.");
		return date;
	}
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;
using PurchaseSample.Receipts;

namespace PurchaseSample.Tests;

[TestFixture]
public sealed class ReceiptTests {
	const string Bundle = "org.dotnet.samples.PurchaseSample";
	const string Product = Bundle + ".premium";
	static readonly byte [] Device = [0, 1, 2, 3, 4, 5];
	readonly DateTimeOffset created = DateTimeOffset.UtcNow.AddDays (-2);
	readonly RSA rootKey;
	readonly RSA signerKey;
	readonly X509Certificate2 root;
	readonly X509Certificate2 signer;
	readonly ReceiptValidator validator;

	public ReceiptTests ()
	{
		rootKey = RSA.Create (2048);
		var rootRequest = new CertificateRequest ("CN=Receipt test root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		rootRequest.CertificateExtensions.Add (new X509BasicConstraintsExtension (true, false, 0, true));
		rootRequest.CertificateExtensions.Add (new X509KeyUsageExtension (X509KeyUsageFlags.KeyCertSign, true));
		root = rootRequest.CreateSelfSigned (created.AddYears (-1), created.AddYears (1));
		signerKey = RSA.Create (2048);
		var request = new CertificateRequest ("CN=Receipt test signer", signerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add (new X509BasicConstraintsExtension (false, false, 0, true));
		request.CertificateExtensions.Add (new X509KeyUsageExtension (X509KeyUsageFlags.DigitalSignature, true));
		using var publicSigner = request.Create (root, created.AddDays (-1), created.AddDays (1), RandomNumberGenerator.GetBytes (16));
		signer = publicSigner.CopyWithPrivateKey (signerKey);
		validator = new ReceiptValidator (root);
	}

	[OneTimeTearDown]
	public void Cleanup ()
	{
		signer.Dispose ();
		root.Dispose ();
		signerKey.Dispose ();
		rootKey.Dispose ();
	}

	[Test]
	public void AcceptsValidReceiptAtCreationTimeEvenWhenSignerIsNowExpired ()
	{
		Assert.That (signer.NotAfter.ToUniversalTime (), Is.LessThan (DateTime.UtcNow));
		Assert.That (Validate (Sign (Payload ())), Is.True);
	}

	[TestCase ("1.3.14.3.2.26")]
	[TestCase ("2.16.840.1.101.3.4.2.1")]
	public void AcceptsSha1AndSha256CmsDigests (string oid)
	{
		Assert.That (Validate (Sign (Payload (), oid)), Is.True);
	}

	[Test]
	public void FulfillmentIsIdempotent ()
	{
		var receipt = Sign (Payload ());
		Assert.That (Validate (receipt), Is.True);
		Assert.That (Validate (receipt), Is.True);
	}

	[TestCase ("purchase", true)]
	[TestCase ("original", true)]
	[TestCase ("unrelated", false)]
	public void MatchesPurchaseOrOriginalTransaction (string transaction, bool expected)
	{
		Assert.That (validator.Validate (Sign (Payload ()), Bundle, "1.0", Device, Product, transaction), Is.EqualTo (expected));
	}

	[Test]
	public void RejectsWrongBundle ()
	{
		Assert.Throws<CryptographicException> (() => validator.Validate (Sign (Payload ()), "other.bundle", "1.0", Device, Product));
	}

	[Test]
	public void RejectsWrongVersion ()
	{
		Assert.Throws<CryptographicException> (() => validator.Validate (Sign (Payload ()), Bundle, "2.0", Device, Product));
	}

	[Test]
	public void RejectsWrongDevice ()
	{
		Assert.Throws<CryptographicException> (() => validator.Validate (Sign (Payload ()), Bundle, "1.0", [5, 4, 3, 2, 1, 0], Product));
	}

	[Test]
	public void RejectsTamperedSignature ()
	{
		var receipt = Sign (Payload ());
		receipt [^1] ^= 1;
		Assert.Throws<CryptographicException> (() => Validate (receipt));
	}

	[Test]
	public void RejectsUntrustedSigner ()
	{
		using var key = RSA.Create (2048);
		var request = new CertificateRequest ("CN=Untrusted root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		using var otherRoot = request.CreateSelfSigned (created.AddYears (-1), created.AddYears (1));
		var otherValidator = new ReceiptValidator (otherRoot);
		Assert.Throws<CryptographicException> (() => otherValidator.Validate (Sign (Payload ()), Bundle, "1.0", Device, Product));
	}

	[Test]
	public void RejectsCertificateNotValidAtCreation ()
	{
		Assert.Throws<CryptographicException> (() => Validate (Sign (Payload (date: created.AddDays (-5)))));
	}

	[Test]
	public void RejectsMissingProduct ()
	{
		Assert.That (validator.Validate (Sign (Payload ()), Bundle, "1.0", Device, "unknown.product"), Is.False);
	}

	[Test]
	public void RejectsCancelledPurchase ()
	{
		Assert.That (Validate (Sign (Payload (cancelled: true))), Is.False);
	}

	[Test]
	public void RejectsReplayedActiveEntryForCancelledOriginalTransaction ()
	{
		Assert.That (Validate (Sign (Payload (cancelled: true, duplicatePurchase: true))), Is.False);
	}

	[Test]
	public void RejectsExpiredAppReceipt ()
	{
		Assert.Throws<CryptographicException> (() => Validate (Sign (Payload (expired: true))));
	}

	[Test]
	public void RejectsDuplicateRequiredAttribute ()
	{
		Assert.Throws<CryptographicException> (() => Validate (Sign (Payload (duplicateBundle: true))));
	}

	[Test]
	public void IgnoresUnknownAttributes ()
	{
		Assert.That (Validate (Sign (Payload (unknownAttributes: true))), Is.True);
	}

	[Test]
	public void RejectsMalformedSignedPayload ()
	{
		Assert.Throws<AsnContentException> (() => Validate (Sign ([0x31, 0x03, 0x00])));
	}

	[Test]
	public void RejectsEmptyReceipt ()
	{
		Assert.Throws<CryptographicException> (() => Validate ([]));
	}

	[Test]
	public void RejectsOversizedReceipt ()
	{
		Assert.Throws<CryptographicException> (() => Validate (new byte [4 * 1024 * 1024 + 1]));
	}

	[Test]
	public void RejectsExcessiveAttributeCount ()
	{
		var attributes = Enumerable.Range (0, 4097).Select (_ => (9999, new byte [] { 1 })).ToArray ();
		Assert.Throws<CryptographicException> (() => Validate (Sign (Set (attributes))));
	}

	[Test]
	public void RejectsMissingRequiredAttribute ()
	{
		Assert.Throws<CryptographicException> (() => Validate (Sign (Set ((12, Date (created))))));
	}

	bool Validate (byte [] receipt) => validator.Validate (receipt, Bundle, "1.0", Device, Product);

	byte [] Sign (byte [] payload, string digestOid = "2.16.840.1.101.3.4.2.1")
	{
		var cms = new SignedCms (new ContentInfo (payload));
		cms.ComputeSignature (new CmsSigner (signer) { IncludeOption = X509IncludeOption.EndCertOnly, DigestAlgorithm = new Oid (digestOid) });
		return cms.Encode ();
	}

	byte [] Payload (bool cancelled = false, bool expired = false, bool duplicateBundle = false, bool duplicatePurchase = false, DateTimeOffset? date = null, bool unknownAttributes = false)
	{
		var encodedBundle = Text (Bundle);
		byte [] opaque = [9, 8, 7];
		var hash = SHA1.HashData (Device.Concat (opaque).Concat (encodedBundle).ToArray ());
		var entry = new List<(int, byte [])> {
			(1702, Text (Product)), (1703, Text ("purchase")), (1705, Text ("original")),
		};
		if (cancelled)
			entry.Add ((1712, Date (created)));
		var attributes = new List<(int, byte [])> {
			(2, encodedBundle), (3, Text ("1.0")), (4, opaque), (5, hash),
			(12, Date (date ?? created)), (17, Set (entry.ToArray ())),
		};
		if (expired)
			attributes.Add ((21, Date (created)));
		if (duplicateBundle)
			attributes.Add ((2, encodedBundle));
		if (duplicatePurchase)
			attributes.Add ((17, Set ((1702, Text (Product)), (1703, Text ("other")), (1705, Text ("original")))));
		if (unknownAttributes) {
			attributes.Add ((9999, [1]));
			attributes.Add ((9999, [2]));
		}
		return Set (attributes.ToArray ());
	}

	static byte [] Set (params (int Type, byte [] Value) [] attributes)
	{
		var writer = new AsnWriter (AsnEncodingRules.DER);
		writer.PushSetOf ();
		foreach (var attribute in attributes) {
			writer.PushSequence ();
			writer.WriteInteger (attribute.Type);
			writer.WriteInteger (1);
			writer.WriteOctetString (attribute.Value);
			writer.PopSequence ();
		}
		writer.PopSetOf ();
		return writer.Encode ();
	}

	static byte [] Text (string text)
	{
		var writer = new AsnWriter (AsnEncodingRules.DER);
		writer.WriteCharacterString (UniversalTagNumber.UTF8String, text);
		return writer.Encode ();
	}

	static byte [] Date (DateTimeOffset date)
	{
		var writer = new AsnWriter (AsnEncodingRules.DER);
		writer.WriteCharacterString (UniversalTagNumber.IA5String, date.UtcDateTime.ToString ("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
		return writer.Encode ();
	}
}

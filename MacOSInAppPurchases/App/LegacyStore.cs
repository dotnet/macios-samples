// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

// This mode intentionally demonstrates the API deprecated on macOS 15.
#pragma warning disable CA1422

using System;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Foundation;
using PurchaseInterop;
using PurchaseSample.Receipts;
using StoreKit;

namespace PurchaseSample;

sealed class LegacyStore : PurchaseStore {
	readonly Observer observer;
	readonly Requests requests;
	readonly X509Certificate2 root;
	readonly ReceiptValidator validator;
	SKProductsRequest? productRequest;
	SKReceiptRefreshRequest? refreshRequest;
	SKProduct? product;
	bool started;
	bool disposed;

	public LegacyStore (string productId) : base (productId)
	{
		var rootPath = NSBundle.MainBundle.PathForResource ("AppleIncRootCertificate", "cer");
		var rootData = File.ReadAllBytes (rootPath);
		if (Convert.ToHexString (SHA256.HashData (rootData)) != "B0B1730ECBC7FF4505142C49F1295E6EDA6BCAED7E2C68C5BE91B5A11001F024")
			throw new CryptographicException ("The bundled Apple root certificate has changed.");
		root = X509CertificateLoader.LoadCertificate (rootData);
		validator = new ReceiptValidator (root);
		observer = new Observer (this);
		requests = new Requests (this);
		CheckReceipt ();
	}

	public override void Start ()
	{
		started = true;
		SKPaymentQueue.DefaultQueue.AddTransactionObserver (observer);
	}

	public override void Load ()
	{
		if (Busy)
			return;
		Busy = true;
		CanPurchase = false;
		product = null;
		Report ("Loading the product.");
		using var identifiers = new NSSet (ProductId);
		productRequest = new SKProductsRequest (identifiers) { Delegate = requests };
		productRequest.Start ();
	}

	public override void Buy ()
	{
		if (Busy || product is null)
			return;
		if (!SKPaymentQueue.CanMakePayments) {
			Report ("Purchases are restricted on this account or device.");
			return;
		}
		Busy = true;
		Report ("Waiting for the purchase.");
		using var payment = SKPayment.CreateFrom (product);
		SKPaymentQueue.DefaultQueue.AddPayment (payment);
	}

	public override void Restore ()
	{
		if (Busy)
			return;
		Busy = true;
		Report ("Restoring purchases.");
		SKPaymentQueue.DefaultQueue.RestoreCompletedTransactions ();
	}

	public override void Refresh ()
	{
		if (Busy)
			return;
		Busy = true;
		Report ("Refreshing the receipt. The App Store may ask you to sign in.");
		refreshRequest = new SKReceiptRefreshRequest ((NSDictionary?) null) { Delegate = requests };
		refreshRequest.Start ();
	}

	bool CheckReceipt (string? transactionId = null)
	{
		try {
			var path = NSBundle.MainBundle.AppStoreReceiptUrl?.Path;
			if (path is null || !File.Exists (path)) {
				Entitled = false;
				Report ("No app receipt. Purchase or use Refresh to obtain a sandbox receipt.");
				return false;
			}
			using var stream = File.OpenRead (path);
			if (stream.Length == 0 || stream.Length > 4 * 1024 * 1024)
				throw new CryptographicException ("The receipt is empty or too large.");
			var receipt = new byte [(int) stream.Length];
			stream.ReadExactly (receipt);
			if (stream.ReadByte () != -1)
				throw new IOException ("The receipt changed while it was being read. Retry validation.");
			using var device = StoreBridge.GetDeviceIdentifier ();
			if (device is null)
				throw new CryptographicException ("Could not obtain the device bytes required for receipt validation.");
			Entitled = validator.Validate (receipt,
				NSBundle.MainBundle.BundleIdentifier ?? throw new InvalidOperationException ("The app has no bundle identifier."),
				NSBundle.MainBundle.ObjectForInfoDictionary ("CFBundleShortVersionString")?.ToString () ?? throw new InvalidOperationException ("The app has no version."),
				device.ToArray (), ProductId, transactionId);
			Report (Entitled ? "Locally verified premium access." : "The verified receipt has no matching premium purchase.");
			return Entitled;
		} catch (Exception ex) when (ex is CryptographicException or AsnContentException or IOException or UnauthorizedAccessException) {
			Entitled = false;
			Report ($"Receipt validation failed: {ex.Message} Use Refresh to retry; premium access remains locked.");
			return false;
		}
	}

	void Updated (SKPaymentQueue queue, SKPaymentTransaction [] transactions)
	{
		foreach (var transaction in transactions) {
			if (transaction.Payment.ProductIdentifier != ProductId) {
				Report ("An unfinished transaction belongs to a different product; it has not been finished.");
				continue;
			}
			switch (transaction.TransactionState) {
			case SKPaymentTransactionState.Purchasing:
				Report ("Purchase in progress.");
				break;
			case SKPaymentTransactionState.Deferred:
				Busy = false;
				Report ("Purchase pending approval. No access granted.");
				break;
			case SKPaymentTransactionState.Failed:
				Busy = false;
				var error = transaction.Error;
				Report (error?.Domain == "SKErrorDomain" && error.Code == (long) SKError.PaymentCancelled
					? "Purchase cancelled." : error?.LocalizedDescription ?? "The purchase failed without an error description.");
				queue.FinishTransaction (transaction);
				break;
			case SKPaymentTransactionState.Purchased:
			case SKPaymentTransactionState.Restored:
				Busy = false;
				var id = transaction.TransactionState == SKPaymentTransactionState.Restored
					? transaction.OriginalTransaction?.TransactionIdentifier : transaction.TransactionIdentifier;
				if (id is null) {
					Report ("The purchase has no transaction identifier; it has not been finished.");
				} else if (CheckReceipt (id)) {
					queue.FinishTransaction (transaction);
				}
				break;
			default:
				Busy = false;
				Report ("Unsupported transaction state; the transaction has not been finished.");
				break;
			}
		}
	}

	public override void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
		if (started)
			SKPaymentQueue.DefaultQueue.RemoveTransactionObserver (observer);
		productRequest?.Cancel ();
		refreshRequest?.Cancel ();
		productRequest?.Dispose ();
		refreshRequest?.Dispose ();
		requests.Dispose ();
		observer.Dispose ();
		root.Dispose ();
	}

	sealed class Observer : SKPaymentTransactionObserver {
		readonly LegacyStore owner;
		public Observer (LegacyStore owner) { this.owner = owner; }
		public override void UpdatedTransactions (SKPaymentQueue queue, SKPaymentTransaction [] transactions) => owner.Updated (queue, transactions);
		public override void RestoreCompletedTransactionsFailedWithError (SKPaymentQueue queue, NSError error)
		{
			owner.Busy = false;
			owner.Report (error.LocalizedDescription);
		}
		public override void RestoreCompletedTransactionsFinished (SKPaymentQueue queue)
		{
			owner.Busy = false;
			owner.CheckReceipt ();
		}
		public override void DidRevokeEntitlements (SKPaymentQueue queue, string [] productIdentifiers)
		{
			if (productIdentifiers.Contains (owner.ProductId)) {
				owner.Entitled = false;
				owner.Report ("Premium access was revoked. Refresh the receipt before checking access again.");
			}
		}
	}

	sealed class Requests : SKProductsRequestDelegate {
		readonly LegacyStore owner;
		public Requests (LegacyStore owner) { this.owner = owner; }
		public override void ReceivedResponse (SKProductsRequest request, SKProductsResponse response)
		{
			owner.product = response.Products.FirstOrDefault (v => v.ProductIdentifier == owner.ProductId);
			owner.CanPurchase = owner.product is not null;
			owner.Busy = false;
			if (owner.product is null || response.InvalidProducts.Length != 0) {
				owner.CanPurchase = false;
				owner.Report ("Product unavailable. Check product ID, bundle ID, agreements, and App Store Connect availability.");
				return;
			}
			using var formatter = new NSNumberFormatter {
				NumberStyle = NSNumberFormatterStyle.Currency,
				Locale = owner.product.PriceLocale,
			};
			owner.ProductDescription = $"{owner.product.LocalizedTitle} - {formatter.StringFromNumber (owner.product.Price)}";
			owner.Report (owner.ProductDescription);
		}
		public override void RequestFailed (SKRequest request, NSError error)
		{
			owner.Busy = false;
			owner.Report (error.LocalizedDescription);
			Cleanup (request);
		}
		public override void RequestFinished (SKRequest request)
		{
			if (request == owner.refreshRequest) {
				owner.Busy = false;
				owner.CheckReceipt ();
				owner.Updated (SKPaymentQueue.DefaultQueue, SKPaymentQueue.DefaultQueue.Transactions);
			}
			Cleanup (request);
		}
		void Cleanup (SKRequest request)
		{
			if (request == owner.productRequest)
				owner.productRequest = null;
			if (request == owner.refreshRequest)
				owner.refreshRequest = null;
			request.Dispose ();
		}
	}
}

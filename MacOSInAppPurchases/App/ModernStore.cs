// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using Foundation;
using PurchaseInterop;

namespace PurchaseSample;

sealed class ModernStore : PurchaseStore {
	readonly StoreBridge bridge = new StoreBridge ();
	bool refreshing;
	bool refreshAgain;
	bool disposed;

	public ModernStore (string productId) : base (productId) { }

	public override void Start ()
	{
		bridge.Start (ProductId, error => {
			if (disposed)
				return;
			if (error is not null) {
				Entitled = false;
				Report (error.LocalizedDescription);
			} else {
				Refresh ();
			}
		});
		Refresh ();
	}

	public override void Load ()
	{
		if (Busy)
			return;
		Busy = true;
		Report ("Loading the product.");
		bridge.LoadProduct (ProductId, (description, error) => {
			if (disposed)
				return;
			Busy = false;
			CanPurchase = error is null && description is not null;
			ProductDescription = description ?? "Product unavailable.";
			Report (error?.LocalizedDescription ?? ProductDescription);
		});
	}

	public override void Buy ()
	{
		if (Busy || !CanPurchase)
			return;
		Busy = true;
		Report ("Waiting for the purchase.");
		bridge.Purchase (ProductId, (result, error) => {
			if (disposed)
				return;
			Busy = false;
			if (error is not null) {
				Report (error.LocalizedDescription);
			} else if (result == "purchased") {
				Refresh ();
			} else if (result == "pending") {
				Report ("Purchase pending approval. No access granted.");
			} else if (result == "cancelled") {
				Report ("Purchase cancelled.");
			} else {
				Report ("StoreKit returned an unsupported purchase result.");
			}
		});
	}

	public override void Restore ()
	{
		if (Busy)
			return;
		Busy = true;
		Report ("Restoring purchases.");
		bridge.Restore (error => {
			if (disposed)
				return;
			Busy = false;
			if (error is not null)
				Report (error.LocalizedDescription);
			else
				Refresh ();
		});
	}

	public override void Refresh ()
	{
		if (refreshing) {
			refreshAgain = true;
			return;
		}
		refreshing = true;
		bridge.ReadEntitlement ((entitled, error) => {
			if (disposed)
				return;
			if (error is not null) {
				Entitled = false;
				Report (error.LocalizedDescription);
				CompleteRefresh ();
				return;
			}
			// Setting access to the verified state is idempotent fulfillment for this sample.
			Entitled = entitled;
			Report (entitled ? "Verified premium access." : "No current premium entitlement.");
			bridge.Finish (finishError => {
				if (disposed)
					return;
				if (finishError is not null)
					Report (finishError.LocalizedDescription);
				CompleteRefresh ();
			});
		});
	}

	void CompleteRefresh ()
	{
		refreshing = false;
		if (refreshAgain) {
			refreshAgain = false;
			Refresh ();
		}
	}

	public override void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
		bridge.Stop ();
		bridge.Dispose ();
	}
}

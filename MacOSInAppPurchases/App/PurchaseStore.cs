// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;

namespace PurchaseSample;

abstract class PurchaseStore : IDisposable {
	protected readonly string ProductId;
	public event Action? Changed;
	public string Status { get; private set; } = "Starting the store.";
	public string ProductDescription { get; protected set; } = "Product not loaded.";
	public bool Entitled { get; protected set; }
	public bool CanPurchase { get; protected set; }
	public bool Busy { get; protected set; }

	protected PurchaseStore (string productId)
	{
		ProductId = productId;
	}

	protected void Report (string status)
	{
		Status = status;
		Console.WriteLine (status);
		Changed?.Invoke ();
	}

	public abstract void Start ();
	public abstract void Load ();
	public abstract void Buy ();
	public abstract void Restore ();
	public abstract void Refresh ();
	public abstract void Dispose ();
}

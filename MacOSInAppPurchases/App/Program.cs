// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Linq;
using AppKit;
using CoreGraphics;
using Foundation;

namespace PurchaseSample;

static class Program {
	static void Main (string [] args)
	{
		if (args.Any (v => v != "--storekit1" && v != "--storekit2") || args.Distinct ().Count () > 1)
			throw new ArgumentException ("Choose --storekit1 or --storekit2, not both.");
		NSApplication.Init ();
		var productId = Environment.GetEnvironmentVariable ("PURCHASE_PRODUCT_ID") ?? "org.dotnet.samples.PurchaseSample.premium";
		using PurchaseStore store = args.Contains ("--storekit1") ? new LegacyStore (productId) : new ModernStore (productId);
		using var appDelegate = new Application (store, args.Contains ("--storekit1") ? "StoreKit 1" : "StoreKit 2");
		NSApplication.SharedApplication.Delegate = appDelegate;
		NSApplication.SharedApplication.Run ();
	}
}

sealed class Application : NSApplicationDelegate {
	readonly PurchaseStore store;
	readonly string mode;
	NSWindow? window;
	NSTextField? status;
	NSTextField? product;
	NSTextField? access;
	NSButton? buy;
	NSButton? load;
	NSButton? restore;
	NSButton? refresh;

	public Application (PurchaseStore store, string mode)
	{
		this.store = store;
		this.mode = mode;
	}

	public override void DidFinishLaunching (NSNotification notification)
	{
		window = new NSWindow (new CGRect (0, 0, 680, 340), NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable,
			NSBackingStore.Buffered, false) { Title = $"In-app purchases - {mode}" };
		var content = new NSView (new CGRect (0, 0, 680, 340));
		window.ContentView = content;
		product = Label (content, 280, "Product not loaded.");
		access = Label (content, 240, "Premium locked.");
		status = Label (content, 115, store.Status);
		status.Frame = new CGRect (20, 80, 640, 115);
		status.LineBreakMode = NSLineBreakMode.ByWordWrapping;
		load = Button (content, 20, "Load product", store.Load);
		buy = Button (content, 180, "Buy premium", store.Buy);
		restore = Button (content, 340, "Restore", store.Restore);
		refresh = Button (content, 500, "Refresh", store.Refresh);
		store.Changed += Update;
		window.Center ();
		window.MakeKeyAndOrderFront (null);
		if (OperatingSystem.IsMacOSVersionAtLeast (14))
			NSApplication.SharedApplication.Activate ();
		store.Start ();
		Update ();
	}

	static NSTextField Label (NSView content, double y, string text)
	{
		var label = NSTextField.CreateLabel (text);
		label.Frame = new CGRect (20, y, 640, 40);
		content.AddSubview (label);
		return label;
	}

	static NSButton Button (NSView content, double x, string text, Action action)
	{
		var button = NSButton.CreateButton (text, action);
		button.Frame = new CGRect (x, 20, 150, 40);
		content.AddSubview (button);
		return button;
	}

	void Update ()
	{
		if (status is null || product is null || access is null || buy is null || load is null || restore is null || refresh is null)
			return;
		status.StringValue = store.Status;
		product.StringValue = store.ProductDescription;
		access.StringValue = store.Entitled ? "Premium unlocked." : "Premium locked.";
		buy.Enabled = store.CanPurchase && !store.Busy && !store.Entitled;
		load.Enabled = restore.Enabled = refresh.Enabled = !store.Busy;
	}

	public override bool ApplicationShouldTerminateAfterLastWindowClosed (NSApplication sender) => true;
	public override void WillTerminate (NSNotification notification)
	{
		store.Changed -= Update;
		store.Dispose ();
	}
}

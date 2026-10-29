// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Diagnostics;
using AppKit;
using Foundation;
using ObjCRuntime;
using PurchaseInterop;

NSApplication.Init ();
using var bridge = new StoreBridge ();
foreach (var name in new [] { "startWithProductID:changed:", "loadProduct:completion:", "purchase:completion:",
	"readEntitlementWithCompletion:", "restoreWithCompletion:", "finishWithCompletion:", "stop" }) {
	if (!bridge.RespondsToSelector (new Selector (name)))
		throw new InvalidOperationException ($"Missing native selector: {name}");
}
using var device = StoreBridge.GetDeviceIdentifier ();
if (device is null || device.Length != 6)
	throw new InvalidOperationException ("Receipt device helper did not return six bytes.");
Console.WriteLine ("Native selectors and device-byte helper passed.");

var receivedEntitlement = false;
var receivedProduct = false;
var finished = false;
bridge.Start ("org.dotnet.samples.unconfigured", error => {
	if (error is not null)
		Console.WriteLine ($"Transaction update error: {error.LocalizedDescription}");
});
bridge.ReadEntitlement ((entitled, error) => {
	if (entitled)
		throw new InvalidOperationException ("The unconfigured product must not have an entitlement.");
	Console.WriteLine (error is null ? "Entitlement callback: no entitlement." : $"Entitlement callback error: {error.LocalizedDescription}");
	receivedEntitlement = true;
	bridge.Finish (finishError => {
		if (finishError is not null)
			throw new InvalidOperationException (finishError.LocalizedDescription);
		finished = true;
	});
});
bridge.LoadProduct ("org.dotnet.samples.unconfigured", (description, error) => {
	if (description is not null || error is null)
		throw new InvalidOperationException ("The unconfigured product must return an explicit error.");
	Console.WriteLine ($"Product error callback: {error.LocalizedDescription}");
	receivedProduct = true;
});
var elapsed = Stopwatch.StartNew ();
while (!(receivedEntitlement && receivedProduct && finished) && elapsed.Elapsed < TimeSpan.FromSeconds (30)) {
	using var until = NSDate.FromTimeIntervalSinceNow (0.1);
	NSRunLoop.Main.RunUntil (until);
}
bridge.Stop ();
if (!(receivedEntitlement && receivedProduct && finished))
	throw new TimeoutException ("Native smoke test did not receive all callbacks.");
Console.WriteLine ("Native callback smoke test passed.");

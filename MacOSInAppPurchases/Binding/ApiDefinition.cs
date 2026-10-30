// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using Foundation;
using ObjCRuntime;

namespace PurchaseInterop {
	[BaseType (typeof (NSObject))]
	[Mac (12, 0)]
	interface StoreBridge {
		[Export ("startWithProductID:changed:")]
		void Start (string productId, Action<NSError?> changed);

		[Export ("loadProduct:completion:")]
		void LoadProduct (string productId, Action<string?, NSError?> completion);

		[Export ("purchase:completion:")]
		void Purchase (string productId, Action<string?, NSError?> completion);

		[Export ("readEntitlementWithCompletion:")]
		void ReadEntitlement (Action<bool, NSError?> completion);

		[Export ("restoreWithCompletion:")]
		void Restore (Action<NSError?> completion);

		[Export ("finishWithCompletion:")]
		void Finish (Action<NSError?> completion);

		[Export ("stop")]
		void Stop ();

		[Static]
		[return: NullAllowed]
		[Export ("deviceIdentifier")]
		NSData GetDeviceIdentifier ();
	}
}

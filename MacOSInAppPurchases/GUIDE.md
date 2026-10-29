# In-app purchases in .NET for macOS

Use StoreKit to sell digital content in a Mac App Store application. This guide
covers native AppKit applications built with .NET for macOS, not Mac Catalyst.
It accompanies a [runnable sample](README.md)
that unlocks one non-consumable product.

## Choose an API

Apple provides two purchase APIs:

* **StoreKit 2** is the recommended API for new applications. It is available on
  macOS 12 and later and provides verified transactions, current entitlements,
  and asynchronous transaction updates.
* **StoreKit 1**, Apple's original in-app purchase API, is accessible through the
  `StoreKit` C# namespace. Apple deprecated its purchase APIs on macOS 15.
  Deprecation does not mean that these APIs have been removed.

The SDK does not expose StoreKit 2's Swift `Product` and `Transaction` purchase
APIs as C# bindings. `StoreKit.AppStore.RequestReview` is a review API, not a
purchase API. To use StoreKit 2, put the purchase logic in a small Swift
framework and bind its Objective-C-compatible interface using
[Native Library Interop](https://github.com/dotnet/macios/blob/main/docs/native-library-interop.md).

The sample has separate StoreKit 1 and StoreKit 2 launch modes. Do not register
both transaction processors for the same product. Purchases made using either
API are available to the other, but your app must have a single owner for
verification, fulfillment, and finishing each transaction.

See Apple's [Choosing a StoreKit API](https://developer.apple.com/documentation/storekit/choosing-a-storekit-api-for-in-app-purchases)
for API availability and migration considerations.

## Configure the app and products

Before testing against Apple's sandbox:

1. Enroll in the Apple Developer Program and configure the required agreements,
   tax information, and banking information in App Store Connect.
2. Register an explicit bundle identifier and create the corresponding macOS app
   record in App Store Connect. Enable the In-App Purchase capability for the
   app identifier where required. Configure signing and provisioning for that
   identifier.
3. Create a **non-consumable** in-app purchase. Set its product identifier,
   localized metadata, price, and availability. Use that exact identifier in the
   sample's `PURCHASE_PRODUCT_ID` environment variable. The product identifier
   is separate from the bundle identifier, even if they share a prefix.
4. Set the sample's `ApplicationId` to the registered bundle identifier and
   supply your signing identity and provisioning profile when building.
   The signed app's bundle identifier must match the App Store Connect record.
5. Create a Sandbox Apple Account in App Store Connect and follow Apple's
   [sandbox testing instructions](https://developer.apple.com/documentation/storekit/testing-in-app-purchases-with-sandbox).
   Do not use a production purchase as a substitute for a sandbox test.

Mac App Store apps use the App Sandbox. The sample includes
`com.apple.security.app-sandbox` and `com.apple.security.network.client`
entitlements. These entitlements do not replace App Store Connect configuration,
signing, or provisioning. There is no generic `com.apple.developer.in-app-purchase`
entitlement to add to make products appear.

No sample product is registered for you. Product lookup may return no products
until configuration has propagated through App Store Connect. Check the bundle
identifier, exact product identifier, agreements, product metadata and
availability, signing, network access, and Apple's current sandbox requirements.
Never interpret an empty product list as a successful purchase.

## Build and run the sample

See the [sample README](README.md) for complete
commands. The sample selects a .NET 10 SDK using its own `global.json`
and requires the macOS workload and a compatible Xcode.
Its projects target macOS 12; use a higher deployment target if required by
your installed workload.

The app's window contains **Load product**, **Buy premium**, **Restore**, and
**Refresh** controls. It displays the selected API, localized product information,
premium access, and status or errors. Load the product before purchasing.

Restart the app to change API modes:

```sh
# Run from MacOSInAppPurchases.
dotnet build App/PurchaseSample.csproj -r osx-arm64

PURCHASE_PRODUCT_ID=com.example.myapp.premium \
  App/bin/Debug/net10.0-macos/osx-arm64/PurchaseSample.app/Contents/MacOS/PurchaseSample --storekit2
```

Use `osx-x64` for both the build and executable path on an Intel Mac. For sandbox
purchases, build with your registered application identifier and signing
configuration as described in the sample README.

## StoreKit 1: C# purchase lifecycle

The implementation is in
[`App/LegacyStore.cs`](App/LegacyStore.cs).

### Observe transactions for the lifetime of the app

Create and retain a subclass of `SKPaymentTransactionObserver`. Register it with
`SKPaymentQueue.DefaultQueue.AddTransactionObserver` when the app launches,
not only when a purchase button is pressed. Keep the observer strongly
referenced and remove it at shutdown.

The queue can redeliver unfinished transactions after a crash or relaunch.
Fulfillment must therefore be idempotent. In this sample, fulfillment means
setting the premium-access state from a verified non-consumable entitlement,
not incrementing a balance or granting another copy of a product.

### Request products and display store prices

Use `SKProductsRequest` with the configured identifiers. Keep both the request
and its delegate strongly referenced until completion or failure. Inspect
`SKProductsResponse.InvalidProducts` as well as `Products`.

Display `SKProduct.LocalizedTitle` and format `SKProduct.Price` with
`NSNumberFormatter` using `SKProduct.PriceLocale`. Do not hard-code a price,
currency, or store locale. Use the returned `SKProduct`, not an unchecked
identifier, to create the payment:

```csharp
if (SKPaymentQueue.CanMakePayments) {
    using var payment = SKPayment.CreateFrom (product);
    SKPaymentQueue.DefaultQueue.AddPayment (payment);
}
```

Here, `product` is the previously loaded `SKProduct`. The sample reports account
restrictions rather than silently ignoring a failed `CanMakePayments` check.

### Process every transaction state

| State          | Action                                                               |
|----------------|----------------------------------------------------------------------|
| `Purchasing`   | Show progress. Do not finish the transaction.                         |
| `Deferred`     | Show pending approval. Do not grant access or finish it.              |
| `Purchased`    | Validate the receipt and purchase, fulfill, then finish.              |
| `Restored`     | Validate the restored purchase, fulfill, then finish.                 |
| `Failed`       | Report the error or cancellation, then finish the failed transaction. |

Use `transaction.Error.Domain` and `SKError.PaymentCancelled` to distinguish a
user cancellation from a store failure. Unknown states are reported and left
unfinished.

For purchases, match `TransactionIdentifier` to the verified receipt's purchase
entry. For restores, use `OriginalTransaction.TransactionIdentifier` and the
receipt's original-transaction identifier as appropriate. Do not grant access
just because the queue reports `Purchased` or `Restored`, or because a receipt
file exists.

If receipt validation fails, keep premium access locked and leave the successful
transaction unfinished. The user can retry with **Refresh**. After a successful
refresh, the sample revalidates the receipt and retries outstanding queue
transactions.

### Validate receipts locally

The sample includes a local validator in
[`ReceiptValidation/ReceiptValidator.cs`](ReceiptValidation/ReceiptValidator.cs).
It does not require a verification service and does not embed an App Store
shared secret.

The validation sequence is:

1. Load `NSBundle.MainBundle.AppStoreReceiptUrl` and decode its PKCS #7 container.
   Verify the signature and build the signer chain to an explicitly trusted
   Apple root. A certificate included in the receipt is not a trusted root.
2. Evaluate certificate validity at the signed `receipt_creation_date`, not
   today's date. A certificate that has since expired can have signed a valid
   receipt. Do not pin intermediate certificates; Apple can replace them.
3. Match the receipt bundle identifier to `BundleIdentifier` and its app-version
   field to **`CFBundleShortVersionString` on macOS**.
4. Compute the receipt-format SHA-1 over the device bytes, opaque receipt value,
   and original ASN.1-encoded bundle identifier. Compare the digest with the
   receipt hash. The Swift helper follows Apple's macOS network-interface
   selection order; a random UUID or UTF-8 MAC-address string is not equivalent.
5. Reject an expired app receipt, if it has an expiration field. Find the
   configured non-consumable purchase, check transaction identifiers when
   processing a queue transaction, and exclude cancelled purchase entries.

The .NET `System.Security.Cryptography.Pkcs` package verifies the CMS signature;
`X509Chain` verifies the chain, and `System.Formats.Asn1` decodes the receipt.
The sample bounds receipt size and attribute counts and rejects ambiguous or
unsupported required data. It does not implement its own signature algorithms.
SHA-1 is used for the device hash because Apple's receipt format requires it;
that does not imply requiring SHA-1 certificate signatures.

The bundled root is downloaded from Apple's PKI site. Its provenance,
fingerprint, and update procedure are in the sample README. Synthetic test
roots are confined to the test project and are never selected by the app.

Before entering the AppKit application loop, the legacy mode validates an
existing receipt. A missing receipt is normal for a new sandbox tester:
premium remains locked and the app still opens. **Refresh** explicitly starts
`SKReceiptRefreshRequest`, which can prompt for authentication; do not loop
authentication requests or terminate the app on validation failure.

Local receipt validation proves what the signed receipt says, not that it is
the latest state on Apple's servers. The sample disables current-time
certificate revocation lookup when evaluating the historical signer chain.
An offline receipt cannot immediately detect every subsequent refund.
Refresh receipts when appropriate, respond to revocation callbacks, and use
StoreKit 2 or server-side notifications for production requirements that need
up-to-date entitlement state.

See Apple's [on-device receipt validation](https://developer.apple.com/documentation/appstorereceipts/validating-receipts-on-the-device)
and [receipt fields](https://developer.apple.com/library/archive/releasenotes/General/ValidateAppStoreReceipt/Chapters/ReceiptFields.html).

### Restore purchases

Provide a user-initiated restore action using
`SKPaymentQueue.DefaultQueue.RestoreCompletedTransactions`. Handle both
`RestoreCompletedTransactionsFinished` and
`RestoreCompletedTransactionsFailedWithError`.

Restoration completion does not prove that the configured product was
purchased. Validate and inspect the receipt even when the restore operation
delivers no matching transactions. Finished consumables are not restored by
this mechanism.

## StoreKit 2: Swift and C# interoperability

The sample's native implementation is in
[`StoreBridge/StoreBridge.swift`](StoreBridge/StoreBridge.swift).
Its binding definitions are in
[`Binding/ApiDefinition.cs`](Binding/ApiDefinition.cs).

The .NET binding project uses the existing `XcodeProject` build item:

```xml
<XcodeProject Include="../StoreBridge/StoreBridge.xcodeproj"
              SchemeName="StoreBridge"
              Kind="Framework"
              Frameworks="StoreKit IOKit" />
```

Building the app builds the framework and binding and embeds the framework in
the app. No separate framework-copy step is required.

Keep Swift-only types behind the boundary. The bridge exposes an `NSObject`
subclass, explicit Objective-C selectors, strings, booleans, `NSData`, and
completion blocks with nullable `NSError` values. Swift `async`, generic
`VerificationResult`, and `AsyncSequence` are not C# binding signatures.
The bridge is main-actor isolated so C# callbacks can update AppKit controls.

The modern implementation:

* Loads the configured non-consumable using `Product.products(for:)` and returns
  `displayName` and `displayPrice`.
* Processes `Product.purchase()` results separately: verified success,
  pending approval, user cancellation, and errors.
* Starts a long-lived `Transaction.updates` listener at launch. Every
  transaction must be `.verified` before it is used.
* Reads verified `Transaction.currentEntitlements` at launch, after purchases,
  on refresh, and after updates. Rebuilds the entitlement state instead of
  treating every transaction update as a grant; revoked products must lock.
* Includes verified unfinished transactions when preparing fulfillment.
  After C# applies the entitlement state, it acknowledges those transactions
  through the bridge's finish method.
* Calls `AppStore.sync()` only for the user-initiated **Restore** action, then
  rereads entitlements. Routine **Refresh** does not force authentication.
* Cancels its update listener at shutdown.

There is no need to parse the legacy receipt to verify StoreKit 2 transactions.
Do not grant access for `.unverified` results. A production app that delivers
downloadable content must complete and durably record that delivery before
calling `finish()`; the sample only unlocks an idempotent local feature.

## Consumables, subscriptions, and production apps

The sample intentionally supports one non-consumable. Do not reuse its
entitlement rules unchanged for other product types:

* **Consumables** require durable, transaction-ID-based fulfillment. A redelivered
  transaction must not add credits twice. Finished consumables do not appear in
  StoreKit 2 current entitlements and cannot be recovered with a simple restore.
* **Auto-renewable subscriptions** require current expiration, renewal, billing
  retry, grace-period, refund, and revocation handling. A historical purchase
  entry alone is not evidence of an active subscription.
* **Non-renewing subscriptions** require app-managed duration and restoration
  rules; a current-entitlements entry does not calculate the access period.

For server-backed access, associate transactions with your app's account model,
verify signed data on the server, and consider App Store Server API and App
Store Server Notifications. Never ship private App Store API keys or shared
secrets in a client app. Do not log full receipts or device identifiers.

When migrating an existing app, preserve the existing fulfillment ledger and
test purchases from both API generations. Do not let a change of API grant
the same consumable again or abandon unfinished transactions.

## Test purchases, not just compilation

The [sample test checklist](README.md#purchase-test-checklist)
covers success, cancellation, pending approval, restore, relaunch, duplicate
delivery, validation failures, and refunds. Account-free tests exercise the
local validator with synthetic certificates and receipts. They do not prove
that your App Store Connect configuration works or that a real Apple-signed
sandbox purchase succeeds.

Xcode StoreKit configuration files are a separate test environment. Selecting
one on the **framework's** Xcode scheme does not attach it to an app launched
by `dotnet` or a shell. Local StoreKit receipts use a different signing
certificate; this sample deliberately trusts the Apple root only and does not
disable receipt verification to accept them. Use the Apple sandbox for this
sample's purchase checklist.

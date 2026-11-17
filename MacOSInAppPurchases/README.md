---
name: ".NET for macOS - In-app purchases"
description: "Native AppKit non-consumable purchases with StoreKit 1 local receipt validation and StoreKit 2 through a bound Swift framework."
page_type: sample
languages:
- csharp
- swift
products:
- dotnet-macios
urlFragment: macosinapppurchases
---

# macOS in-app purchase sample

An AppKit app that unlocks one non-consumable using either StoreKit 1 with
**local receipt validation**, or StoreKit 2 through a bound Swift framework.
Read the [guide](GUIDE.md) before configuring purchases.

## Prerequisites

* A Mac with .NET 10, the `macos` workload, and the Xcode version required by that
  workload. If needed, install the workload using `dotnet workload install macos`.
* For real sandbox purchases, an Apple Developer membership, a registered macOS
  app and non-consumable in App Store Connect, suitable signing/provisioning,
  and a Sandbox Apple Account.

Run all commands below **from this directory**. Its `global.json` selects a
.NET 10 SDK, consistent with the other samples in this repository.
The projects target macOS 12. If your workload requires a higher minimum, update
`SupportedOSPlatformVersion` in both macOS projects and
`MACOSX_DEPLOYMENT_TARGET` in the Xcode project together.

## Build and launch

```sh
dotnet build App/PurchaseSample.csproj -r osx-arm64

PURCHASE_PRODUCT_ID=com.example.myapp.premium \
  App/bin/Debug/net10.0-macos/osx-arm64/PurchaseSample.app/Contents/MacOS/PurchaseSample --storekit2
```

On an Intel Mac, use `osx-x64` instead of `osx-arm64` in **both** commands.
The app defaults to StoreKit 2. To exercise StoreKit 1, quit the app and
relaunch the same executable with `--storekit1` instead. Do not pass both modes
or run both simultaneously against the same app's purchases.

The unsigned/ad-hoc development build is useful for inspecting the UI and
missing-receipt/error handling, but does not configure sandbox purchases.
For sandbox testing, build with your own bundle identifier and signing:

```sh
dotnet build App/PurchaseSample.csproj -r osx-arm64 \
  -p:ApplicationId=com.example.myapp \
  -p:CodesignKey="Apple Development: Your Name (TEAMID)" \
  -p:CodesignProvision="Your Mac App Development Profile"
```

Use the identity/profile appropriate to your developer account and Apple's
current sandbox requirements. Replace the product identifier when launching.
Do not change the Swift framework bundle identifier to match your app:
the framework has its own identity.

Click **Load product**, verify the localized description and price, then
**Buy premium**. **Restore** is user-initiated and may authenticate. In
StoreKit 1, **Refresh** explicitly refreshes the receipt and retries
unfinished transactions; it may authenticate. In StoreKit 2, **Refresh**
rereads current entitlements without forcing a sync.

The default product identifier is intentionally unconfigured. Product-not-found
and missing-receipt messages do not mean access was granted.

## Projects

| Path                 | Purpose                                                      |
|----------------------|--------------------------------------------------------------|
| `App/`               | AppKit UI and separate legacy/modern purchase controllers.    |
| `Binding/`           | C# binding of the sample's Objective-C-compatible Swift API.  |
| `StoreBridge/`       | Swift StoreKit 2 logic and macOS receipt-device-byte helper.  |
| `ReceiptValidation/` | Local PKCS #7, chain, ASN.1, and purchase validation.          |
| `Tests/`             | Synthetic receipt tests; no Apple account or secrets needed. |

The binding's `XcodeProject` item builds and packages the framework automatically.
Open `MacOSInAppPurchases.slnx` to work with all managed projects, including tests.
Generated archives/frameworks and managed outputs go under `obj`/`bin`.
The helper uses IOKit only to obtain Apple's documented receipt device bytes;
this is not a new SDK binding for IOKit.

## Receipt trust anchor

`ReceiptValidation/AppleIncRootCertificate.cer` is the unmodified DER certificate
downloaded from:

https://www.apple.com/appleca/AppleIncRootCertificate.cer

Apple lists its certificates at https://www.apple.com/certificateauthority/.
The certificate's subject is `Apple Root CA`; its SHA-256 fingerprint is:

```text
b0b1730ecbc7ff4505142c49f1295e6eda6bcaed7e2c68c5be91b5a11001f024
```

The app checks that fingerprint before creating the validator. It never trusts a
receipt-supplied root or a user-selected trust anchor. Test certificates are
created only inside the test fixture.

If Apple changes the receipt root, verify the replacement through Apple's
published PKI information, update the bundled certificate and app fingerprint,
and validate representative Apple-signed receipts before shipping. Do not
replace the root with an intermediate, pin an intermediate, disable trust
validation, or change the fingerprint just to make a failing receipt pass.
The library supports SHA-1 and SHA-256 CMS digests through the platform crypto
implementation; evaluate signer chains at receipt creation time.

Local validation is not a real-time refund service. The UI locks on validation
failure or revocation; an old valid offline receipt may not reflect a later
refund until refreshed. This is a focused non-consumable example, not a
subscription engine or a tamper-proof licensing system.

## Automated checks

```sh
dotnet test Tests/ReceiptTests.csproj
dotnet build App/PurchaseSample.csproj -r osx-arm64
```

For an account-free native integration check on the current Mac:

```sh
dotnet build Tests/Native/NativeSmoke.csproj -r osx-arm64
Tests/Native/bin/Debug/net10.0-macos/osx-arm64/NativeSmoke.app/Contents/MacOS/NativeSmoke
```

Use `osx-x64` in both paths on Intel. This check verifies the exported selectors,
device-byte helper, entitlement callback, finish callback, and an explicit
product-not-found error without initiating a purchase or restore.

The tests cover signature tampering, malformed/missing/duplicate attributes,
untrusted chains, certificate evaluation at receipt creation time, wrong app
identity/version/device, absent and cancelled purchases, transaction matching,
expiration, and repeated fulfillment. Tests deliberately use synthetic signing
material; passing them is not equivalent to testing an Apple sandbox receipt.

## Purchase test checklist

Run this checklist separately for `--storekit1` and `--storekit2`, with the same
configured non-consumable and one API mode running at a time:

1. Launch without an existing purchase. Premium must remain locked. Load the
   configured product and check the store-localized name and price.
2. Purchase with a Sandbox Apple Account. Premium must unlock only after
   verification and fulfillment, not when the payment sheet opens.
3. Reset the sandbox purchase history as needed and cancel the purchase sheet.
   Show cancellation without unlocking.
4. Exercise pending/Ask to Buy where supported by the test environment.
   Do not unlock or finish a pending transaction. Approval must be handled by
   the launch-time listener even if the purchase UI is no longer visible.
5. Quit and relaunch after purchase. Access must come from verified receipt or
   current entitlements, not from an unchecked preferences flag.
6. Interrupt fulfillment before finishing a transaction, then relaunch.
   Redelivery must be safe and must not grant an extra benefit.
7. Restore on a clean installation/account session. Check success, no matching
   purchases, cancellation, and store/network failure. A successful restore
   operation with no entitlement must not unlock premium.
8. Revoke/refund the sandbox purchase using Apple's available test tools.
   Check the update callback and refresh/relaunch behavior. Record offline
   freshness limits rather than treating an old receipt as current server state.
9. In StoreKit 1, test an invalid/missing receipt on a disposable test build.
   Premium must remain locked, the failure must be visible, and **Refresh**
   must allow recovery. Do not disable signature or device checks.
10. Use an invalid product identifier or disconnect the network. The app must
    report the failure without granting access or claiming restore success.

Apple's sandbox and Xcode-local StoreKit testing are different environments.
A `.storekit` file selected on the framework scheme does not configure a
shell-launched .NET app. This sample's production trust path intentionally
does not accept Xcode's local receipt-signing certificate.

---
name: ".NET for iOS - Background Assets"
description: "Downloads an asset pack from your own server using a Background Assets downloader extension written in C#."
page_type: sample
languages:
- csharp
products:
- dotnet-macios
urlFragment: macios-backgroundassets-ios
---

# Background Assets

This sample uses a C# Background Assets downloader extension and a containing iOS
app. The extension implements `IBAManagedDownloaderExtension`; the system manages
downloads and calls `ShouldDownload` to let the extension filter asset packs. The
app downloads the `Greeting` pack and displays `Assets/Greeting.txt`.

The extension's `Info.plist` declares `EXAppExtensionAttributes` with
`EXExtensionPointIdentifier` set to `com.apple.background-asset-downloader-extension`
and `EXPrincipalClass` matching `[Register ("BackgroundDownloadHandler")]`. It
belongs in `BackgroundAssetsApp.app/Extensions`, not `PlugIns`. .NET starts up and
then calls `NSExtensionMain`, the same entry point Xcode uses for Objective-C
ExtensionKit extensions. You don't need a Swift `@main` implementation.

## Requirements

- .NET 10 with an iOS workload that supports iOS 27, and the Xcode version that
  workload requires.
- iOS 27 or later. The downloader protocol itself requires only iOS 26, but the
  app uses the newer `GetManifestAsync` API.
- A .NET iOS SDK that puts `EXAppExtensionAttributes` extensions in the app's
  `Extensions` directory and doesn't require an `NSExtension` dictionary.
  Older SDKs can build the extension by itself, but not the app that contains it.
- An Apple Developer account and provisioning profiles with the same app group
  for both targets when running on a device.
- HTTPS hosting with a certificate trusted by the test device. Do not disable
  certificate validation to run this sample.

## Configure identifiers

Replace `com.example.BackgroundAssets` in both project files with your registered
app identifier, keeping `.downloader` as the extension suffix. Replace
`group.com.example.BackgroundAssets` in both project files and the app's
`Info.plist` with an app group shared by both targets.

Set `BAManifestURL` in the app's `Info.plist` to your HTTPS download-manifest URL.
Also update `BADownloadDomainAllowList` to match your server's hostname. The
checked-in `localhost` settings are for testing on the simulator; a physical
device needs a hostname or IP address it can use to reach your Mac or server.

## Package and host the asset pack

Run the commands in this section from this directory:

```sh
xcrun ba-package Greeting.json -o Greeting.aar
```

For local development, follow Apple's
[Testing asset packs locally](https://developer.apple.com/documentation/backgroundassets/testing-asset-packs-locally)
instructions to create and trust an HTTPS certificate. Then run:

```sh
xcrun ba-serve --host localhost Greeting.aar
```

Choose the server certificate when prompted. On the test device, configure the
Background Assets development URL override as described in Apple's guide, using
the server's HTTPS base URL. You must do the certificate and device steps
yourself; the sample doesn't change your trust store or device settings.

To host the pack on your own server instead of `ba-serve`, generate a download
manifest:

```sh
xcrun ba-package download-manifest create Greeting.aar --ios \
  --download-base-url https://assets.example.com/packs \
  -o download-manifest.json
```

Serve `download-manifest.json` at `BAManifestURL` and serve `Greeting.aar` at the
generated pack URL (`https://assets.example.com/packs/Greeting` in this example).
In `Info.plist`, keep `BAHasManagedAssetPacks` set to `true` and
`BAUsesAppleHosting` set to `false`. The size limits in `Info.plist` allow up to
1 MiB of nonessential downloads and no essential downloads; update them if you
change the asset pack.

## Build and run

```sh
dotnet build BackgroundAssetsApp/BackgroundAssetsApp.csproj -r iossimulator-arm64
```

Before installing, check that the built app contains
`Extensions/DownloaderExtension.appex` and not
`PlugIns/DownloaderExtension.appex`. Run the app and tap **Download Greeting**.
The label should display:

```text
Hello from a managed Background Assets pack!
```

When the system asks whether to prefetch the pack, the extension logs
`BackgroundDownloadHandler: allowing asset pack Greeting`. The system decides
when to run the extension; you can't open it like an app. A successful download
after tapping **Download Greeting** doesn't prove that the extension ran.
Check the extension's log as well. The app shows errors on screen and also writes
them to its log.

The downloader matches the `ios-background-download-extension` template. Do not
implement the inherited `IBADownloaderExtension` download callbacks: the system
implements those for managed asset packs, as described in Apple's
[Objective-C downloader documentation](https://developer.apple.com/documentation/backgroundassets/bamanageddownloaderextension).

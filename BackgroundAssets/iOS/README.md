---
name: ".NET for iOS - Background Assets"
description: "Downloads a self-hosted managed asset pack using an ExtensionKit downloader written in C#."
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
belongs in `BackgroundAssetsApp.app/Extensions`, not `PlugIns`. The .NET runtime
initializes the managed registrar before calling `NSExtensionMain`, the same
bootstrap Xcode uses for an Objective-C ExtensionKit principal class. A Swift
`@main` implementation is not needed.

## Requirements

- .NET 10 and the iOS workload with iOS 27 bindings, plus its compatible Xcode.
- iOS 27 or later. The downloader protocol itself requires only iOS 26, but the
  app uses the newer `GetManifestAsync` API.
- A .NET iOS SDK that places managed `EXAppExtensionAttributes` extensions in
  `Extensions` and validates them without requiring an `NSExtension` dictionary.
  Older SDKs may build the standalone extension but cannot package this app.
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
device needs a hostname or IP address that can reach your Mac or server.

## Package and host the asset pack

Run these commands from this directory:

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
the server's HTTPS base URL. These certificate and device-setting steps are
manual; the sample does not change your trust store or settings.

For self-hosting outside the development server, generate a download manifest:

```sh
xcrun ba-package download-manifest create Greeting.aar --ios \
  --download-base-url https://assets.example.com/packs \
  -o download-manifest.json
```

Serve `download-manifest.json` at `BAManifestURL` and serve `Greeting.aar` at the
generated pack URL (`https://assets.example.com/packs/Greeting` in this example).
Keep `BAHasManagedAssetPacks=true` and `BAUsesAppleHosting=false`. The checked-in
download allowances permit up to 1 MiB and no essential downloads; update them if
you change the asset pack.

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

The downloader writes `BackgroundDownloadHandler: allowing asset pack Greeting`
to its log when the system asks whether it should download the prefetch pack.
Background requests are scheduled by the OS, not by opening the extension as an
ordinary app. A successful foreground download alone does not prove that the OS
launched the extension; check the extension log as well. Errors are displayed in
the app and written to its log.

The downloader matches the `ios-background-download-extension` template. Do not
implement the inherited `IBADownloaderExtension` download callbacks: the system
supplies those for managed asset packs, as described in Apple's
[Objective-C downloader documentation](https://developer.apple.com/documentation/backgroundassets/bamanageddownloaderextension).

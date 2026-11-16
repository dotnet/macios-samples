---
name: .NET for macOS - FSKit file system extension
description: "Shows how to write a file system extension for macOS with FSKit in C#."
page_type: sample
languages:
- csharp
products:
- dotnet-macios
urlFragment: fskitextension
---

# FSKit file system extension

This sample shows how to write a file system for macOS in C#, using
[FSKit](https://developer.apple.com/documentation/fskit).

The file system, called HelloFS, is read-only and lives in memory. It has two
text files: `hello.txt` and `about.txt`.

## Projects

The solution (`FSKitExtension.slnx`) has two projects:

* `HelloFS`: the container app. macOS can only install a file system extension
  that's inside an app, so this app exists to ship the extension. It shows a
  window that explains how to use the file system.
* `HelloFSExtension`: the file system extension. It's an app extension
  (`IsAppExtension=true`), and the app references it with
  `<IsAppExtension>true</IsAppExtension>` on its `ProjectReference`.

The extension has three parts:

* `Info.plist`: the `EXAppExtensionAttributes` dictionary tells macOS that this
  is an FSKit module (`EXExtensionPointIdentifier` is `com.apple.fskit.fsmodule`),
  and describes the file system. `FSShortName` is the name you pass to `mount`.
* `Entitlements.plist`: FSKit modules must be sandboxed and have the
  `com.apple.developer.fskit.fsmodule` entitlement.
* The C# code:
    * `HelloFileSystem.cs`: a subclass of `FSUnaryFileSystem`. FSKit asks it
      whether it can handle a resource, and then to create a volume for it.
      The build finds this class (it must be the only subclass of
      `FSUnaryFileSystem` in the project) and generates the native code needed
      to start the extension.
    * `HelloVolume.cs`: a subclass of `FSVolume` that implements the volume
      operations: looking up files, listing the root directory, returning file
      attributes and reading file contents. Operations that would modify the
      volume fail with `EROFS`.
    * `HelloItem.cs`: a file or directory in the volume.

## Requirements

* macOS 15.4 or later.
* A version of .NET for macOS with support for FSKit extensions (.NET 12 or later).
* An Apple Developer account. FSKit extensions can't be signed ad-hoc: they need
  a provisioning profile with the FSKit Module capability.

## Signing

1. Choose a bundle identifier for the app, for example `com.example.hellofs`.
   The extension's bundle identifier must start with the app's bundle identifier,
   by default it's the app's identifier + `.fsmodule`.
2. In the [Apple Developer portal](https://developer.apple.com/account/resources/identifiers/list),
   register an App ID for the extension's bundle identifier, and enable the
   **FSKit Module** capability.
3. Create a macOS development provisioning profile for that App ID, and install it
   (for example in Xcode → Settings → Apple Accounts → Download Manual Profiles).

The app itself doesn't need a provisioning profile, only a signing certificate.

## Build

Pass the bundle identifiers, the signing certificate and the name of the
provisioning profile when building:

```shell
dotnet build HelloFS/HelloFS.csproj \
    -p:HelloFSApplicationId=com.example.hellofs \
    -p:HelloFSExtensionApplicationId=com.example.hellofs.fsmodule \
    -p:CodesignKey="Apple Development: Your Name (XXXXXXXXXX)" \
    -p:HelloFSProvisioningProfile="HelloFS Profile"
```

Instead of passing these properties every time, you can set them in
`Directory.Build.props`.

The extension can also be compiled with NativeAOT:

```shell
dotnet publish HelloFS/HelloFS.csproj -c Release -r osx-arm64 -p:PublishAot=true ...
```

To check that the extension is signed with the FSKit entitlement:

```shell
codesign -d --entitlements - HelloFS/bin/Debug/net12.0-macos/osx-arm64/HelloFS.app/Contents/PlugIns/HelloFSExtension.appex
```

## Run

1. Launch the app once, so macOS registers the extension:

    ```shell
    open HelloFS/bin/Debug/net12.0-macos/osx-arm64/HelloFS.app
    ```

2. Enable the extension in System Settings → General → Login Items & Extensions →
   File System Extensions (the app has a button that opens this page).

3. Mount the file system. HelloFS doesn't look at the contents of the source
   directory, but `mount` needs one:

    ```shell
    mkdir -p /tmp/hellofs-source /tmp/hellofs
    mount -F -t HelloFS /tmp/hellofs-source /tmp/hellofs
    ls -l /tmp/hellofs
    cat /tmp/hellofs/hello.txt
    ```

4. Unmount it when you're done:

    ```shell
    umount /tmp/hellofs
    ```

## Troubleshooting

* `mount` fails with `Module ... is disabled!`: enable the extension in System
  Settings (step 2 above).
* Every time you rebuild the extension, macOS disables it again. After each
  rebuild, launch the app again and re-enable the extension in System Settings.
* After rebuilding, macOS may keep running the previous version of the extension.
  Quit it with `killall HelloFSExtension` before mounting again.
* `mount` fails with `com.apple.extensionKit.errorDomain error 2` and
  `File system named HelloFS not found` right after switching to a build in a
  different location (for example from Debug to Release): macOS is still
  unloading the old extension. Try the `mount` command again.
* `mount` fails with an error about signing or entitlements: check that the
  extension is signed with a provisioning profile that has the FSKit Module
  capability (see [Signing](#signing)).
* `umount` fails with `Resource busy`: something (such as Spotlight or Finder)
  is still looking at the volume. Wait a moment and try again, or use
  `diskutil unmount /tmp/hellofs`.
* Use `pluginkit -m -v -i <extension bundle identifier>` to check that macOS
  found the extension, and Console.app to see its log messages.

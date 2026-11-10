---
name: ".NET for macOS - Bundled XPC service"
description: "Send one local request to a bundled macOS XPC service and receive an asynchronous reply."
page_type: sample
languages:
- csharp
products:
- dotnet-macios
urlFragment: macios-xpcservice
---

# Bundled XPC service

This command-line host sends one string to its bundled XPC service. The service
echoes the string through a reply block. No network connection, launch daemon,
system registration, or file access is needed.

## Requirements

Unlike the other samples, this sample requires the .NET 12 SDK and a .NET for
macOS workload containing the bundled XPC managed-entry-point and proxy-ownership
fixes. Older workloads can build a `.xpc` bundle but start `NSExtensionMain`
instead of the service's managed `Main`. Use the Xcode version required by your
workload and macOS 14 or later.

## Build and run

From this directory:

```sh
dotnet build macOS/Host/Host.csproj -p:RuntimeIdentifier=osx-arm64
./macOS/Host/bin/Debug/net12.0-macos/osx-arm64/Host.app/Contents/MacOS/Host
```

Use `osx-x64` instead of `osx-arm64` on an Intel Mac. Run the executable inside
the complete host bundle, not the service executable on its own. macOS launches
the service on demand.

Expected output:

```text
Sending one request to the bundled XPC service...
XPC reply: Hello from the host
```

The host returns zero only for the expected reply. Connection errors,
interruption, invalidation, an unexpected reply, and a ten-second timeout are
reported to standard error and return a nonzero exit code.

## Project layout

`Contracts` is a binding project, even though no native library is needed. The
binding generator creates the Objective-C protocol, typed remote proxy, and
reply-block marshalling code from `ApiDefinition.cs`. Both executables reference
this same contract. Both use `Registrar=trimmable-static` so the XPC subsystem can
read the reply block's Objective-C type signature. The reply uses a named
delegate so the trimmable static registrar preserves its concrete string
parameter when generating the native protocol.

`EchoService` sets `OutputType=Exe`, `IsAppExtension=true`, and `IsXPCService=true`.
Its manifest declares `CFBundlePackageType=XPC!` and
`XPCService/ServiceType=Application`; it does not declare `NSExtension`.
Its managed entry point retains the listener delegate and calls
`NSXpcListener.ServiceListener.Resume`, which owns the service's run loop.
Each accepted connection retains its exported service object.

`Host` references `EchoService` with `IsAppExtension` project-reference metadata.
The SDK embeds it at:

```text
Host.app/Contents/XPCServices/EchoService.xpc
```

The host connects by the service's bundle identifier,
`com.microsoft.samples.xpcservice.echo`. It keeps the connection and typed proxy
alive through the asynchronous reply, then invalidates the connection and
disposes the proxy. The console host does not run an AppKit event loop, so its
await uses `ConfigureAwait(false)`.

This service exposes only an echo operation. Before adding privileged operations
or sensitive data, define and enforce an appropriate client identity policy.

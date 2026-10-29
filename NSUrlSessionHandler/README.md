---
name: ".NET for iOS - NSUrlSessionHandler networking on Apple platforms"
description: "Explore native HTTP networking, server trust, certificate pinning, client identities, redirects, cookies, uploads, and proxies."
page_type: sample
languages:
- csharp
products:
- dotnet-macios
urlFragment: nsurlsessionhandler
---

# NSUrlSessionHandler

Native iOS, tvOS, macOS, and Mac Catalyst apps use `HttpClient` with an explicit
`Foundation.NSUrlSessionHandler`. A shared runner exercises local HTTPS endpoints,
custom server trust, certificate pinning, managed certificate validation, client
identities, authentication and redirects, cookies, uploads, decompression,
cancellation, and proxies.

Read the companion guide, `docs/building-apps/nsurlsessionhandler.md` in
[dotnet/macios](https://github.com/dotnet/macios),
for API semantics, lifetime rules, security considerations, and workload version
differences. Start with `Shared/NativeTrust.cs` for trust examples and
`Shared/ScenarioRunner.cs` for requests and their expected outcomes.

Screenshots: [iOS](Screenshots/iOS.png), [tvOS](Screenshots/tvOS.png),
[macOS](Screenshots/macOS.png), and [Mac Catalyst](Screenshots/MacCatalyst.png).

## Requirements

Use a Mac with the .NET 10 SDK, the Apple workloads for the platforms you want to
run, and the matching Xcode version. Install workloads with `dotnet workload install
ios tvos macos maccatalyst` if they are not already available.

Use current Apple workload servicing releases for the latest behavior. Merely
targeting `net10.0` does not guarantee the newer handler implementation:

| Result        | Meaning                                                                                                       |
|---------------|---------------------------------------------------------------------------------------------------------------|
| `PASS`        | The observed result matches the current implementation's expected behavior.                                    |
| `FAIL`        | A request or assertion failed; inspect the detail rather than treating a connection failure as a TLS rejection. |
| `LEGACY`      | Older redirect, decompression, or missing-client-identity behavior was observed, not the current behavior.       |
| `UNAVAILABLE` | The installed handler reports `SupportsProxy == false`; its custom-proxy scenarios were not executed.           |

The older redirect behavior can forward broad credentials after a redirect.
The newer behavior is described in [dotnet/macios#25529](https://github.com/dotnet/macios/pull/25529);
decompression header cleanup is described in
[dotnet/macios#24924](https://github.com/dotnet/macios/pull/24924), and custom proxy
support in [dotnet/macios#26858](https://github.com/dotnet/macios/pull/26858).
Do not use a `LEGACY` result as evidence that credential isolation is safe on that
workload. These checks observe behavior directly; they do not infer unrelated
features from `SupportsProxy`.

## Generate fixtures and start the server

From this directory, run:

```sh
dotnet run --project Server -- --setup
dotnet run --project Server
```

Leave the second command running while using the apps. Setup generates a private
development CA, valid and deliberately invalid server certificates, a client
identity, a random development password, and certificate pins under the ignored
`Development/` directory. It refuses to overwrite an existing directory. No
certificate is installed into a system trust store, and the CA's private key is
not persisted.

The apps bundle `Development/Public/`, including the **development-only client
private key and password**. Despite that directory's name, it is not safe to
publish or distribute. Never use production identities here or ship these apps.
Neither `Development/` nor generated `.app` bundles belong in source control.

Server certificates expire after seven days; the CA expires after thirty days.
To regenerate, stop the server, remove only this sample's `Development/` directory,
rerun setup, rebuild every app, and restart the server. Rebuilding is necessary
because the root, client identity, password, and pins are bundled resources.
Removing the generated directory and built apps is sufficient to remove this
sample's certificate material; there is no global trust setting to undo.

## Build and run an app

Open `NSUrlSessionHandler.slnx` in an IDE, select a platform project and device, and
run. Alternatively, change to the corresponding platform directory (`iOS`,
`tvOS`, `MacCatalyst`, or `macOS`) and run:

```sh
dotnet run
```

Run the resulting app and select **Run local networking scenarios**. The tvOS
button can be selected with the remote. Each result includes a name, outcome,
and diagnostic detail. Requests are sequential, and each scenario creates its own
handler so that sessions with different security policies are not reused.

The macOS app also supports a console run without opening its window:

```sh
cd macOS
dotnet run -- --run-tests
```

The exit code is nonzero for `FAIL`. Add `--require-current-behavior` to also fail
for `LEGACY` and `UNAVAILABLE`; this stricter mode is appropriate when validating
a current handler. Neither legacy observations nor unavailable proxy tests are
counted as passes. `--run-on-launch` starts the scenarios automatically in the
graphical apps, for example with `open -a <app-path> --args --run-on-launch` or
`xcrun simctl launch <device-id> <bundle-id> --app-arg=--run-on-launch`.
The iOS and tvOS runtimes require each managed argument to be forwarded with
`--app-arg=`, unlike macOS and Mac Catalyst.
For reproducible screenshots of just the sample view, also pass
`--screenshot=<absolute-png-path>`; the app saves it after the scenarios finish.
The path must be writable by the app; on a sandboxed device use its own container,
or capture the screen with `xcrun simctl io <device-id> screenshot <png-path>`
from the Mac instead.

## Simulators, physical devices, and local networking

The default hostname is `localhost`, suitable for macOS, Mac Catalyst, and Apple
simulators sharing the Mac's loopback interface. A physical iPhone, iPad, or Apple
TV cannot reach the Mac through its own `localhost`.

For a physical device, generate new fixtures with a DNS name or IP address that
the device can resolve and reach, and explicitly opt into LAN listening:

```sh
dotnet run --project Server -- --setup --host=development-mac.local
dotnet run --project Server -- --lan
```

This requires removing existing fixtures first as described above. The hostname
is included in the certificates' subject alternative names and bundled settings;
do not edit just the URL or the hostname check. Rebuild and reinstall the app
after generation. Use normal development signing for physical-device builds.

Grant the app local-network permission when prompted, allow the server through
the Mac's firewall, and use a trusted development network. The projects declare
`NSLocalNetworkUsageDescription` and the narrow `NSAllowsLocalNetworking` ATS
exception, not `NSAllowsArbitraryLoads`. Local-network ATS exceptions do not
disable HTTPS certificate validation.

The server binds only to IPv4 loopback unless `--lan` is supplied. LAN mode
exposes all fixture ports, including an intentionally insecure HTTP endpoint,
invalid TLS identities, and development Basic authentication. Do not run it on
untrusted or public networks.

## Scenarios

| Area                    | What to inspect                                                                                                                     |
|-------------------------|-------------------------------------------------------------------------------------------------------------------------------------|
| Default trust           | The platform rejects the private development CA without a custom callback.                                                           |
| Native trust            | A scoped custom anchor accepts the valid endpoint and rejects expired, wrong-host, and unrelated-CA certificates.                     |
| Certificate pins        | SHA-256 of the whole leaf certificate's DER data accepts primary and backup identities and rejects a mismatch, after trust validation. |
| Managed validation      | A configured custom-root chain and native hostname/TLS check accept the fixture; a rejecting callback fails.                           |
| Native/managed bridge   | Owned native certificate wrappers are converted and disposed; native TLS/hostname checks still precede managed chain checks.           |
| Callback precedence     | The native callback wins when both native and managed callbacks are configured.                                                      |
| Client identities       | Required and optional mTLS endpoints exercise identity supplied/missing; macOS imports its private key as exportable.                  |
| Redirect credentials    | Same-origin and cross-origin redirects test broad credentials versus a `CredentialCache` scoped to the destination origin.            |
| Cookies                 | Round-trip and disabled-cookie cases use an ephemeral session and an explicit container.                                              |
| Uploads                 | Buffered, threshold-boundary, streamed, and unknown-length content verify received byte counts, hashes, and content lengths.            |
| Decompression           | The body is decoded; modern workloads also remove stale compressed headers.                                                          |
| Proxy routing           | HTTP forwarding, HTTPS CONNECT, proxy authentication, disabled proxies, and bypass rules verify observable routing.                    |
| Cancellation            | A delayed request is canceled explicitly, separately from certificate authentication failures.                                        |

Negative TLS tests require an observed rejection from the configured validation
callback, an authentication exception, or a native TLS-domain error code, not
arbitrary HTTP errors or refused connections. Older workloads can report a lost
connection instead of a missing-client-certificate error; that is reported as
`LEGACY`, not a passed TLS check. The readiness scenario must also succeed before
treating a run as healthy.

The managed validation example does **not** blindly accept
`RemoteCertificateChainErrors`: custom managed roots alone do not suppress native
trust errors, so it independently validates the fixture's native TLS policy and
managed chain. Never copy an unconditional `return true` into a validation
callback. The native callback's URL is the original request URL, not a guaranteed
redirect target; this sample only redirects within its controlled fixture host.

Pins are certificate pins, **not SPKI pins**. They change when the leaf
certificate is regenerated. The sample intentionally disables revocation checks
only for its short-lived private development CA with no revocation service.
Keep normal platform trust and revocation policies for production services.

## Fixture endpoints and restricted proxies

| Port | Service                                                       |
|------|---------------------------------------------------------------|
| 5080 | HTTP fixture endpoint                                         |
| 5443 | Valid HTTPS identity                                          |
| 5444 | Valid backup identity / cross-origin redirect destination      |
| 5445 | Expired HTTPS identity                                        |
| 5446 | Wrong-host HTTPS identity                                     |
| 5447 | Identity signed by an unrelated CA                             |
| 5448 | HTTPS requiring a client identity                             |
| 5449 | HTTPS allowing an optional client identity                     |
| 5888 | Anonymous development proxy                                  |
| 5889 | Basic-authenticated development proxy                         |

The proxies are not general-purpose proxies. They accept only GET requests to
the configured hostname on HTTP port 5080 and CONNECT tunnels to HTTPS port
5443, and connect to the Mac's loopback endpoints internally. Other hosts,
schemes, ports, and methods are rejected. They have bounded headers and request
timeouts; the server limits upload bodies to 1 MiB. Proxy counters verify that
successful proxy scenarios actually traversed the proxy rather than going direct.

`NSUrlSessionHandler` supports HTTP proxy endpoints; HTTPS destinations use
CONNECT. Proxy authentication is supported for HTTPS destinations, not ordinary
HTTP forwarding, so the authenticated HTTP test expects 407 rather than success.
The fixture uses Basic authentication only for demonstration and is not a
production authentication or proxy implementation.

## Troubleshooting

If the build requests fixtures, run setup from this directory before building.
If the first scenario cannot connect, check that the server is still running,
the configured hostname matches this Mac, and no other process owns the fixture
ports. A connection refusal is not successful certificate validation.

If expected trust scenarios fail, check certificate expiration and ensure the
app was rebuilt after fixture generation. Regenerating certificates without
rebuilding the app leaves stale roots, pins, and passwords in its bundle.
`LEGACY` or `UNAVAILABLE` means the workload needs updating to exercise current
behavior; changing the target framework string alone is insufficient.

For server-only TLS diagnostics, `Development/Public/root.pem` is the PEM root
certificate suitable for tools such as
`curl --cacert Development/Public/root.pem https://localhost:5443/`. It is not a
client identity and does not enable access to the required-mTLS endpoint.

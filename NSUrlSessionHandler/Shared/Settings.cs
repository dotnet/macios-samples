// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace HandlerSample;

public sealed record Settings (string Host, string Password, string PrimaryPin, string BackupPin) {
	public Uri Endpoint (int port, string path = "/") => new UriBuilder ("https", Host, port, path).Uri;

	public static Settings Load (string directory) =>
		JsonSerializer.Deserialize (File.ReadAllText (Path.Combine (directory, "settings.json")), SampleJsonContext.Default.Settings)
		?? throw new InvalidDataException ("Development settings are empty. Run Server --setup again.");
}

[JsonSerializable (typeof (Settings))]
internal partial class SampleJsonContext : JsonSerializerContext {
}

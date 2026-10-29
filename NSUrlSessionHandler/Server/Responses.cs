// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace HandlerSample;

sealed record EchoResponse (string? Cookie, string Via);
sealed record UploadResponse (long? Length, long Bytes, string Hash);
sealed record ProxyResponse (int Anonymous, int Authenticated);

[JsonSourceGenerationOptions (PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable (typeof (EchoResponse))]
[JsonSerializable (typeof (UploadResponse))]
[JsonSerializable (typeof (ProxyResponse))]
internal partial class ServerJsonContext : JsonSerializerContext {
}

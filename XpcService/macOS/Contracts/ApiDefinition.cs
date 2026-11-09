// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Foundation;

namespace XpcService;

public delegate void EchoReply (string text);

[Protocol (Name = "XpcServiceEchoProtocol"), Model]
[BaseType (typeof (NSObject))]
[XpcInterface]
interface EchoProtocol {
	[Abstract]
	[Export ("echo:reply:")]
	void Echo (string text, EchoReply reply);
}

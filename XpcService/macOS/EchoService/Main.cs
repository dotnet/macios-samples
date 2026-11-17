// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Foundation;

namespace XpcService;

static class Program {
	static void Main ()
	{
		using var listenerDelegate = new ServiceDelegate ();
		using var listener = NSXpcListener.ServiceListener;
		listener.Delegate = listenerDelegate;
		// The service listener's Resume starts its run loop and does not return.
		listener.Resume ();
		GC.KeepAlive (listenerDelegate);
	}
}

sealed class ServiceDelegate : NSXpcListenerDelegate {
	public override bool ShouldAcceptConnection (NSXpcListener listener, NSXpcConnection connection)
	{
		connection.ExportedInterface = NSXpcInterface.Create (typeof (IEchoProtocol));
		connection.ExportedObject = new EchoService ();
		connection.Activate ();
		return true;
	}
}

sealed class EchoService : EchoProtocol {
	public override void Echo (string text, EchoReply reply)
	{
		reply (text);
	}
}

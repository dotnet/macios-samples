// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AppKit;

namespace BonjourChat;

static class Program {
	static AppDelegate? appDelegate;

	static void Main (string [] args)
	{
		NSApplication.Init ();
		NSApplication.SharedApplication.Delegate = appDelegate = new AppDelegate ();
		NSApplication.SharedApplication.Run ();
		GC.KeepAlive (appDelegate);
	}
}

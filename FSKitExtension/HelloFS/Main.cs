// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace HelloFS;

static class MainClass {
	static void Main (string [] args)
	{
		NSApplication.Init ();
		NSApplication.SharedApplication.Delegate = new AppDelegate ();
		NSApplication.SharedApplication.Run ();
	}
}

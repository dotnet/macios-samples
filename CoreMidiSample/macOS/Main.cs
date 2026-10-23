// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using AppKit;

namespace CoreMidiSample;

static class MainClass {
#pragma warning disable 414
	static AppDelegate? app_delegate;
#pragma warning restore 414

	static void Main (string [] args)
	{
		NSApplication.Init ();
		app_delegate = new AppDelegate {
			LaunchArguments = args,
		};
		NSApplication.SharedApplication.Delegate = app_delegate;
		NSApplication.SharedApplication.Run ();
	}
}

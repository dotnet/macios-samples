// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CoreGraphics;
using FSKit;

namespace HelloFS;

// The container app doesn't do anything with the file system itself: its only purpose is to
// ship the file system extension. macOS registers the extension the first time the app is launched.
public class AppDelegate : NSApplicationDelegate {
	NSWindow? window;

	public override void DidFinishLaunching (NSNotification notification)
	{
		var appMenu = new NSMenu ();
		appMenu.AddItem (new NSMenuItem ("Quit HelloFS", new ObjCRuntime.Selector ("terminate:"), "q"));
		var mainMenu = new NSMenu ();
		mainMenu.AddItem (new NSMenuItem { Submenu = appMenu });
		NSApplication.SharedApplication.MainMenu = mainMenu;

		window = new NSWindow (new CGRect (0, 0, 520, 220), NSWindowStyle.Titled | NSWindowStyle.Closable, NSBackingStore.Buffered, false);
		window.Title = "HelloFS";

		var label = NSTextField.CreateWrappingLabel (
			"This app contains the HelloFS file system extension.\n\n" +
			"1. Enable the extension in System Settings → General → Login Items & Extensions → File System Extensions.\n" +
			"2. Mount the file system from Terminal:\n\n" +
			"    mkdir -p /tmp/hellofs\n" +
			"    mount -F -t HelloFS /tmp /tmp/hellofs");
		label.Frame = new CGRect (20, 60, 480, 140);
		label.Selectable = true;

		var button = NSButton.CreateButton ("Open File System Extensions Settings", OpenSettings);
		button.Frame = new CGRect (20, 16, 300, 32);

		window.ContentView!.AddSubview (label);
		window.ContentView.AddSubview (button);
		window.Center ();
		window.MakeKeyAndOrderFront (this);
		NSApplication.SharedApplication.Activate ();
	}

	public override bool ApplicationShouldTerminateAfterLastWindowClosed (NSApplication sender) => true;

	static void OpenSettings ()
	{
		if (OperatingSystem.IsMacOSVersionAtLeast (27, 0) && FSClient.SharedInstance.OpenFileSystemExtensionsSettings ())
			return;
		NSWorkspace.SharedWorkspace.OpenUrl (new NSUrl ("x-apple.systempreferences:com.apple.LoginItems-Settings.extension"));
	}
}

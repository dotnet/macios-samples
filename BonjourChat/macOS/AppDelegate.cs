// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;

namespace BonjourChat;

public sealed class AppDelegate : NSApplicationDelegate {
	readonly BonjourSession session = new BonjourSession ();
	readonly NSPopUpButton peers = new NSPopUpButton (new CGRect (20, 365, 380, 30), false);
	readonly NSButton connect = new NSButton (new CGRect (410, 365, 130, 30)) { Title = "Connect" };
	readonly NSButton start = new NSButton (new CGRect (550, 365, 100, 30)) { Title = "Stop" };
	readonly NSTextField message = new NSTextField (new CGRect (20, 20, 520, 28)) {
		PlaceholderString = "Enter a message (up to 4096 UTF-8 bytes)",
	};
	readonly NSButton send = new NSButton (new CGRect (550, 20, 100, 28)) { Title = "Send" };
	readonly NSTextView transcript = new NSTextView (new CGRect (0, 0, 610, 300)) {
		Editable = false,
		Selectable = true,
	};
	readonly Queue<string> lines = new Queue<string> ();
	NSWindow? window;

	public override void DidFinishLaunching (NSNotification notification)
	{
		var menu = new NSMenu ();
		var appMenuItem = new NSMenuItem ();
		var appMenu = new NSMenu ();
		appMenu.AddItem (new NSMenuItem ("Quit Bonjour Chat", "q",
			(_, _) => NSApplication.SharedApplication.Terminate (this)));
		menu.AddItem (appMenuItem);
		menu.SetSubmenu (appMenu, appMenuItem);
		var editMenuItem = new NSMenuItem { Title = "Edit" };
		var editMenu = new NSMenu ("Edit");
		editMenu.AddItem (new NSMenuItem ("Cut", new Selector ("cut:"), "x"));
		editMenu.AddItem (new NSMenuItem ("Copy", new Selector ("copy:"), "c"));
		editMenu.AddItem (new NSMenuItem ("Paste", new Selector ("paste:"), "v"));
		editMenu.AddItem (new NSMenuItem ("Select All", new Selector ("selectAll:"), "a"));
		menu.AddItem (editMenuItem);
		menu.SetSubmenu (editMenu, editMenuItem);
		NSApplication.SharedApplication.MainMenu = menu;

		window = new NSWindow (new CGRect (0, 0, 670, 470),
			NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable,
			NSBackingStore.Buffered, false) { Title = $"Bonjour Chat - {session.ServiceName}" };
		var content = window.ContentView!;
		var identity = NSTextField.CreateLabel ($"Your service: {session.ServiceName}");
		identity.Frame = new CGRect (20, 430, 630, 22);
		content.AddSubview (identity);
		var help = NSTextField.CreateLabel ("Open another instance or use a second Mac on the same local network.");
		help.Frame = new CGRect (20, 403, 630, 22);
		content.AddSubview (help);
		content.AddSubview (peers);
		content.AddSubview (connect);
		content.AddSubview (start);
		var scroll = new NSScrollView (new CGRect (20, 65, 630, 290)) {
			HasVerticalScroller = true,
			BorderType = NSBorderType.BezelBorder,
			DocumentView = transcript,
		};
		transcript.VerticallyResizable = true;
		transcript.HorizontallyResizable = false;
		transcript.TextContainer!.Size = new CGSize (610, float.MaxValue);
		transcript.TextContainer.WidthTracksTextView = true;
		content.AddSubview (scroll);
		content.AddSubview (message);
		content.AddSubview (send);

		connect.Activated += (_, _) => {
			if (session.Busy)
				session.Disconnect ();
			else if (peers.TitleOfSelectedItem is string name)
				session.Connect (name);
		};
		start.Activated += (_, _) => {
			if (session.Running)
				session.Stop ();
			else
				session.Start ();
		};
		send.Activated += (_, _) => Send ();
		message.Activated += (_, _) => Send ();
		session.Log += Append;
		session.Changed += Refresh;
		Append ("Plaintext demo: use only on a trusted network; do not send sensitive information.");
		session.Start ();
		Refresh ();
		window.Center ();
		window.MakeKeyAndOrderFront (null);
		NSApplication.SharedApplication.Activate ();
	}

	void Send ()
	{
		if (session.Send (message.StringValue))
			message.StringValue = "";
	}

	void Refresh ()
	{
		string? selected = peers.TitleOfSelectedItem;
		peers.RemoveAllItems ();
		peers.AddItems (session.PeerNames);
		if (selected is not null && session.PeerNames.Contains (selected, StringComparer.Ordinal))
			peers.SelectItem (selected);
		peers.Enabled = !session.Busy;
		connect.Title = session.Busy ? "Disconnect" : "Connect";
		connect.Enabled = session.Busy || peers.ItemCount > 0;
		start.Title = session.Running ? "Stop" : "Start";
		send.Enabled = session.CanSend;
		message.Enabled = session.Connected;
	}

	void Append (string text)
	{
		lines.Enqueue (text);
		while (lines.Count > 200)
			lines.Dequeue ();
		transcript.Value = string.Join ("\n", lines) + "\n";
		transcript.ScrollRangeToVisible (new NSRange (transcript.Value.Length, 0));
	}

	public override bool ApplicationShouldTerminateAfterLastWindowClosed (NSApplication sender) => true;

	public override void WillTerminate (NSNotification notification)
	{
		session.Stop ();
	}
}

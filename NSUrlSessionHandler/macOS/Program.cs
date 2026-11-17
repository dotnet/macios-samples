// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AppKit;
using CoreGraphics;
using Foundation;

namespace HandlerSample;

static class Program {
	static AppDelegate? appDelegate;

	static int Main (string [] args)
	{
		NSApplication.Init ();
		if (args.Contains ("--run-tests")) {
			Console.WriteLine ($"Apple workload: {ObjCRuntime.Constants.Version}");
			var results = Task.Run (() => ScenarioRunner.RunAsync (result => Console.WriteLine (result))).GetAwaiter ().GetResult ();
			Console.WriteLine (ScenarioRunner.Summary (results));
			return results.Any (result => result.Outcome == "FAIL"
				|| (args.Contains ("--require-current-behavior") && result.Outcome is "LEGACY" or "UNAVAILABLE")) ? 1 : 0;
		}
		NSApplication.SharedApplication.Delegate = appDelegate = new AppDelegate { RunOnLaunch = args.Contains ("--run-on-launch") };
		NSApplication.SharedApplication.Run ();
		return 0;
	}
}

[Register ("AppDelegate")]
sealed class AppDelegate : NSApplicationDelegate {
	NSWindow? window;
	public bool RunOnLaunch { get; init; }

	public override void DidFinishLaunching (NSNotification notification)
	{
		window = new NSWindow (new CGRect (0, 0, 960, 720),
			NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Resizable,
			NSBackingStore.Buffered, false) { Title = "NSUrlSessionHandler - macOS" };
		var view = window.ContentView ?? throw new InvalidOperationException ("The window has no content view.");
		var run = new NSButton (new CGRect (20, 665, 250, 32)) {
			Title = "Run local networking scenarios", BezelStyle = NSBezelStyle.Rounded,
			AutoresizingMask = NSViewResizingMask.MinYMargin,
		};
		var scroll = new NSScrollView (new CGRect (20, 20, 920, 630)) {
			HasVerticalScroller = true,
			AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
		};
		var output = new NSTextView (new CGRect (0, 0, 900, 630)) {
			Editable = false, Font = NSFont.MonospacedSystemFont (12, NSFontWeight.Regular) ?? throw new InvalidOperationException ("The system font is unavailable."),
			Value = "Start the local Server, then run the scenarios.\nNo production certificates or trust settings are used.\n",
		};
		scroll.DocumentView = output;
		run.Activated += async (_, _) => {
			run.Enabled = false;
			output.Value = $"NSUrlSessionHandler\nApple workload: {ObjCRuntime.Constants.Version}\n\n";
			try {
				var results = await ScenarioRunner.RunAsync (result => {
					output.Value += result + "\n";
					Console.WriteLine (result);
				});
				var summary = ScenarioRunner.Summary (results);
				Console.WriteLine (summary);
				output.Value = summary + "\n\n" + output.Value;
				run.Enabled = true;
				var screenshot = Environment.GetCommandLineArgs ().SingleOrDefault (argument => argument.StartsWith ("--screenshot=", StringComparison.Ordinal));
				if (screenshot is not null) {
					using var bitmap = view.BitmapImageRepForCachingDisplayInRect (view.Bounds)
						?? throw new IOException ("Could not capture the sample view.");
					view.CacheDisplay (view.Bounds, bitmap);
					using var properties = new NSDictionary ();
					using var png = bitmap.RepresentationUsingTypeProperties (NSBitmapImageFileType.Png, properties);
					using var destination = NSUrl.FromFilename (screenshot ["--screenshot=".Length..]);
					if (png is null || !png.Save (destination, false))
						throw new IOException ("Could not save the requested sample screenshot.");
				}
			} catch (Exception error) when (error is IOException or InvalidOperationException or System.Text.Json.JsonException) {
				output.Value += $"SETUP ERROR: {error.Message}";
				Console.Error.WriteLine (error);
			} finally {
				run.Enabled = true;
			}
		};
		view.AddSubview (run);
		view.AddSubview (scroll);
		window.Center ();
		window.MakeKeyAndOrderFront (this);
		if (OperatingSystem.IsMacOSVersionAtLeast (14))
			NSApplication.SharedApplication.Activate ();
		if (RunOnLaunch)
			run.PerformClick (this);
	}
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !MACOS
using CoreGraphics;
using Foundation;
using UIKit;

namespace HandlerSample;

static class Program {
	static void Main (string [] args) => UIApplication.Main (args, null, typeof (AppDelegate));
}

[Register ("AppDelegate")]
sealed class AppDelegate : UIApplicationDelegate {
	public override UISceneConfiguration GetConfiguration (UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options) =>
		new UISceneConfiguration ("Default Configuration", connectingSceneSession.Role) { DelegateType = typeof (SceneDelegate) };
}

[Register ("SceneDelegate")]
sealed class SceneDelegate : UIWindowSceneDelegate {
	public override UIWindow? Window { get; set; }

	public override void WillConnect (UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
	{
		if (scene is not UIWindowScene windowScene)
			throw new InvalidOperationException ("The sample requires a window scene.");
		Window = new UIWindow (windowScene) { RootViewController = new ScenariosViewController () };
		Window.MakeKeyAndVisible ();
	}
}

sealed class ScenariosViewController : UIViewController {
	public override void ViewDidLoad ()
	{
		base.ViewDidLoad ();
		var view = View ?? throw new InvalidOperationException ("The controller has no view.");
#if TVOS
		view.BackgroundColor = UIColor.Black;
#else
		view.BackgroundColor = UIColor.SystemBackground;
#endif
		var run = UIButton.FromType (UIButtonType.System);
		run.SetTitle ("Run local networking scenarios", UIControlState.Normal);
		run.TranslatesAutoresizingMaskIntoConstraints = false;
		var output = new UITextView {
#if !TVOS
			Editable = false,
#else
			TextColor = UIColor.White,
#endif
			Text = "NSUrlSessionHandler\nStart the local Server, then run the scenarios.\nDevelopment certificates only.",
			TranslatesAutoresizingMaskIntoConstraints = false,
		};
		view.AddSubviews (run, output);
		NSLayoutConstraint.ActivateConstraints ([
			run.TopAnchor.ConstraintEqualTo (view.SafeAreaLayoutGuide.TopAnchor, 12),
			run.CenterXAnchor.ConstraintEqualTo (view.CenterXAnchor),
			output.TopAnchor.ConstraintEqualTo (run.BottomAnchor, 12),
			output.LeadingAnchor.ConstraintEqualTo (view.SafeAreaLayoutGuide.LeadingAnchor, 16),
			output.TrailingAnchor.ConstraintEqualTo (view.SafeAreaLayoutGuide.TrailingAnchor, -16),
			output.BottomAnchor.ConstraintEqualTo (view.SafeAreaLayoutGuide.BottomAnchor, -12),
		]);
		run.PrimaryActionTriggered += async (_, _) => {
			run.Enabled = false;
			output.Text = $"NSUrlSessionHandler\nApple workload: {ObjCRuntime.Constants.Version}\n\n";
			try {
				var results = await ScenarioRunner.RunAsync (result => {
					output.Text += result + "\n";
					Console.WriteLine (result);
				});
				var summary = ScenarioRunner.Summary (results);
				Console.WriteLine (summary);
				output.Text = summary + "\n\n" + output.Text;
				run.Enabled = true;
				var screenshot = Environment.GetCommandLineArgs ().SingleOrDefault (argument => argument.StartsWith ("--screenshot=", StringComparison.Ordinal));
				if (screenshot is not null) {
					using var renderer = new UIGraphicsImageRenderer (view.Bounds.Size);
					using var image = renderer.CreateImage (context => view.Layer.RenderInContext (context.CGContext));
					using var png = image.AsPNG ();
					using var destination = NSUrl.FromFilename (screenshot ["--screenshot=".Length..]);
					if (png is null || !png.Save (destination, false))
						throw new IOException ("Could not save the requested sample screenshot.");
				}
			} catch (Exception error) when (error is IOException or InvalidOperationException or System.Text.Json.JsonException) {
				output.Text += $"SETUP ERROR: {error.Message}";
				Console.Error.WriteLine (error);
			} finally {
				run.Enabled = true;
			}
		};
		if (Environment.GetCommandLineArgs ().Contains ("--run-on-launch"))
			run.SendActionForControlEvents (UIControlEvent.PrimaryActionTriggered);
	}
}
#endif

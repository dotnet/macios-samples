// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BackgroundAssets;
using Foundation;
using UIKit;

namespace BackgroundAssetsSample;

public class DownloadViewController : UIViewController {
	readonly UILabel status = new UILabel {
		Lines = 0,
		Text = "Set up HTTPS hosting and the shared app group (see the README), then tap Download Greeting.",
	};
	readonly UIButton download = UIButton.FromType (UIButtonType.System);

	public override void ViewDidLoad ()
	{
		base.ViewDidLoad ();
		var view = View ?? throw new InvalidOperationException ("The view is unavailable.");
		view.BackgroundColor = UIColor.SystemBackground;

		download.SetTitle ("Download Greeting", UIControlState.Normal);
		download.TouchUpInside += async (sender, args) => await DownloadAsync ();
		var stack = new UIStackView ([status, download]) {
			Axis = UILayoutConstraintAxis.Vertical,
			Spacing = 24,
			TranslatesAutoresizingMaskIntoConstraints = false,
		};
		view.AddSubview (stack);
		NSLayoutConstraint.ActivateConstraints ([
			stack.LeadingAnchor.ConstraintEqualTo (view.SafeAreaLayoutGuide.LeadingAnchor, 24),
			stack.TrailingAnchor.ConstraintEqualTo (view.SafeAreaLayoutGuide.TrailingAnchor, -24),
			stack.CenterYAnchor.ConstraintEqualTo (view.SafeAreaLayoutGuide.CenterYAnchor),
		]);
	}

	async Task DownloadAsync ()
	{
		download.Enabled = false;
		status.Text = "Downloading Greeting...";
		try {
			var manager = BAAssetPackManager.SharedManager;
			using var manifest = await manager.GetManifestAsync ();
			using var assetPack = manifest.GetAssetPack ("Greeting")
				?? throw new InvalidOperationException ("The server manifest does not contain the Greeting asset pack.");
			await manager.EnsureLocalAvailabilityAsync (assetPack);

			using var data = manager.GetContents ("Assets/Greeting.txt", "Greeting", NSDataReadingOptions.Mapped, out var error);
			if (error is not null)
				throw new NSErrorException (error);
			if (data is null)
				throw new InvalidOperationException ("Couldn't read the downloaded Greeting file.");
			using var text = NSString.FromData (data, NSStringEncoding.UTF8)
				?? throw new InvalidOperationException ("Greeting.txt is not UTF-8 text.");
			status.Text = text.ToString ();
		} catch (NSErrorException ex) {
			status.Text = $"Download failed: {ex.Message}";
			Console.WriteLine (ex);
		} catch (InvalidOperationException ex) {
			status.Text = ex.Message;
			Console.WriteLine (ex);
		} finally {
			download.Enabled = true;
		}
	}
}

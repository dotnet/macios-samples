namespace AlternateAppIcons;

using System.Runtime.Versioning;

using UserNotifications;

[Register ("AppDelegate")]
public class AppDelegate : UIApplicationDelegate {
	UIColor blue = UIColor.FromRGB (81, 43, 212);
	UIColor green = UIColor.FromRGB (119, 187, 65);
	UILabel? label;
	int badgeCount;

	public override UIWindow? Window {
		get;
		set;
	}

	public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
	{
		// create a new window instance based on the screen size
		Window = new UIWindow (UIScreen.MainScreen.Bounds);

		// create a UIViewController with a single UILabel
		var vc = new UIViewController ();

		var stackView = new UIStackView (Window.Frame);
		stackView.Axis = UILayoutConstraintAxis.Vertical;
		stackView.Distribution = UIStackViewDistribution.FillEqually;

		label = new UILabel () {
			TextColor = blue,
			TextAlignment = UITextAlignment.Center,
			Text = "Hello, App Icon Switcher!",
			AutoresizingMask = UIViewAutoresizing.All,
		};
		stackView.AddArrangedSubview (label);

		var primaryIconButton = UIButton.FromType (UIButtonType.Plain);
		primaryIconButton.SetTitle ("Switch to primary icon", UIControlState.Normal);
		primaryIconButton.SetTitleColor (blue, UIControlState.Normal);
		primaryIconButton.PrimaryActionTriggered += async (sender, args) => await UsePrimaryIconAsync ();
		primaryIconButton.Enabled = UIApplication.SharedApplication.SupportsAlternateIcons;
		stackView.AddArrangedSubview (primaryIconButton);

		var alternateIconButton = UIButton.FromType (UIButtonType.Plain);
		alternateIconButton.SetTitle ("Switch to alternate icon", UIControlState.Normal);
		alternateIconButton.SetTitleColor (green, UIControlState.Normal);
		alternateIconButton.PrimaryActionTriggered += async (sender, args) => await UseAlternateIconAsync ();
		alternateIconButton.Enabled = UIApplication.SharedApplication.SupportsAlternateIcons;
		stackView.AddArrangedSubview (alternateIconButton);

		var incrementBadgeCount = UIButton.FromType (UIButtonType.Plain);
		incrementBadgeCount.SetTitle ("Increment badge count", UIControlState.Normal);
		incrementBadgeCount.PrimaryActionTriggered += (sender, args) => SetBadgeCount (badgeCount + 1);
		incrementBadgeCount.Enabled = UIApplication.SharedApplication.SupportsAlternateIcons;
		stackView.AddArrangedSubview (incrementBadgeCount);

		var decrementBadgeCount = UIButton.FromType (UIButtonType.Plain);
		decrementBadgeCount.SetTitle ("Decrement badge count", UIControlState.Normal);
		decrementBadgeCount.PrimaryActionTriggered += (sender, args) => SetBadgeCount (badgeCount - 1);
		decrementBadgeCount.Enabled = UIApplication.SharedApplication.SupportsAlternateIcons;
		stackView.AddArrangedSubview (decrementBadgeCount);

		var view = vc.View!;
		view.AddSubview (stackView);
		view.UserInteractionEnabled = true;
		Window.RootViewController = vc;

		// make the window visible
		Window.MakeKeyAndVisible ();

		return true;
	}

	void SetBadgeCount (int count)
	{
		if (count >= 0) {
			if (OperatingSystem.IsTvOSVersionAtLeast (16, 0)) {
				UNUserNotificationCenter.Current.RequestAuthorization (UNAuthorizationOptions.Badge, [SupportedOSPlatform ("tvos16.0")] (bool granted, NSError error) => {
					if (granted) {
#pragma warning disable CA1416 //  This call site is reachable on: 'tvOS' 12.2 and later. 'UNUserNotificationCenter.SetBadgeCount(nint, Action<NSError>?)' is only supported on: 'ios' 16.0 and later, 'maccatalyst' 16.0 and later, 'macOS/OSX' 13.0 and later, 'tvos' 16.0 and later.)
						UNUserNotificationCenter.Current.SetBadgeCount (count, (error) => {
#pragma warning restore CA1416
							InvokeOnMainThread (() => {
								if (error is null) {
									label!.Text = $"Updated badge count to {count}";
									badgeCount = count;
								} else {
									label!.Text = $"Updated to update badge count: {error}";
								}
							});
						});
					} else {
						InvokeOnMainThread (() => {
							label!.Text = $"Denied permission to the badge: {error}";
						});
					}
				});
			} else {
				UIApplication.SharedApplication.ApplicationIconBadgeNumber = count;
				badgeCount = count;
			}
		} else {
			label!.Text = $"Can't decrement badge count to below 0.";
		}
	}

	async Task UsePrimaryIconAsync ()
	{
		label!.Text = $"Switching to primary icon...";
		try {
			await UIApplication.SharedApplication.SetAlternateIconNameAsync (null);
			label!.Text = "Using primary icon";
			label!.TextColor = blue;
		} catch (Exception e) {
			label!.Text = $"Failed to switch to primary icon: {e.Message}";
			label!.TextColor = UIColor.Red;
		}
	}

	async Task UseAlternateIconAsync ()
	{
		label!.Text = $"Switching to alternate icon...";
		try {
			await UIApplication.SharedApplication.SetAlternateIconNameAsync ("AlternateAppIcons");
			label!.Text = $"Using alternate icon {UIApplication.SharedApplication.AlternateIconName}";
			label!.TextColor = green;
		} catch (Exception e) {
			label!.Text = $"Failed to switch to alternate icon: {e.Message}";
			label!.TextColor = UIColor.Red;
		}
	}
}

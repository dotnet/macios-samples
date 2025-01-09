using UserNotifications;

namespace NotificationTest;

[Register ("RootViewController")]
public class RootViewController : UIViewController {
	public override void ViewDidLoad ()
	{
		base.ViewDidLoad ();

		if (View == null)
			return;

		View.BackgroundColor = UIColor.Black;

		var button = new UIButton (UIButtonType.Custom) {
			TranslatesAutoresizingMaskIntoConstraints = false,
			BackgroundColor = UIColor.Purple
		};
		button.SetTitle ("Send Local Notification", UIControlState.Normal);
		button.SetTitleColor (UIColor.White, UIControlState.Normal);
		button.TouchUpInside += (sender, e) => {
			var content = new UNMutableNotificationContent () {
				Title = "Hi there",
				Body = "Have a nice day",
				Sound = UNNotificationSound.Default,
				CategoryIdentifier = "general"
			};
			var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger (0.1f, false);
			var request = UNNotificationRequest.FromIdentifier ("notificationTest", content, trigger);
			UNUserNotificationCenter.Current.AddNotificationRequest (request, null);
		};
		View.AddSubview (button);

		button.CenterYAnchor.ConstraintEqualTo (View.CenterYAnchor).Active = true;
		button.LeadingAnchor.ConstraintEqualTo (View.LeadingAnchor).Active = true;
		button.TrailingAnchor.ConstraintEqualTo (View.TrailingAnchor).Active = true;
		button.HeightAnchor.ConstraintEqualTo (50).Active = true;
	}
}

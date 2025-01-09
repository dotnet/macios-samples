using UserNotifications;

namespace NotificationTest;

[Register ("AppDelegate")]
public class AppDelegate : UIApplicationDelegate, IUNUserNotificationCenterDelegate {
	public override UIWindow? Window {
		get;
		set;
	}

	public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
	{
		UNUserNotificationCenter.Current.Delegate = this;

		var dismissAction =
			UNNotificationAction.FromIdentifier ("dismiss", "Dismiss", UNNotificationActionOptions.Destructive);

		var watchCategory = UNNotificationCategory.FromIdentifier ("dismiss",
			new [] { dismissAction }, [], "", UNNotificationCategoryOptions.None);

		var categories = new NSSet<UNNotificationCategory> (watchCategory);
		UNUserNotificationCenter.Current.SetNotificationCategories (categories);

		var authOptions = UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge;
		UNUserNotificationCenter.Current.RequestAuthorization (authOptions, (granted, error) => {
			System.Diagnostics.Debug.WriteLine ($"Notification permission granted: {granted} (error: {error})");
			if (granted)
				UNUserNotificationCenter.Current.Delegate = new NotificationReceiver ();
		});
		UIApplication.SharedApplication.RegisterForRemoteNotifications ();

		Window = new UIWindow (UIScreen.MainScreen.Bounds);
		Window.RootViewController = new RootViewController ();
		Window.MakeKeyAndVisible ();
		return true;
	}

	public void WillPresentNotification (UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
	{
		completionHandler (UNNotificationPresentationOptions.List | UNNotificationPresentationOptions.Banner |
						  UNNotificationPresentationOptions.Sound);
	}

	public override void RegisteredForRemoteNotifications (UIKit.UIApplication application, NSData deviceToken)
	{
		Console.WriteLine ($"UserNotifications.AppDelegate.RegisteredForRemoteNotifications() Device Token: {deviceToken.DebugDescription}");
		Console.WriteLine ($"UserNotifications.AppDelegate.RegisteredForRemoteNotifications() Device Token Hex: {string.Join ("", deviceToken.ToArray ().Select (v => $"{v:x2}"))}");
	}

	public override void FailedToRegisterForRemoteNotifications (UIKit.UIApplication application, NSError error)
	{
		Console.WriteLine ($"UserNotifications.AppDelegate.FailedToRegisterForRemoteNotifications(): {error}");
	}

	public override void DidReceiveRemoteNotification (UIApplication application, NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
	{
		Console.WriteLine ($"UserNotifications.AppDelegate.DidReceiveRemoteNotification ({userInfo})");
		completionHandler (UIBackgroundFetchResult.NewData);
	}
}

public class NotificationReceiver : UNUserNotificationCenterDelegate {
	// Called if app is in the foreground.
	public override void WillPresentNotification (UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
	{
		var presentationOptions = UNNotificationPresentationOptions.Banner;
		Console.WriteLine ($"UserNotifications.NotificationReceiver.WillPresentNotification ({notification}) => {presentationOptions}");
		completionHandler (presentationOptions);
	}

	// Called if app is in the background, or killed state.
	public override void DidReceiveNotificationResponse (UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
	{
		Console.WriteLine ($"UserNotifications.NotificationReceiver.DidReceiveNotificationResponse ({response})");
		completionHandler ();
	}
}

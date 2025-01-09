using System;

using Foundation;
using ObjCRuntime;
using UIKit;
using UserNotifications;

namespace NotificationServiceExtension {
	[Register ("NotificationService")] // this must match the value of the 'NSExtensionPrincipalClass' key in the extension's Info.plist
	public class NotificationService : UNNotificationServiceExtension {
		protected NotificationService (NativeHandle handle) : base (handle)
		{
			// Note: this .ctor should not contain any initialization logic.
		}

		public override void DidReceiveNotificationRequest (UNNotificationRequest request, Action<UNNotificationContent> contentHandler)
		{
			Console.WriteLine ($"UserNotifications.NotificationServiceExtension.DidReceiveNotificationRequest ({request})");

			var newContent = (UNMutableNotificationContent) request.Content.MutableCopy ();

			// Modify the notification content here...
			newContent.Title = $"[modified] {newContent.Title}";

			contentHandler (newContent);
		}

		public override void TimeWillExpire ()
		{
			// Called just before the extension will be terminated by the system.
			// Use this as an opportunity to deliver your "best attempt" at modified content, otherwise the original push payload will be used.
		}
	}
}


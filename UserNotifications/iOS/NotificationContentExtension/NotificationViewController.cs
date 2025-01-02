﻿using System;
using Foundation;
using UIKit;
using UserNotifications;
using UserNotificationsUI;

namespace NotificationTest.NotificationContent
{
	[Register ("NotificationViewController")]
	public class NotificationViewController : UIViewController, IUNNotificationContentExtension
	{
		const int LabelHeight = 88;

		UILabel? notificationLabel;

		protected NotificationViewController (IntPtr handle) : base (handle)
		{
			// Note: this .ctor should not contain any initialization logic.
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();

			Console.WriteLine ($"UserNotifications.NotificationContentExtension.NotificationViewController.ViewDidLoad ()");

			View.BackgroundColor = UIColor.Clear;

			notificationLabel = new UILabel()
			{
				TranslatesAutoresizingMaskIntoConstraints = false,
				Lines = 0,
				TextAlignment = UITextAlignment.Center
			};
			View.AddSubview(notificationLabel);

			notificationLabel.TopAnchor.ConstraintEqualTo(View.TopAnchor).Active = true;
			notificationLabel.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor).Active = true;
			notificationLabel.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor).Active = true;
			notificationLabel.HeightAnchor.ConstraintEqualTo(LabelHeight).Active = true;
		}

		void IUNNotificationContentExtension.DidReceiveNotification(UNNotification notification)
		{
			Console.WriteLine ($"UserNotifications.NotificationContentExtension.NotificationViewController.DidReceiveNotification ({notification})");

			if (notificationLabel is not null) {
				notificationLabel.Text =
					$"➡️ {notification.Request.Content.Title}\n" +
					$"↪️ {notification.Request.Content.Subtitle}\n" +
					$"⏩ {notification.Request.Content.Body}";
			}
		}
	}
}

using UserNotifications;

namespace NotificationTest;

[Register ("RootViewController")]
public class RootViewController : UIViewController
{
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        if (View == null)
            return;
        
        View.BackgroundColor = UIColor.Black;
        
        UIButton button = new UIButton(UIButtonType.Custom)
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            BackgroundColor = UIColor.Purple
        };
        button.SetTitle("Send Local Notification", UIControlState.Normal);
        button.SetTitleColor(UIColor.White, UIControlState.Normal);
        button.TouchUpInside += (sender, e) =>
        {
            var info = new NSMutableDictionary();
            info["imageUrl"] = (NSString)"https://cdn.pixabay.com/photo/2014/06/03/19/38/board-361516_640.jpg";
                
            var content = new UNMutableNotificationContent()
            {
                Title = "Hi there",
                Body = "Have a nice day",
                UserInfo = info,
                Sound = UNNotificationSound.Default,
                CategoryIdentifier = "dismiss"
            };
            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(0.1f, false);
            var request = UNNotificationRequest.FromIdentifier("notificationTest", content, trigger);
            UNUserNotificationCenter.Current.AddNotificationRequest(request, null);
        };
        View.AddSubview(button);
        
        button.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor).Active = true;
        button.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor).Active = true;
        button.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor).Active = true;
        button.HeightAnchor.ConstraintEqualTo(50).Active = true;

    }
}
namespace ComposerIcons;

[Register ("AppDelegate")]
public class AppDelegate : UIApplicationDelegate {

	public override UISceneConfiguration GetConfiguration (UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
	{
		return new UISceneConfiguration ("Default Configuration", connectingSceneSession.Role);
	}
}

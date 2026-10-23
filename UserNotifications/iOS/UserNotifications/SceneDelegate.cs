// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace NotificationTest;

[Register ("SceneDelegate")]
public class SceneDelegate : UIWindowSceneDelegate {

	public override UIWindow? Window { get; set; }

	public override void WillConnect (UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
	{
		if (scene is not UIWindowScene windowScene)
			throw new InvalidOperationException ("Expected a window scene.");

		Window = new UIWindow (windowScene);
		Window.RootViewController = new RootViewController ();
		Window.MakeKeyAndVisible ();
	}
}

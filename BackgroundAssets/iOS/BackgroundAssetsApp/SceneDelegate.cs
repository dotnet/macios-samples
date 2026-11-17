// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Foundation;
using UIKit;

namespace BackgroundAssetsSample;

[Register ("SceneDelegate")]
public class SceneDelegate : UIWindowSceneDelegate {
	public override UIWindow? Window { get; set; }

	public override void WillConnect (UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
	{
		if (scene is not UIWindowScene windowScene)
			throw new InvalidOperationException ("Expected a window scene.");

		Window = new UIWindow (windowScene) {
			RootViewController = new DownloadViewController (),
		};
		Window.MakeKeyAndVisible ();
	}
}

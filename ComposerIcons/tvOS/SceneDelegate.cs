namespace ComposerIcons;

[Register ("SceneDelegate")]
public class SceneDelegate : UIWindowSceneDelegate {

	public override UIWindow? Window { get; set; }

	public override void WillConnect (UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
	{
		if (scene is not UIWindowScene windowScene)
			return;

		Window = new UIWindow (windowScene);

		var vc = new UIViewController ();
		vc.View!.BackgroundColor = UIColor.FromDynamicProvider (tc =>
			tc.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? UIColor.Black : UIColor.White);

		var label = new UILabel () {
			Text = "Composer Icons Sample",
			TextAlignment = UITextAlignment.Center,
			Font = UIFont.PreferredTitle1!,
			TranslatesAutoresizingMaskIntoConstraints = false,
		};
		vc.View.AddSubview (label);

		var description = new UILabel () {
			Text = "This app uses an Icon Composer (.icon) file\nfor its app icon instead of a traditional\n.xcassets asset catalog.",
			TextAlignment = UITextAlignment.Center,
			Lines = 0,
			Font = UIFont.PreferredBody!,
			TextColor = UIColor.SecondaryLabel!,
			TranslatesAutoresizingMaskIntoConstraints = false,
		};
		vc.View.AddSubview (description);

		NSLayoutConstraint.ActivateConstraints ([
			label.CenterXAnchor.ConstraintEqualTo (vc.View.CenterXAnchor),
			label.CenterYAnchor.ConstraintEqualTo (vc.View.CenterYAnchor, -40),
			description.CenterXAnchor.ConstraintEqualTo (vc.View.CenterXAnchor),
			description.TopAnchor.ConstraintEqualTo (label.BottomAnchor, 20),
			description.LeadingAnchor.ConstraintGreaterThanOrEqualTo (vc.View.LeadingAnchor, 20),
			description.TrailingAnchor.ConstraintLessThanOrEqualTo (vc.View.TrailingAnchor, -20),
		]);

		Window.RootViewController = vc;
		Window.MakeKeyAndVisible ();
	}
}

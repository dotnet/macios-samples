using CoreGraphics;

namespace ComposerIcons;

[Register ("AppDelegate")]
public class AppDelegate : NSApplicationDelegate {
	NSWindow? window;

	public override void DidFinishLaunching (NSNotification notification)
	{
		window = new NSWindow (
			new CGRect (0, 0, 480, 300),
			NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable,
			NSBackingStore.Buffered,
			false);
		window.Title = "Composer Icons Sample";

		var contentView = window.ContentView!;

		var label = new NSTextField () {
			StringValue = "Composer Icons Sample",
			Alignment = NSTextAlignment.Center,
			Font = NSFont.BoldSystemFontOfSize (24)!,
			Bezeled = false,
			DrawsBackground = false,
			Editable = false,
			Selectable = false,
			TranslatesAutoresizingMaskIntoConstraints = false,
		};
		contentView.AddSubview (label);

		var description = new NSTextField () {
			StringValue = "This app uses an Icon Composer (.icon) file\nfor its app icon instead of a traditional\n.xcassets asset catalog.",
			Alignment = NSTextAlignment.Center,
			Font = NSFont.SystemFontOfSize (14)!,
			TextColor = NSColor.SecondaryLabel,
			Bezeled = false,
			DrawsBackground = false,
			Editable = false,
			Selectable = false,
			MaximumNumberOfLines = 0,
			TranslatesAutoresizingMaskIntoConstraints = false,
		};
		contentView.AddSubview (description);

		NSLayoutConstraint.ActivateConstraints ([
			label.CenterXAnchor.ConstraintEqualTo (contentView.CenterXAnchor),
			label.CenterYAnchor.ConstraintEqualTo (contentView.CenterYAnchor, 20),
			description.CenterXAnchor.ConstraintEqualTo (contentView.CenterXAnchor),
			description.TopAnchor.ConstraintEqualTo (label.BottomAnchor, 20),
			description.LeadingAnchor.ConstraintGreaterThanOrEqualTo (contentView.LeadingAnchor, 20),
			description.TrailingAnchor.ConstraintLessThanOrEqualTo (contentView.TrailingAnchor, -20),
		]);

		window.Center ();
		window.MakeKeyAndOrderFront (this);
	}
}

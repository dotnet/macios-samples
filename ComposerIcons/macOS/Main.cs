namespace ComposerIcons;

static class MainClass {
	static AppDelegate? app_delegate;

	static void Main (string [] args)
	{
		NSApplication.Init ();
		NSApplication.SharedApplication.Delegate = app_delegate = new AppDelegate ();
		NSApplication.SharedApplication.Run ();
	}
}

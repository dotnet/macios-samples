// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ObjCRuntime;

namespace ExceptionMarshaling;

public class AppDelegate : UIApplicationDelegate {
	UIWindow? window;
	UINavigationController? nav;
	MyTableViewController? dvc;

	public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
	{
		window = new UIWindow (UIScreen.MainScreen.Bounds);
		dvc = new RootTableViewController ();
		nav = new UINavigationController (dvc);
		window.RootViewController = nav;
		window.MakeKeyAndVisible ();

		return true;
	}

	class MyTableViewController : UITableViewController, IUITableViewDelegate, IUITableViewDataSource {
		protected UITableView tableView;

		public MyTableViewController ()
			: base (UITableViewStyle.Grouped)
		{
			tableView = new UITableView (UIScreen.MainScreen.Bounds, UITableViewStyle.Grouped);
			tableView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
			tableView.AutosizesSubviews = true;
			tableView.Delegate = this;
			tableView.DataSource = this;
		}

		public override void LoadView ()
		{
			View = tableView;
		}
	}

	class RootTableViewController : MyTableViewController {
		static string [] sections = new [] {
			"Throw Objective-C exception",
			"Throw managed exception",
		};

		public override nint RowsInSection (UITableView tableview, nint section)
		{
			return 1;
		}

		public override nint NumberOfSections (UITableView tableView)
		{
			return sections.Length;
		}

		public override string TitleForHeader (UITableView tableView, nint section)
		{
			return string.Empty;
		}

		public override string TitleForFooter (UITableView tableView, nint section)
		{
			return string.Empty;
		}

		static NSString CellKey = (NSString) "rootCellKey";

		public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
		{
			var cell = tableView.DequeueReusableCell (CellKey);
			if (cell is null) {
				cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);
				cell.SelectionStyle = UITableViewCellSelectionStyle.Blue;
				cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
			}

			var content = cell.DefaultContentConfiguration;
			content.Text = sections [indexPath.Section];
			cell.ContentConfiguration = content;
			return cell;
		}

		public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
		{
			tableView.DeselectRow (indexPath, false);

			var controller = new ModeTableViewController (indexPath.Section == 1);
			var nav = (UINavigationController) ParentViewController!;
			nav.PushViewController (controller, true);
		}
	}

	class ModeTableViewController : MyTableViewController {
		bool isManagedMode; // if not, objective-c mode
		ThreadMode threadMode;

		static string [] threadModes = new []
		{
			"Main thread",
			"Background thread",
			"Threadpool thread",
		};

		static string [] objectiveCExceptionModes = new [] {
			"None",
			"Default",
			"Unwind managed code",
			"Throw managed exception",
			"Abort",
			"Disable",
		};

		static string [] managedExceptionModes = new [] {
			"None",
			"Default",
			"Unwind native code",
			"Throw Objective-C exception",
			"Abort",
			"Disable",
		};

		int SelectedManagedExceptionMode {
			get {
				if (Exceptions.ManagedExceptionMode is null)
					return 0;
				return ((int) Exceptions.ManagedExceptionMode.Value) + 1;
			}
			set {
				if (value == 0)
					Exceptions.ManagedExceptionMode = null;
				else
					Exceptions.ManagedExceptionMode = (MarshalManagedExceptionMode) (value - 1);
			}
		}

		int SelectedObjectiveCExceptionMode {
			get {
				if (Exceptions.ObjectiveCExceptionMode is null)
					return 0;
				return ((int) Exceptions.ObjectiveCExceptionMode.Value) + 1;
			}
			set {
				if (value == 0)
					Exceptions.ObjectiveCExceptionMode = null;
				else
					Exceptions.ObjectiveCExceptionMode = (MarshalObjectiveCExceptionMode) (value - 1);
			}
		}

		public ModeTableViewController (bool isManagedMode)
		{
			this.isManagedMode = isManagedMode;
		}

		public override nint RowsInSection (UITableView tableview, nint section)
		{
			return 1;
		}

		public override nint NumberOfSections (UITableView tableView)
		{
			return 3;
		}

		public override string TitleForHeader (UITableView tableView, nint section)
		{
			switch (section) {
			case 0:
				return isManagedMode ? "Marshal managed exception mode" : "Marshal Objective-C exception mode";
			case 1:
				return "Thread";
			case 2:
				return "Actions";
			default:
				return "?";
			}
		}

		public override string TitleForFooter (UITableView tableView, nint section)
		{
			return string.Empty;
		}

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			tableView.ReloadData ();
		}

		static NSString CellKey = (NSString) "modeKey";

		public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
		{
			var cell = tableView.DequeueReusableCell (CellKey);
			if (cell is null) {
				cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);
				cell.SelectionStyle = UITableViewCellSelectionStyle.Blue;
			}

			var text = "?";
			switch (indexPath.Section) {
			case 0:
				if (isManagedMode) {
					text = managedExceptionModes [SelectedManagedExceptionMode];
				} else {
					text = objectiveCExceptionModes [SelectedObjectiveCExceptionMode];
				}
				cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
				break;
			case 1:
				text = threadModes [(int) threadMode];
				cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
				break;
			case 2:
				text = "Throw";
				cell.Accessory = UITableViewCellAccessory.None;
				break;
			}

			var content = cell.DefaultContentConfiguration;
			content.Text = text;
			cell.ContentConfiguration = content;

			return cell;
		}

		public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
		{
			tableView.DeselectRow (indexPath, false);

			switch (indexPath.Section) {
			case 0: {
				var controller = new SelectItemTableViewController (isManagedMode ? managedExceptionModes : objectiveCExceptionModes);
				controller.SelectedIndex = isManagedMode ? SelectedManagedExceptionMode : SelectedObjectiveCExceptionMode;
				controller.SelectedIndexChanged += (sender, selectedIndex) => {
					if (isManagedMode) {
						SelectedManagedExceptionMode = selectedIndex;
					} else {
						SelectedObjectiveCExceptionMode = selectedIndex;
					}
				};
				var nav = (UINavigationController) ParentViewController!;
				nav.PushViewController (controller, true);
				break;
			}
			case 1: {
				var controller = new SelectItemTableViewController (threadModes);
				controller.SelectedIndex = (int) threadMode;
				controller.SelectedIndexChanged += (sender, selectedIndex) => {
					threadMode = (ThreadMode) selectedIndex;
				};
				var nav = (UINavigationController) ParentViewController!;
				nav.PushViewController (controller, true);
				break;
			}
			case 2:
				if (isManagedMode) {
					Exceptions.ThrowManagedException ((ThreadMode) threadMode);
				} else {
					Exceptions.ThrowObjectiveCException ((ThreadMode) threadMode);
				}
				break;
			default:
				break;
			}
		}
	}

	class SelectItemTableViewController : UITableViewController, IUITableViewDelegate, IUITableViewDataSource {
		UITableView? tableView;
		string [] items;

		public int SelectedIndex = 0;

		public event SelectedIndexChangedHandler? SelectedIndexChanged;
		public delegate void SelectedIndexChangedHandler (SelectItemTableViewController obj, int selectedIndex);

		public SelectItemTableViewController (string [] items)
			: base (UITableViewStyle.Grouped)
		{
			this.items = items;
		}

		public override void LoadView ()
		{
			tableView = new UITableView (UIScreen.MainScreen.Bounds, UITableViewStyle.Grouped);
			tableView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
			tableView.AutosizesSubviews = true;
			tableView.Delegate = this;
			tableView.DataSource = this;
			View = tableView;
		}

		public override nint RowsInSection (UITableView tableview, nint section)
		{
			return items.Length;
		}

		public override nint NumberOfSections (UITableView tableView)
		{
			return 1;
		}

		public override string TitleForHeader (UITableView tableView, nint section)
		{
			return string.Empty;
		}

		public override string TitleForFooter (UITableView tableView, nint section)
		{
			return string.Empty;
		}

		static NSString CellKey = (NSString) "selectItemKey";

		public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
		{
			var cell = tableView.DequeueReusableCell (CellKey);
			if (cell is null)
				cell = new UITableViewCell (UITableViewCellStyle.Default, CellKey);

			var selected = SelectedIndex == indexPath.Row;
			var text = items [indexPath.Row];

			var content = cell.DefaultContentConfiguration;
			content.Text = text;
			cell.ContentConfiguration = content;
			cell.Accessory = selected ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None;

			return cell;
		}

		public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
		{
			var newIndex = indexPath.Row;
			if (newIndex != SelectedIndex) {
				SelectedIndex = newIndex;
				if (SelectedIndexChanged is not null)
					SelectedIndexChanged (this, SelectedIndex);
			}
			tableView.ReloadData ();
		}
	}
}

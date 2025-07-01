using CoreAnimation;
using CoreGraphics;
using Foundation;
using System;

using UIKit;

namespace Skyshobe
{
    public partial class FirstViewController : UIViewController
    {
        public FirstViewController (IntPtr handle) : base (handle)
        {
        }

        UITextField inputField;
        CADisplayLink displayLink;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            View.BackgroundColor = UIColor.Black;

            // 1. Create the input box
            inputField = new UITextField
            {
                Frame = new CGRect(20, View.Bounds.Height - 60, View.Bounds.Width - 40, 40),
                BackgroundColor = UIColor.White,
                TextColor = UIColor.Black,
                Placeholder = "Filter",
                BorderStyle = UITextBorderStyle.RoundedRect,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin
            };

            // 2. Add Done button toolbar
            var toolbar = new UIToolbar(new CGRect(0, 0, View.Frame.Width, 44));
            var flexible = new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace);
            var doneButton = new UIBarButtonItem(UIBarButtonSystemItem.Done, (s, e) =>
            {
                inputField.ResignFirstResponder(); // Dismiss keyboard

                // Apply filter logic here
                AppDelegate.FilterText = inputField.Text;

                // Reload data with new filter
                var hygPath = NSBundle.MainBundle.PathForResource("hyg", "csv");
                var ngcPath = NSBundle.MainBundle.PathForResource("ngc", "csv");
                var constellationPath = NSBundle.MainBundle.PathForResource("ConstellationLines", "csv");

                AppDelegate.SkyDatabase.LoadSkyData(DateTime.Now, hygPath, ngcPath, AppDelegate.FilterText, constellationPath);
            });
            toolbar.Items = new UIBarButtonItem[] { flexible, doneButton };
            inputField.InputAccessoryView = toolbar;

            // 3. Add to view
            View.AddSubview(inputField);


            // Start frame loop
            displayLink = CADisplayLink.Create(() =>
            {
                AppDelegate.FilterText = inputField.Text;
                string current = AppDelegate.FilterText;

                // Optional: Debug log or trigger redraw
                // Console.WriteLine(current);
            });

            displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Default);
        }


        public override void DidReceiveMemoryWarning ()
        {
            base.DidReceiveMemoryWarning ();
            // Release any cached data, images, etc that aren't in use.
        }
    }
}
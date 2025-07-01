using CloudKit;
using Foundation;
using System;
using Telescope_Pointery_Thing;
using UIKit;

namespace Skyshobe
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the
    // User Interface of the application, as well as listening (and optionally responding) to application events from iOS.
    [Register ("AppDelegate")]
    public class AppDelegate : UIResponder, IUIApplicationDelegate {

        public static Database SkyDatabase;
        public static string FilterText { get; set; } = "";

        [Export("window")]
        public UIWindow Window { get; set; }

        [Export("application:didFinishLaunchingWithOptions:")]
        public bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            SkyDatabase = new Database();

            // Get file paths from bundle
            var hygPath = NSBundle.MainBundle.PathForResource("hyg", "csv");
            var ngcPath = NSBundle.MainBundle.PathForResource("ngc", "csv");
            var tlsPath = NSBundle.MainBundle.PathForResource("naif0012", "tls");
            var bspPath = NSBundle.MainBundle.PathForResource("de430", "bsp");
            var constellationPath = NSBundle.MainBundle.PathForResource("ConstellationLines", "csv");

            // Register SPICE files
            CSPICE.furnsh_c(tlsPath);
            CSPICE.furnsh_c(bspPath);

            // Load sky data with constellations
            SkyDatabase.LoadSkyData(DateTime.Now, hygPath, ngcPath, constellationPath);

            return true;
        }


        // UISceneSession Lifecycle

        [Export ("application:configurationForConnectingSceneSession:options:")]
        public UISceneConfiguration GetConfiguration (UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
        {
            // Called when a new scene session is being created.
            // Use this method to select a configuration to create the new scene with.
            return UISceneConfiguration.Create ("Default Configuration", connectingSceneSession.Role);
        }

        [Export ("application:didDiscardSceneSessions:")]
        public void DidDiscardSceneSessions (UIApplication application, NSSet<UISceneSession> sceneSessions)
        {
            // Called when the user discards a scene session.
            // If any sessions were discarded while the application was not running, this will be called shortly after `FinishedLaunching`.
            // Use this method to release any resources that were specific to the discarded scenes, as they will not return.
        }
    }
}


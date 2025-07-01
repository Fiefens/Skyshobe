using System;
using CoreGraphics;
using UIKit;
using Telescope_Pointery_Thing;
using Foundation;
using CoreMotion;
using CoreLocation;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public class SkyView : UIView
{
    Database db;
    CMMotionManager motionManager = new CMMotionManager();
    CMAttitude currentAttitude;

    CLLocationManager locationManager;
    CLLocation currentLocation;

    bool showCenterDot = false;
    UIButton redButton;
    UIButton secondButton;

    static SkyObject LockedTarget = null;
    static bool IsLocked = false;
    static double OffsetYaw = 0;
    static double OffsetPitch = 0;


    public SkyView(CGRect frame, Database database) : base(frame)
    {
        db = database;
        BackgroundColor = UIColor.Black;

        InitLocation();

        motionManager.DeviceMotionUpdateInterval = 1.0 / 60.0; // 60 Hz

        // Red Button
        redButton = new UIButton(new CGRect(10, 30, 30, 30));
        redButton.Layer.CornerRadius = 15;
        redButton.BackgroundColor = UIColor.Red;
        redButton.TouchUpInside += (s, e) =>
        {
            showCenterDot = !showCenterDot;
            SetNeedsDisplay();
        };
        AddSubview(redButton);

        // Second (placeholder) Button
        secondButton = new UIButton(new CGRect(50, 30, 30, 30));
        secondButton.Layer.CornerRadius = 15;
        secondButton.BackgroundColor = UIColor.Gray;
        // Optional: add a tap handler here later


        secondButton.TouchUpInside += async (s, e) =>
        {
            if (IsLocked)
            {
                LockedTarget = null;
                IsLocked = false;
                OffsetYaw = 0;
                OffsetPitch = 0;
                ShowPopup("Tracking Reset", "Target lock removed.");
                return;
            }

            if (currentAttitude == null || db == null)
            {
                ShowPopup("Error", "Sensor or database not ready.");
                return;
            }

            var candidates = new List<SkyObject>();
            candidates.AddRange(db.Planets);
            if (db.Moon != null) candidates.Add(db.Moon);
            if (db.Sun != null) candidates.Add(db.Sun);
            candidates.AddRange(db.Stars);
            candidates.AddRange(db.DeepSkyObjects);

            if (candidates.Count == 0)
            {
                ShowPopup("No Match", "No visible objects to lock onto.");
                return;
            }

            var centerYaw = currentAttitude.Yaw * 180.0 / Math.PI;
            var centerPitch = currentAttitude.Pitch * 180.0 / Math.PI;

            SkyObject closest = null;
            double closestDist = double.MaxValue;

            foreach (var obj in candidates)
            {
                var (alt, az) = RaDecToAltAz(obj.RA, obj.Dec,
                    currentLocation.Coordinate.Latitude,
                    currentLocation.Coordinate.Longitude,
                    DateTime.UtcNow);

                double dAz = az - centerYaw;
                double dAlt = alt - centerPitch;
                double dist = dAz * dAz + dAlt * dAlt;

                if (dist < closestDist)
                {
                    closest = obj;
                    closestDist = dist;
                }
            }

            if (closest == null)
            {
                ShowPopup("No Target", "Could not find the closest object. Lower your search filter?");
                return;
            }

            ShowPopup("Attach Phone To Telescope", $"If telescope is pointed at {closest.Name}, phone will adjust to telescope view.");

            await Task.Delay(10000);

            if (currentAttitude == null)
            {
                ShowPopup("Cancelled", "Motion data lost.");
                return;
            }

            var (targetAlt, targetAz) = RaDecToAltAz(closest.RA, closest.Dec,
                currentLocation.Coordinate.Latitude,
                currentLocation.Coordinate.Longitude,
                DateTime.UtcNow);

            var yawNow = currentAttitude.Yaw * 180.0 / Math.PI;
            var pitchNow = currentAttitude.Pitch * 180.0 / Math.PI;

            OffsetYaw = targetAz - yawNow;
            OffsetPitch = targetAlt - pitchNow;

            LockedTarget = closest;
            IsLocked = true;

            ShowPopup("Target Locked", $"{closest.Name} is now centered.");
        };

        void ShowPopup(string title, string message)
        {
            var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));

            var vc = UIApplication.SharedApplication.KeyWindow.RootViewController;
            while (vc.PresentedViewController != null)
                vc = vc.PresentedViewController;

            vc.PresentViewController(alert, true, null);
        }



        AddSubview(secondButton);





        motionManager.StartDeviceMotionUpdates(NSOperationQueue.CurrentQueue, (data, error) =>
        {
            if (data != null)
            {
                currentAttitude = data.Attitude;
                SetNeedsDisplay(); // trigger redraw
            }
        });

    }

    // Returns altitude and azimuth in degrees
    public static (double alt, double az) RaDecToAltAz(
        double raDeg, double decDeg,
        double latDeg, double lonDeg,
        DateTime utcTime)
    {
        double latRad = latDeg * Math.PI / 180.0;
        double decRad = decDeg * Math.PI / 180.0;
        double raRad = raDeg * Math.PI / 180.0;

        // Calculate Local Sidereal Time (LST)
        double jd = 367 * utcTime.Year
            - (int)((7 * (utcTime.Year + (int)((utcTime.Month + 9) / 12))) / 4)
            + (int)((275 * utcTime.Month) / 9)
            + utcTime.Day + 1721013.5
            + (utcTime.Hour + utcTime.Minute / 60.0 + utcTime.Second / 3600.0) / 24.0
            - 0.5 * Math.Sign(100 * utcTime.Year + utcTime.Month - 190002.5) + 0.5;

        double d = jd - 2451545.0;
        double gmst = 280.46061837 + 360.98564736629 * d;
        double lst = (gmst + lonDeg) % 360;
        if (lst < 0) lst += 360;

        double haRad = ((lst - raDeg) % 360) * Math.PI / 180.0;
        if (haRad < -Math.PI) haRad += 2 * Math.PI;
        if (haRad > Math.PI) haRad -= 2 * Math.PI;

        double sinAlt = Math.Sin(decRad) * Math.Sin(latRad) + Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad);
        double alt = Math.Asin(sinAlt);

        double cosAz = (Math.Sin(decRad) - Math.Sin(alt) * Math.Sin(latRad)) / (Math.Cos(alt) * Math.Cos(latRad));
        double az = Math.Acos(Math.Clamp(cosAz, -1.0, 1.0));

        if (Math.Sin(haRad) > 0)
            az = 2 * Math.PI - az;

        return (alt * 180.0 / Math.PI, az * 180.0 / Math.PI);
    }



    CGPoint Project(double ra, double dec)
    {
        if (currentAttitude == null || currentLocation == null)
            return new CGPoint(-1000, -1000);

        var (alt, az) = RaDecToAltAz(
            ra, dec,
            currentLocation.Coordinate.Latitude,
            currentLocation.Coordinate.Longitude,
            DateTime.UtcNow);

        double deviceAz = currentAttitude.Yaw * 180.0 / Math.PI;
        double deviceAlt = currentAttitude.Pitch * 180.0 / Math.PI;

        double deltaAz = az - deviceAz;
        double deltaAlt = alt - deviceAlt;

        double fovX = 60.0;
        double fovY = 40.0;

        if (Math.Abs(deltaAz) > fovX / 2 || Math.Abs(deltaAlt) > fovY / 2)
            return new CGPoint(-1000, -1000);

        double x = (deltaAz / fovX + 0.5) * Bounds.Width;
        double y = (1.0 - (deltaAlt / fovY + 0.5)) * Bounds.Height;

        return new CGPoint(x, y);
    }

    void InitLocation()
    {
        locationManager = new CLLocationManager();
        locationManager.RequestWhenInUseAuthorization();
        locationManager.DesiredAccuracy = CLLocation.AccuracyBest;

        locationManager.LocationsUpdated += (sender, e) =>
        {
            currentLocation = e.Locations[e.Locations.Count() - 1];
        };

        locationManager.StartUpdatingLocation();
    }

    public override void Draw(CGRect rect)
    {
        base.Draw(rect);
        var ctx = UIGraphics.GetCurrentContext();
        if (ctx == null || db == null) return;

        CGPoint Project(double ra, double dec)
        {
            if (currentAttitude == null || currentLocation == null)
                return new CGPoint(-1000, -1000);

            // Convert celestial coordinates to horizontal (Alt/Az)
            var (alt, az) = RaDecToAltAz(
                ra, dec,
                currentLocation.Coordinate.Latitude,
                currentLocation.Coordinate.Longitude,
                DateTime.UtcNow);

            // Get device pitch/yaw (in degrees)
            double pitch = currentAttitude.Pitch * 180.0 / Math.PI;
            double yaw = currentAttitude.Yaw * 180.0 / Math.PI;

            // Apply offsets if locked
            if (IsLocked)
            {
                pitch += OffsetPitch;
                yaw += OffsetYaw;
            }

            double deltaAz = az - yaw;
            double deltaAlt = alt - pitch;

            // Normalize azimuth delta to [-180, 180]
            while (deltaAz > 180) deltaAz -= 360;
            while (deltaAz < -180) deltaAz += 360;

            double fovX = 60.0;
            double fovY = 40.0;

            if (Math.Abs(deltaAz) > fovX / 2 || Math.Abs(deltaAlt) > fovY / 2)
                return new CGPoint(-1000, -1000); // object out of screen

            double x = (deltaAz / fovX + 0.5) * Bounds.Width;
            double y = (1.0 - (deltaAlt / fovY + 0.5)) * Bounds.Height;

            return new CGPoint(x, y);
        }

        // --- Constellation Lines --- (FIRST BECAUSE WE DONT CARE ABOUT THEM AS MUCH)
        ctx.SetStrokeColor(UIColor.DarkGray.CGColor);
        ctx.SetLineWidth(1);

        foreach (var line in db.ConstellationLines)
        {
            for (int i = 0; i < line.HipSequence.Count - 1; i++)
            {
                int hipA = line.HipSequence[i];
                int hipB = line.HipSequence[i + 1];

                var starA = db.Stars.FirstOrDefault(s => s.HipId == hipA);
                var starB = db.Stars.FirstOrDefault(s => s.HipId == hipB);

                if (starA == null || starB == null)
                    continue;

                var ptA = Project(starA.RA, starA.Dec);
                var ptB = Project(starB.RA, starB.Dec);

                // Skip lines if either point is off-screen
                if (ptA.X < 0 || ptB.X < 0)
                    continue;

                ctx.MoveTo(ptA.X, ptA.Y);
                ctx.AddLineToPoint(ptB.X, ptB.Y);
                ctx.StrokePath();
            }
        }


        // --- Planets ---
        foreach (var planet in db.Planets)
        {
            var pt = Project(planet.RA, planet.Dec);
            float radius = 5f;

            var color = UIColor.FromRGB(planet.Color.R, planet.Color.G, planet.Color.B);
            ctx.SetFillColor(color.CGColor);
            ctx.FillEllipseInRect(new CGRect(pt.X - radius, pt.Y - radius, radius * 2, radius * 2));

            DrawLabel(ctx, planet.Name, pt.X + 8, pt.Y);
        }

        // --- Sun ---
        if (db.Sun != null)
        {
            var pt = Project(db.Sun.RA, db.Sun.Dec);
            ctx.SetFillColor(UIColor.Yellow.CGColor);
            ctx.FillEllipseInRect(new CGRect(pt.X - 8, pt.Y - 8, 16, 16));
            DrawLabel(ctx, "Sun", pt.X + 10, pt.Y);
        }

        // --- Moon ---
        if (db.Moon != null)
        {
            var pt = Project(db.Moon.RA, db.Moon.Dec);
            ctx.SetFillColor(UIColor.LightGray.CGColor);
            ctx.FillEllipseInRect(new CGRect(pt.X - 7, pt.Y - 7, 14, 14));
            DrawLabel(ctx, "Moon", pt.X + 10, pt.Y);
        }

        // --- Deep Sky Objects ---
        foreach (var dso in db.DeepSkyObjects)
        {
            var pt = Project(dso.RA, dso.Dec);
            ctx.SetFillColor(UIColor.Gray.CGColor);
            ctx.FillEllipseInRect(new CGRect(pt.X - 2, pt.Y - 2, 4, 4));
        }

        // --- Stars ---
        foreach (var star in db.Stars)
        {
            var pt = Project(star.RA, star.Dec);
            ctx.SetFillColor(UIColor.White.CGColor);
            ctx.FillEllipseInRect(new CGRect(pt.X - 1, pt.Y - 1, 2, 2));
        }


        if (showCenterDot)
        {
            var ctzx = UIGraphics.GetCurrentContext();
            ctzx.SetFillColor(UIColor.Red.CGColor);
            float size = 4f;
            ctzx.FillEllipseInRect(new CGRect(Bounds.GetMidX() - size / 2, Bounds.GetMidY() - size / 2, size, size));
        }
    }

    private void DrawLabel(CGContext ctx, string text, double x, double y)
    {
        var attr = new UIStringAttributes
        {
            ForegroundColor = UIColor.White,
            Font = UIFont.SystemFontOfSize(12)
        };
        new NSString(text).DrawString(new CGPoint(x, y), attr);
    }

}

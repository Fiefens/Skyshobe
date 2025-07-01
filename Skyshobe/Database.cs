using GameController;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Telescope_Pointery_Thing
{
    // --- Data Models ---
    public class Star : SkyObject
    {
        public double Magnitude;
        public string SpectralType;
        public int HipId;
    }
    public class DeepSkyObject : SkyObject
    {
        public string Type;
        public double Magnitude;
    }


    public class SkyObject
    {
        public string Name;
        public double RA;
        public double Dec;
    }

    public class ConstellationLine
    {
        public string Abbreviation;         // e.g., "And"
        public List<int> HipSequence;       // HIP numbers in drawing order
    }



    public class Planet : SkyObject
    {
        public double DistanceAU;
        public (byte R, byte G, byte B) Color;

        public Planet(string name, double ra, double dec, double distanceAU)
        {
            Name = name;
            RA = ra;
            Dec = dec;
            DistanceAU = distanceAU;
            Color = GetColorForPlanet(name);
        }

        private static (byte R, byte G, byte B) GetColorForPlanet(string name)
        {
            switch (name)
            {
                case "Mercury":
                    return (169, 169, 169); // Gray
                case "Venus":
                    return (218, 165, 32);  // Golden
                case "Earth":
                    return (70, 130, 180);  // Steel Blue
                case "Mars":
                    return (188, 39, 50);   // Reddish
                case "Jupiter":
                    return (205, 133, 63);  // Light Brown
                case "Saturn":
                    return (210, 180, 140); // Tan
                case "Uranus":
                    return (175, 238, 238); // Pale Cyan
                case "Neptune":
                    return (72, 61, 139);   // Dark Blue
                default:
                    return (255, 255, 255); // White fallback
            }
        }

    }

    public class Moon : SkyObject
    {
        public double Phase; // 0 = new, 0.5 = full, 1 = new
    }
    public class Sun : SkyObject
    {
    }

    
    public class Database
    {
        // --- Data Stores ---
        public List<Star> Stars = new List<Star>();
        public List<Planet> Planets = new List<Planet>();
        public Moon Moon;
        public Sun Sun; 
        public List<ConstellationLine> ConstellationLines = new List<ConstellationLine>();

        public List<DeepSkyObject> DeepSkyObjects = new List<DeepSkyObject>();

        // --- Load All Data ---
        public void LoadSkyData(DateTime timestamp, string hygPath, string ngcPath, string filterText = "", string constellationPath = null)
        {
            // Normalize filter
            string normalizedFilter = filterText?.Trim().ToLowerInvariant();

            // --- Load SPICE kernels ---
            CSPICE.furnsh_c("naif0012.tls");
            CSPICE.furnsh_c("de430.bsp");

            // --- Convert time ---
            string utc = timestamp.ToUniversalTime().ToString("yyyy MMM dd HH:mm:ss");
            CSPICE.str2et_c(utc, out double et);

            // --- Load Planets ---
            string[] planetNames = {
        "MERCURY BARYCENTER", "VENUS BARYCENTER", "EARTH BARYCENTER",
        "MARS BARYCENTER", "JUPITER BARYCENTER", "SATURN BARYCENTER",
        "URANUS BARYCENTER", "NEPTUNE BARYCENTER"
    };
            string[] commonNames = {
        "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune"
    };

            for (int i = 0; i < planetNames.Length; i++)
            {
                double[] state = new double[6];
                CSPICE.spkezr_c(planetNames[i], et, "J2000", "LT+S", "EARTH", state, out double lt);
                CSPICE.reclat_c(state, out double radius, out double lon, out double lat);

                Planets.Add(new Planet(
                    commonNames[i],
                    lon * (180.0 / Math.PI),
                    lat * (180.0 / Math.PI),
                    radius / 149597870.7
                ));
            }

            // --- Load Sun ---
            double[] sunState = new double[6];
            CSPICE.spkezr_c("SUN", et, "J2000", "LT+S", "EARTH", sunState, out double sunLt);
            CSPICE.reclat_c(sunState, out double rSun, out double lonSun, out double latSun);
            Sun = new Sun
            {
                RA = lonSun * (180.0 / Math.PI),
                Dec = latSun * (180.0 / Math.PI)
            };

            // --- Load Moon ---
            double[] moonState = new double[6];
            CSPICE.spkezr_c("MOON", et, "J2000", "LT+S", "EARTH", moonState, out double moonLt);
            CSPICE.reclat_c(moonState, out double rMoon, out double lonMoon, out double latMoon);

            // For now, set Phase to 0 as a placeholder
            Moon = new Moon
            {
                RA = lonMoon * (180.0 / Math.PI),
                Dec = latMoon * (180.0 / Math.PI),
                Phase = 0.5
            };


            string[] lines;


            // --- Load Stars if filter is provided ---
            if (!string.IsNullOrEmpty(normalizedFilter) && File.Exists(hygPath))
            {
                lines = File.ReadAllLines(hygPath);
                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length < 16) continue;

                    string name = parts[6];
                    string fallbackName = $"HIP {parts[1]}";
                    string displayName = string.IsNullOrWhiteSpace(name) ? fallbackName : name;

                    if (!displayName.ToLowerInvariant().Contains(normalizedFilter))
                        continue;

                    double.TryParse(parts[7], out double ra);
                    double.TryParse(parts[8], out double dec);
                    double.TryParse(parts[13], out double mag);
                    string spect = parts[15];
                    int.TryParse(parts[1], out int hipId); // column 1 is 'hip'


                    Stars.Add(new Star
                    {
                        HipId = hipId,
                        Name = displayName,
                        RA = ra,
                        Dec = dec,
                        Magnitude = mag,
                        SpectralType = spect
                    });
                }
            }

            //Constellation lines


            if (!File.Exists(constellationPath)) return;

            lines = File.ReadAllLines(constellationPath);

            foreach (var line in lines)
            {
                var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                if (parts.Length < 3) continue;

                var con = new ConstellationLine
                {
                    Abbreviation = parts[0],
                    HipSequence = new List<int>()
                };

                for (int i = 2; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i], out int hip))
                    {
                        con.HipSequence.Add(hip);
                    }
                }

                ConstellationLines.Add(con);
            }


            // --- Load Deep Sky Objects if filter is provided ---
            if (!string.IsNullOrEmpty(normalizedFilter) && File.Exists(ngcPath))
            {
                lines = File.ReadAllLines(ngcPath);
                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length < 5) continue;

                    string name = parts[0];
                    if (!name.ToLowerInvariant().Contains(normalizedFilter))
                        continue;

                    string type = parts[1];
                    double.TryParse(parts[2], out double ra);
                    double.TryParse(parts[3], out double dec);
                    double.TryParse(parts[10], out double mag); // V-Mag

                    DeepSkyObjects.Add(new DeepSkyObject
                    {
                        Name = name,
                        Type = type,
                        RA = ra,
                        Dec = dec,
                        Magnitude = mag
                    });
                }
            }

        }
    }
}

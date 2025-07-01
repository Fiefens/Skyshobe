using System.Runtime.InteropServices;

public static class CSPICE
{
    [DllImport("__Internal")]
    public static extern void furnsh_c(string file);

    [DllImport("__Internal")]
    public static extern void str2et_c(string time, out double et);

    [DllImport("__Internal")]
    public static extern void spkezr_c(
        string target,
        double et,
        string refFrame,
        string abcorr,
        string observer,
        double[] state,
        out double lightTime
    );

    [DllImport("__Internal")]
    public static extern void reclat_c(double[] rectan, out double radius, out double lon, out double lat);
}

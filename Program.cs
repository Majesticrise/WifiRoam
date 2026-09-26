using System;
using System.IO;

namespace WifiRoam;

internal static class Program
{
    private static void Main()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "WifiRoam");
        Directory.CreateDirectory(dir);

        var log = Path.Combine(dir, "startup.log");
        File.AppendAllText(log, $"{DateTime.Now:O} WifiRoam started{Environment.NewLine}");
    }
}

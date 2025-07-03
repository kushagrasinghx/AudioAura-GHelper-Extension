using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using NAudio.CoreAudioApi;

class Program
{
    static string currentMode = "";

    static void Main()
    {
        Console.WriteLine("G-Helper Lighting Controller started...");

        while (true)
        {
            try
            {
                string activeProcess = GetActiveProcessName();
                bool isAudioPlaying = IsAudioPlaying();
                int idleTime = GetIdleTimeSeconds();

                string desiredMode = "AuraStatic";

                if (isAudioPlaying)
                {
                    if (activeProcess.Contains("vlc") || activeProcess.Contains("chrome") || activeProcess.Contains("mpv") || activeProcess.Contains("potplayer"))
                        desiredMode = "AuraBreathe";
                    else
                        desiredMode = "AuraStrobe";
                }
                else if (idleTime > 600)
                {
                    desiredMode = "AuraStatic";
                }

                if (desiredMode != currentMode)
                {
                    Console.WriteLine($"[MODE SWITCH] {currentMode} → {desiredMode} (Process: {activeProcess}, Idle: {idleTime}s)");
                    SetAuraMode(desiredMode);
                    RestartGHelper();
                    currentMode = desiredMode;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }

            Thread.Sleep(5000);
        }
    }

    static bool IsAudioPlaying()
    {
        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return device.AudioMeterInformation.MasterPeakValue > 0.01;
    }

    static string GetActiveProcessName()
    {
        IntPtr hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out uint pid);
        var proc = Process.GetProcessById((int)pid);
        return proc.ProcessName.ToLower();
    }

    static int GetIdleTimeSeconds()
    {
        LASTINPUTINFO lastInput = new() { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref lastInput);
        return (Environment.TickCount - (int)lastInput.dwTime) / 1000;
    }

    static void SetAuraMode(string mode)
    {
        string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper", "config.json");

        if (!File.Exists(configPath))
        {
            Console.WriteLine("[ERROR] G-Helper config.json not found!");
            return;
        }

        string jsonRaw = File.ReadAllText(configPath);
        if (string.IsNullOrWhiteSpace(jsonRaw)) return;

        try
        {
            dynamic? json = JsonConvert.DeserializeObject(jsonRaw);
            if (json == null) return;

            json.aura_mode = mode;

            // Set aura_speed for non-static modes
            if (mode != "AuraStatic")
                json.aura_speed = 2; // 0 = stopped, 1 = slow, 2 = medium, 3 = fast

            string newJson = JsonConvert.SerializeObject(json, Formatting.Indented);
            File.WriteAllText(configPath, newJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to update config.json: {ex.Message}");
        }
    }


    static void RestartGHelper()
    {
        string? gHelperPath = null;

        foreach (var proc in Process.GetProcessesByName("GHelper"))
        {
            try
            {
                gHelperPath = proc.MainModule?.FileName;
                proc.Kill();
            }
            catch
            {
                Console.WriteLine("[WARN] Couldn't access or kill G-Helper process.");
            }
        }

        if (!string.IsNullOrEmpty(gHelperPath) && File.Exists(gHelperPath))
        {
            try
            {
                Process.Start(gHelperPath);
                Console.WriteLine($"[INFO] G-Helper restarted from: {gHelperPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to restart G-Helper: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("[ERROR] G-Helper is not running and path could not be determined.");
        }
    }



    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}

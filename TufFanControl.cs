using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TufFanControl
{
    public enum FanControlMode
    {
        EcAuto = 0,
        FansOff = 1,
        Manual = 2,
        CustomCurve = 3
    }

    public class CurvePoint
    {
        public int Temp;
        public int Percent;
        public CurvePoint(int t, int p) { Temp = t; Percent = p; }
    }

    public class AppConfig
    {
        public FanControlMode Mode = FanControlMode.EcAuto;
        public bool LightMode = false;
        public int ManualCpuPercent = 40;
        public int ManualGpuPercent = 40;
        public bool LinkFans = true;
        public bool SafetyOverrideEnabled = true;
        public int SafetyTempLimit = 86;
        public int Hysteresis = 3;
        public int RampMode = 0; // 0=Smooth, 1=Fast, 2=Instant
        public bool TempSmoothingEnabled = true;
        public int ActivePreset = 0; // 0=ZeroRpm, 1=Silent, 2=Balanced, 3=Turbo, -1=Custom
        public bool RestoreAutoOnExit = true;
        public List<CurvePoint> Curve = new List<CurvePoint>();

        public static AppConfig CreateDefault()
        {
            AppConfig cfg = new AppConfig();
            cfg.SetPresetZeroRpmIdle();
            return cfg;
        }

        public void SetPresetZeroRpmIdle()
        {
            ActivePreset = 0;
            Curve.Clear();
            Curve.Add(new CurvePoint(35, 0));
            Curve.Add(new CurvePoint(45, 0));
            Curve.Add(new CurvePoint(55, 0));
            Curve.Add(new CurvePoint(62, 22));
            Curve.Add(new CurvePoint(70, 35));
            Curve.Add(new CurvePoint(78, 52));
            Curve.Add(new CurvePoint(85, 75));
            Curve.Add(new CurvePoint(92, 100));
        }

        public void SetPresetSilent()
        {
            ActivePreset = 1;
            Curve.Clear();
            Curve.Add(new CurvePoint(35, 0));
            Curve.Add(new CurvePoint(50, 0));
            Curve.Add(new CurvePoint(60, 0));
            Curve.Add(new CurvePoint(68, 20));
            Curve.Add(new CurvePoint(75, 30));
            Curve.Add(new CurvePoint(82, 45));
            Curve.Add(new CurvePoint(88, 68));
            Curve.Add(new CurvePoint(94, 100));
        }

        public void SetPresetBalanced()
        {
            ActivePreset = 2;
            Curve.Clear();
            Curve.Add(new CurvePoint(35, 18));
            Curve.Add(new CurvePoint(45, 22));
            Curve.Add(new CurvePoint(55, 30));
            Curve.Add(new CurvePoint(65, 42));
            Curve.Add(new CurvePoint(72, 54));
            Curve.Add(new CurvePoint(80, 68));
            Curve.Add(new CurvePoint(86, 84));
            Curve.Add(new CurvePoint(92, 100));
        }

        public void SetPresetTurbo()
        {
            ActivePreset = 3;
            Curve.Clear();
            Curve.Add(new CurvePoint(35, 35));
            Curve.Add(new CurvePoint(45, 45));
            Curve.Add(new CurvePoint(55, 58));
            Curve.Add(new CurvePoint(65, 70));
            Curve.Add(new CurvePoint(72, 82));
            Curve.Add(new CurvePoint(78, 92));
            Curve.Add(new CurvePoint(84, 100));
            Curve.Add(new CurvePoint(92, 100));
        }

        public void Save(string path)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Mode=" + (int)Mode);
                sb.AppendLine("LightMode=" + (LightMode ? "1" : "0"));
                sb.AppendLine("ManualCpuPercent=" + ManualCpuPercent);
                sb.AppendLine("ManualGpuPercent=" + ManualGpuPercent);
                sb.AppendLine("LinkFans=" + (LinkFans ? "1" : "0"));
                sb.AppendLine("SafetyOverrideEnabled=" + (SafetyOverrideEnabled ? "1" : "0"));
                sb.AppendLine("SafetyTempLimit=" + SafetyTempLimit);
                sb.AppendLine("Hysteresis=" + Hysteresis);
                sb.AppendLine("RampMode=" + RampMode);
                sb.AppendLine("TempSmoothingEnabled=" + (TempSmoothingEnabled ? "1" : "0"));
                sb.AppendLine("ActivePreset=" + ActivePreset);
                sb.AppendLine("RestoreAutoOnExit=" + (RestoreAutoOnExit ? "1" : "0"));
                List<string> pts = new List<string>();
                for (int i = 0; i < Curve.Count; i++)
                {
                    pts.Add(Curve[i].Temp + ":" + Curve[i].Percent);
                }
                sb.AppendLine("Curve=" + string.Join(",", pts.ToArray()));
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static AppConfig Load(string path)
        {
            AppConfig cfg = CreateDefault();
            try
            {
                if (!File.Exists(path)) return cfg;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    int iv;
                    if (k == "Mode" && int.TryParse(v, out iv) && iv >= 0 && iv <= 3) cfg.Mode = (FanControlMode)iv;
                    else if (k == "LightMode") cfg.LightMode = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                    else if (k == "ManualCpuPercent" && int.TryParse(v, out iv)) cfg.ManualCpuPercent = Clamp(iv, 0, 100);
                    else if (k == "ManualGpuPercent" && int.TryParse(v, out iv)) cfg.ManualGpuPercent = Clamp(iv, 0, 100);
                    else if (k == "LinkFans") cfg.LinkFans = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                    else if (k == "SafetyOverrideEnabled") cfg.SafetyOverrideEnabled = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                    else if (k == "SafetyTempLimit" && int.TryParse(v, out iv)) cfg.SafetyTempLimit = Clamp(iv, 70, 90);
                    else if (k == "Hysteresis" && int.TryParse(v, out iv)) cfg.Hysteresis = Clamp(iv, 1, 8);
                    else if (k == "RampMode" && int.TryParse(v, out iv)) cfg.RampMode = Clamp(iv, 0, 2);
                    else if (k == "TempSmoothingEnabled") cfg.TempSmoothingEnabled = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                    else if (k == "ActivePreset" && int.TryParse(v, out iv)) cfg.ActivePreset = Clamp(iv, -1, 3);
                    else if (k == "RestoreAutoOnExit") cfg.RestoreAutoOnExit = (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                    else if (k == "Curve")
                    {
                        string[] parts = v.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 8)
                        {
                            List<CurvePoint> parsed = new List<CurvePoint>();
                            for (int i = 0; i < parts.Length; i++)
                            {
                                string[] tp = parts[i].Split(':');
                                int t, p;
                                if (tp.Length == 2 && int.TryParse(tp[0], out t) && int.TryParse(tp[1], out p))
                                {
                                    parsed.Add(new CurvePoint(Clamp(t, 30, 100), Clamp(p, 0, 100)));
                                }
                            }
                            if (parsed.Count == 8) cfg.Curve = parsed;
                        }
                    }
                }
            }
            catch { }
            return cfg;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }

    internal struct ThemePalette
    {
        public Color BgWindow;
        public Color BgCard;
        public Color BgSubtle;
        public Color BgHover;
        public Color BorderCard;
        public Color GridLine;
        public Color TextPrimary;
        public Color TextSecondary;
        public Color TextMuted;
        public Color AccentBlue;
        public Color AccentGreen;
        public Color AccentRed;
        public Color AccentAmber;

        public static ThemePalette Get(bool light)
        {
            ThemePalette t = new ThemePalette();
            if (light)
            {
                t.BgWindow = Color.FromArgb(243, 244, 246);
                t.BgCard = Color.FromArgb(255, 255, 255);
                t.BgSubtle = Color.FromArgb(241, 245, 249);
                t.BgHover = Color.FromArgb(226, 232, 240);
                t.BorderCard = Color.FromArgb(226, 232, 240);
                t.GridLine = Color.FromArgb(241, 245, 249);
                t.TextPrimary = Color.FromArgb(15, 23, 42);
                t.TextSecondary = Color.FromArgb(71, 85, 105);
                t.TextMuted = Color.FromArgb(148, 163, 184);
                t.AccentBlue = Color.FromArgb(2, 132, 199);
                t.AccentGreen = Color.FromArgb(5, 150, 105);
                t.AccentRed = Color.FromArgb(225, 29, 72);
                t.AccentAmber = Color.FromArgb(217, 119, 6);
            }
            else
            {
                t.BgWindow = Color.FromArgb(24, 24, 27);
                t.BgCard = Color.FromArgb(35, 35, 40);
                t.BgSubtle = Color.FromArgb(28, 28, 33);
                t.BgHover = Color.FromArgb(48, 48, 56);
                t.BorderCard = Color.FromArgb(52, 52, 60);
                t.GridLine = Color.FromArgb(38, 38, 45);
                t.TextPrimary = Color.FromArgb(244, 244, 245);
                t.TextSecondary = Color.FromArgb(161, 161, 170);
                t.TextMuted = Color.FromArgb(113, 113, 122);
                t.AccentBlue = Color.FromArgb(56, 189, 248);
                t.AccentGreen = Color.FromArgb(16, 185, 129);
                t.AccentRed = Color.FromArgb(244, 63, 94);
                t.AccentAmber = Color.FromArgb(245, 158, 11);
            }
            return t;
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("winmm.dll")]
        public static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        public static extern uint timeEndPeriod(uint uPeriod);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFileW(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode,
            byte[] lpInBuffer, uint nInBufferSize,
            byte[] lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DuplicateTokenEx(
            IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes,
            int ImpersonationLevel, int TokenType, out IntPtr phNewToken);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessWithTokenW(
            IntPtr hToken, int dwLogonFlags, string lpApplicationName,
            StringBuilder lpCommandLine, uint dwCreationFlags, IntPtr lpEnvironment,
            string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);
    }

    internal static class SystemEcWorker
    {
        private const int WorkerHardSafetyTemp = 92;
        private static IntPtr _hDevice = IntPtr.Zero;
        private static readonly object _ecLock = new object();
        private static Mutex _hwMutex;
        private static bool _keepOnExit = false;

        private static void InitHwMutex()
        {
            if (_hwMutex != null) return;
            try
            {
                MutexSecurity sec = new MutexSecurity();
                sec.AddAccessRule(new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    MutexRights.FullControl, AccessControlType.Allow));
                bool createdNew;
                _hwMutex = new Mutex(false, @"Global\TufFanControl_EcHw_Mutex", out createdNew, sec);
            }
            catch
            {
                try { _hwMutex = new Mutex(false, @"Global\TufFanControl_EcHw_Mutex"); } catch { }
            }
        }

        private static bool AcquireHwMutex()
        {
            InitHwMutex();
            if (_hwMutex == null) return false;
            try
            {
                return _hwMutex.WaitOne(2500);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ReleaseHwMutex(bool acquired)
        {
            if (!acquired || _hwMutex == null) return;
            try { _hwMutex.ReleaseMutex(); } catch { }
        }

        private static void CloseDeviceInternal()
        {
            if (_hDevice != IntPtr.Zero && _hDevice != new IntPtr(-1))
            {
                try { NativeMethods.CloseHandle(_hDevice); } catch { }
            }
            _hDevice = IntPtr.Zero;
        }

        private static void EnsureDriverService()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("sc.exe", "start AsusSAIO");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                if (p != null) p.WaitForExit(1200);
            }
            catch { }
        }

        private static bool OpenDevice()
        {
            if (_hDevice != IntPtr.Zero && _hDevice != new IntPtr(-1))
                return true;
            _hDevice = NativeMethods.CreateFileW(
                @"\\.\AsusSAIO", 0xC0000000u, 3u, IntPtr.Zero, 3u, 0x80u, IntPtr.Zero);
            if (_hDevice == IntPtr.Zero || _hDevice == new IntPtr(-1))
            {
                _hDevice = IntPtr.Zero;
                EnsureDriverService();
                _hDevice = NativeMethods.CreateFileW(
                    @"\\.\AsusSAIO", 0xC0000000u, 3u, IntPtr.Zero, 3u, 0x80u, IntPtr.Zero);
            }
            return (_hDevice != IntPtr.Zero && _hDevice != new IntPtr(-1));
        }

        private static bool EcTransaction(byte cmd, byte[] payload, byte readDir, out byte resultByte)
        {
            resultByte = 0;
            lock (_ecLock)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (!OpenDevice()) return false;
                    byte[] buf = new byte[16];
                    buf[0] = cmd;
                    buf[1] = (byte)payload.Length;
                    for (int i = 0; i < payload.Length && i < 8; i++)
                        buf[2 + i] = payload[i];
                    buf[10] = readDir;
                    uint retBytes = 0;
                    bool ok = NativeMethods.DeviceIoControl(
                        _hDevice, 0x80102070u, buf, 16u, buf, 16u, out retBytes, IntPtr.Zero);
                    int status = BitConverter.ToInt32(buf, 12);
                    resultByte = buf[11];
                    if (readDir == 0)
                        Thread.Sleep(16);
                    else
                        Thread.Sleep(5);
                    if (ok && status == 0)
                        return true;
                    CloseDeviceInternal();
                    Thread.Sleep(10);
                }
                return false;
            }
        }

        private static bool ReadReg(byte reg, out byte val)
        {
            return EcTransaction(0xDD, new byte[] { 0x02, reg, 0x00 }, 1, out val);
        }

        private static bool WriteReg(byte reg, byte val)
        {
            byte dummy;
            return EcTransaction(0xDD, new byte[] { 0x82, reg, val }, 0, out dummy);
        }

        private static void SetAutoMode()
        {
            bool hasMut = AcquireHwMutex();
            try
            {
                WriteReg(0x31, 0x00);
            }
            finally
            {
                ReleaseHwMutex(hasMut);
            }
        }

        private static void SetManualDuties(byte duty0, byte duty1)
        {
            bool hasMut = AcquireHwMutex();
            try
            {
                byte[] duties = new byte[] { duty0, duty1 };
                for (byte fan = 0; fan < 2; fan++)
                {
                    WriteReg(0x32, fan);
                    WriteReg(0x31, 0x01);
                    WriteReg(0x35, duties[fan]);
                }
            }
            finally
            {
                ReleaseHwMutex(hasMut);
            }
        }

        // Fast path for ramp steps: assumes manual mode already engaged.
        private static bool WriteDutiesOnly(byte duty0, byte duty1)
        {
            bool hasMut = AcquireHwMutex();
            try
            {
                bool ok0 = WriteReg(0x32, 0) && WriteReg(0x35, duty0);
                bool ok1 = WriteReg(0x32, 1) && WriteReg(0x35, duty1);
                return ok0 && ok1;
            }
            finally
            {
                ReleaseHwMutex(hasMut);
            }
        }

        private static string ReadTelemetryLine()
        {
            bool hasMut = AcquireHwMutex();
            try
            {
                if (!OpenDevice())
                    return "ERR NoDevice";
                byte mode, cpuTemp, gpuTemp;
                if (!ReadReg(0x31, out mode) || !ReadReg(0x00, out cpuTemp) || !ReadReg(0x03, out gpuTemp))
                    return "ERR IoFail";

                // Hardware-level backstop: never allow EC manual mode to stay engaged at or above 92C.
                if (mode != 0 && (cpuTemp >= WorkerHardSafetyTemp || gpuTemp >= WorkerHardSafetyTemp))
                {
                    WriteReg(0x31, 0x00);
                    mode = 0;
                }

                int[] duties = new int[2];
                int[] rpms = new int[2];
                for (byte fan = 0; fan < 2; fan++)
                {
                    if (!WriteReg(0x32, fan)) return "ERR IoFail";
                    byte duty, hi, lo;
                    if (!ReadReg(0x35, out duty) || !ReadReg(0x34, out hi) || !ReadReg(0x33, out lo))
                        return "ERR IoFail";
                    duties[fan] = duty;
                    rpms[fan] = ((int)hi << 8) | (int)lo;
                }
                return string.Format(CultureInfo.InvariantCulture,
                    "OK {0} {1} {2} {3} {4} {5} {6}",
                    mode, cpuTemp, gpuTemp, duties[0], rpms[0], duties[1], rpms[1]);
            }
            finally
            {
                ReleaseHwMutex(hasMut);
            }
        }

        public static void Run(int port, int parentPid, string token)
        {
            try { NativeMethods.timeBeginPeriod(1); } catch { }
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal; } catch { }

            Thread parentWatcher = new Thread(delegate()
            {
                try
                {
                    Process parent = Process.GetProcessById(parentPid);
                    parent.WaitForExit();
                }
                catch { }
                if (!_keepOnExit)
                {
                    try { SetAutoMode(); } catch { }
                }
                CloseDeviceInternal();
                try { NativeMethods.timeEndPeriod(1); } catch { }
                Environment.Exit(0);
            });
            parentWatcher.IsBackground = true;
            parentWatcher.Start();

            try
            {
                using (TcpClient client = new TcpClient("127.0.0.1", port))
                {
                    client.NoDelay = true;
                    using (NetworkStream ns = client.GetStream())
                    {
                        // Dead-man timeout: if UI hangs/stops polling for 12s, exit and restore EC Auto.
                        ns.ReadTimeout = 12000;
                        ns.WriteTimeout = 8000;
                        using (StreamReader sr = new StreamReader(ns, Encoding.UTF8))
                        using (StreamWriter sw = new StreamWriter(ns, new UTF8Encoding(false)) { AutoFlush = true })
                        {
                            sw.WriteLine("HELLO " + token);
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                string[] parts = line.Trim().Split(' ');
                                string cmd = parts[0];
                                if (cmd == "POLL")
                                {
                                    sw.WriteLine(ReadTelemetryLine());
                                }
                                else if (cmd == "AUTO")
                                {
                                    SetAutoMode();
                                    sw.WriteLine(ReadTelemetryLine());
                                }
                                else if (cmd == "MANUAL" && parts.Length >= 3)
                                {
                                    int d0, d1;
                                    if (int.TryParse(parts[1], out d0) && int.TryParse(parts[2], out d1))
                                    {
                                        SetManualDuties((byte)Math.Max(0, Math.Min(255, d0)), (byte)Math.Max(0, Math.Min(255, d1)));
                                    }
                                    sw.WriteLine(ReadTelemetryLine());
                                }
                                else if (cmd == "DUTY" && parts.Length >= 3)
                                {
                                    int d0, d1;
                                    bool ok = false;
                                    if (int.TryParse(parts[1], out d0) && int.TryParse(parts[2], out d1))
                                    {
                                        ok = WriteDutiesOnly((byte)Math.Max(0, Math.Min(255, d0)), (byte)Math.Max(0, Math.Min(255, d1)));
                                    }
                                    sw.WriteLine(ok ? "OKD" : "ERR DutyFail");
                                }
                                else if (cmd == "KEEP_ON_EXIT" && parts.Length >= 2)
                                {
                                    _keepOnExit = (parts[1] == "1");
                                    sw.WriteLine("OK_KEEP");
                                }
                                else if (cmd == "SHUTDOWN")
                                {
                                    bool restore = (parts.Length < 2 || parts[1] == "1");
                                    _keepOnExit = !restore;
                                    if (restore) SetAutoMode();
                                    sw.WriteLine("BYE");
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (!_keepOnExit)
                {
                    try { SetAutoMode(); } catch { }
                }
                CloseDeviceInternal();
                try { NativeMethods.timeEndPeriod(1); } catch { }
            }
        }
    }

    public class EcBridge : IDisposable
    {
        private TcpListener _listener;
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;
        private int _workerPid = 0;
        private bool _keepOnExit = false;
        private bool _disposed = false;
        private DateTime _lastConnectAttemptUtc = DateTime.MinValue;
        private readonly object _ioLock = new object();

        public bool Connected { get; private set; }
        public int ConnectionGeneration { get; private set; }

        public bool Start()
        {
            lock (_ioLock)
            {
                return ConnectInternal();
            }
        }

        public bool EnsureConnected()
        {
            lock (_ioLock)
            {
                if (Connected) return true;
                if (_disposed) return false;
                DateTime now = DateTime.UtcNow;
                if ((now - _lastConnectAttemptUtc).TotalMilliseconds < 1200)
                    return false;
                return ConnectInternal();
            }
        }

        private bool ConnectInternal()
        {
            if (_disposed) return false;
            _lastConnectAttemptUtc = DateTime.UtcNow;
            CleanupConnectionInternal(true);
            try
            {
                EnsureAsusSaioDriverRunning();
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                string token = Guid.NewGuid().ToString("N");
                int myPid = Process.GetCurrentProcess().Id;

                int spawnedPid;
                if (!SpawnSystemWorker(port, myPid, token, out spawnedPid))
                {
                    CleanupConnectionInternal(true);
                    return false;
                }
                _workerPid = spawnedPid;

                IAsyncResult ar = _listener.BeginAcceptTcpClient(null, null);
                if (!ar.AsyncWaitHandle.WaitOne(6000))
                {
                    CleanupConnectionInternal(true);
                    return false;
                }

                _client = _listener.EndAcceptTcpClient(ar);
                try { _listener.Stop(); } catch { }
                _listener = null;

                _client.NoDelay = true;
                NetworkStream ns = _client.GetStream();
                ns.ReadTimeout = 8000;
                ns.WriteTimeout = 8000;
                _reader = new StreamReader(ns, Encoding.UTF8);
                _writer = new StreamWriter(ns, new UTF8Encoding(false)) { AutoFlush = true };

                string hello = _reader.ReadLine();
                if (hello != "HELLO " + token)
                {
                    CleanupConnectionInternal(true);
                    return false;
                }

                _writer.WriteLine("KEEP_ON_EXIT " + (_keepOnExit ? "1" : "0"));
                string keepResp = _reader.ReadLine();
                if (keepResp != "OK_KEEP")
                {
                    CleanupConnectionInternal(true);
                    return false;
                }

                Connected = true;
                ConnectionGeneration++;
                return true;
            }
            catch
            {
                CleanupConnectionInternal(true);
                return false;
            }
        }

        private void CleanupConnectionInternal(bool killWorker)
        {
            Connected = false;
            try { if (_writer != null) _writer.Dispose(); } catch { }
            try { if (_reader != null) _reader.Dispose(); } catch { }
            try { if (_client != null) _client.Close(); } catch { }
            try { if (_listener != null) _listener.Stop(); } catch { }
            _writer = null;
            _reader = null;
            _client = null;
            _listener = null;

            if (killWorker && _workerPid > 0)
            {
                try
                {
                    Process wp = Process.GetProcessById(_workerPid);
                    if (!wp.HasExited) wp.Kill();
                }
                catch { }
                _workerPid = 0;
            }
        }

        private static void EnsureAsusSaioDriverRunning()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("sc.exe", "start AsusSAIO");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                if (p != null) p.WaitForExit(1500);
            }
            catch { }
        }

        private static bool SpawnSystemWorker(int port, int parentPid, string token, out int workerPid)
        {
            workerPid = 0;
            Process[] procs = Process.GetProcessesByName("winlogon");
            if (procs == null || procs.Length == 0) return false;
            uint winlogonPid = (uint)procs[0].Id;

            IntPtr hProc = NativeMethods.OpenProcess(0x0400u, false, winlogonPid);
            if (hProc == IntPtr.Zero) return false;

            IntPtr hTok;
            if (!NativeMethods.OpenProcessToken(hProc, 0x02000000u, out hTok))
            {
                NativeMethods.CloseHandle(hProc);
                return false;
            }

            IntPtr hDup;
            if (!NativeMethods.DuplicateTokenEx(hTok, 0x02000000u, IntPtr.Zero, 2, 1, out hDup))
            {
                NativeMethods.CloseHandle(hTok);
                NativeMethods.CloseHandle(hProc);
                return false;
            }

            NativeMethods.STARTUPINFO si = new NativeMethods.STARTUPINFO();
            si.cb = Marshal.SizeOf(typeof(NativeMethods.STARTUPINFO));
            NativeMethods.PROCESS_INFORMATION pi;
            string exePath = Application.ExecutablePath;
            StringBuilder cmdLine = new StringBuilder(
                string.Format(CultureInfo.InvariantCulture, "\"{0}\" --worker {1} {2} {3}", exePath, port, parentPid, token));

            bool ok = NativeMethods.CreateProcessWithTokenW(
                hDup, 0, null, cmdLine, 0x08000000u, IntPtr.Zero,
                Path.GetDirectoryName(exePath), ref si, out pi);

            if (ok)
            {
                workerPid = pi.dwProcessId;
                NativeMethods.CloseHandle(pi.hProcess);
                NativeMethods.CloseHandle(pi.hThread);
            }
            NativeMethods.CloseHandle(hDup);
            NativeMethods.CloseHandle(hTok);
            NativeMethods.CloseHandle(hProc);
            return ok;
        }

        public bool SendCommand(string cmd, out int ecMode, out int cpuTemp, out int gpuTemp, out int d0, out int rpm0, out int d1, out int rpm1)
        {
            ecMode = 0; cpuTemp = 0; gpuTemp = 0; d0 = 0; rpm0 = 0; d1 = 0; rpm1 = 0;
            lock (_ioLock)
            {
                if (_disposed) return false;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (!Connected && !ConnectInternal())
                        return false;
                    try
                    {
                        _writer.WriteLine(cmd);
                        string resp = _reader.ReadLine();
                        if (!string.IsNullOrEmpty(resp) && resp.StartsWith("OK "))
                        {
                            string[] p = resp.Split(' ');
                            if (p.Length >= 8)
                            {
                                int.TryParse(p[1], out ecMode);
                                int.TryParse(p[2], out cpuTemp);
                                int.TryParse(p[3], out gpuTemp);
                                int.TryParse(p[4], out d0);
                                int.TryParse(p[5], out rpm0);
                                int.TryParse(p[6], out d1);
                                int.TryParse(p[7], out rpm1);
                                return true;
                            }
                        }
                    }
                    catch { }
                    CleanupConnectionInternal(true);
                }
                return false;
            }
        }

        public bool SendDuty(int d0, int d1)
        {
            lock (_ioLock)
            {
                if (_disposed) return false;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (!Connected && !ConnectInternal())
                        return false;
                    try
                    {
                        _writer.WriteLine("DUTY " + d0 + " " + d1);
                        string resp = _reader.ReadLine();
                        if (resp == "OKD")
                            return true;
                    }
                    catch { }
                    CleanupConnectionInternal(true);
                }
                return false;
            }
        }

        public void SetKeepOnExit(bool keep)
        {
            lock (_ioLock)
            {
                _keepOnExit = keep;
                if (!Connected) return;
                try
                {
                    _writer.WriteLine("KEEP_ON_EXIT " + (keep ? "1" : "0"));
                    string resp = _reader.ReadLine();
                    if (resp != "OK_KEEP")
                        CleanupConnectionInternal(true);
                }
                catch
                {
                    CleanupConnectionInternal(true);
                }
            }
        }

        public void Shutdown(bool restoreAuto)
        {
            lock (_ioLock)
            {
                _disposed = true;
                _keepOnExit = !restoreAuto;
                if (!Connected) return;
                try
                {
                    _writer.WriteLine("SHUTDOWN " + (restoreAuto ? "1" : "0"));
                    _reader.ReadLine();
                }
                catch { }
                CleanupConnectionInternal(false);
            }
        }

        public void Dispose()
        {
            lock (_ioLock)
            {
                _disposed = true;
                CleanupConnectionInternal(false);
            }
        }
    }

    public class MainForm : Form
    {
        // Absolute thermal floor: forces EC Auto no matter what the user configured.
        private const int HardSafetyTemp = 92;

        private readonly AppConfig _cfg;
        private readonly string _cfgPath;
        private readonly EcBridge _ec;
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly EventWaitHandle _activateEvent;
        private readonly AutoResetEvent _wakeEvent = new AutoResetEvent(false);
        private Thread _controlThread;
        private Thread _activateThread;
        private volatile bool _closing = false;
        private volatile bool _forceApplyPending = true;
        private int _lastSeenGeneration = -1;
        private DateTime _lastPollUtc = DateTime.MinValue;

        // Live telemetry
        private int _hwEcMode = 0;
        private int _cpuTemp = 50;
        private int _gpuTemp = 45;
        private int _fan0Duty = 0;
        private int _fan0Rpm = 0;
        private int _fan1Duty = 0;
        private int _fan1Rpm = 0;
        private bool _safetyTripped = false;
        private int _lastCurveTempAnchor = -100;
        private int _lastCurvePercent = -1;

        // Smoothing / ramp state
        private double _smoothCpu = -1;
        private double _smoothGpu = -1;
        private int _targetDuty0 = 0;
        private int _targetDuty1 = 0;
        private int _rampDuty0 = 0;
        private int _rampDuty1 = 0;
        private int _lastSentDuty0 = -1;
        private int _lastSentDuty1 = -1;
        private bool _manualEngaged = false;
        private DateTime _lastRampUtc = DateTime.MinValue;

        // View state: false = Curve View, true = Manual Sliders View
        private bool _showManualTab = false;

        // Hit rectangles
        private Rectangle _statusPillRect;
        private Rectangle _themeToggleRect;
        private Rectangle _trayBtnRect;
        private Rectangle[] _modeRects = new Rectangle[4];
        private Rectangle _tabCurveRect;
        private Rectangle _tabManualRect;

        // Manual tab hit rects
        private Rectangle _cpuSliderRect;
        private Rectangle _gpuSliderRect;
        private Rectangle _linkFansRect;
        private Rectangle[] _quickSpeedRects = new Rectangle[5];
        private readonly int[] _quickSpeedValues = new int[] { 0, 25, 50, 75, 100 };

        // Curve tab hit rects
        private Rectangle _curvePlotRect;
        private Rectangle[] _presetRects = new Rectangle[4];
        private readonly string[] _presetNames = new string[] { "0-RPM", "Silent", "Balanced", "Turbo" };

        // Footer safety hit rects
        private Rectangle _safetyToggleRect;
        private Rectangle _safetySliderRect;
        private Rectangle _rampPillRect;
        private Rectangle _restoreOnExitRect;

        // Mouse / drag state
        private int _draggingCurveIdx = -1;
        private bool _draggingCpuSlider = false;
        private bool _draggingGpuSlider = false;
        private bool _draggingSafetySlider = false;
        private Point _mousePos = Point.Empty;

        public MainForm(AppConfig cfg, string cfgPath, EcBridge ec, EventWaitHandle activateEvent)
        {
            _cfg = cfg;
            _cfgPath = cfgPath;
            _ec = ec;
            _activateEvent = activateEvent;
            _showManualTab = (_cfg.Mode == FanControlMode.Manual);

            Text = "G-Fan Control (FX506LH)";
            ClientSize = new Size(500, 496);
            MinimumSize = new Size(500, 496);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            ApplyWindowTheme();

            Icon appIcon = CreateFanIcon(Color.FromArgb(56, 189, 248));
            Icon = appIcon;

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Open Window", null, delegate { ShowFromTray(); });
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Auto (BIOS)", null, delegate { SetMode(FanControlMode.EcAuto); });
            _trayMenu.Items.Add("0 RPM (Off)", null, delegate { SetMode(FanControlMode.FansOff); });
            _trayMenu.Items.Add("Manual Speed", null, delegate { SetMode(FanControlMode.Manual); });
            _trayMenu.Items.Add("Custom Curve", null, delegate { SetMode(FanControlMode.CustomCurve); });
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Exit", null, delegate
            {
                Close();
            });

            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = appIcon;
            _trayIcon.Text = "G-Fan Control";
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += delegate { ShowFromTray(); };

            _ec.SetKeepOnExit(!_cfg.RestoreAutoOnExit);
            _lastSeenGeneration = _ec.ConnectionGeneration;
            _lastRampUtc = DateTime.UtcNow;

            try { SystemEvents.PowerModeChanged += OnPowerModeChanged; } catch { }

            _controlThread = new Thread(ControlLoop);
            _controlThread.IsBackground = true;
            _controlThread.Name = "GFanControlLoop";
            _controlThread.Start();

            if (_activateEvent != null)
            {
                _activateThread = new Thread(ActivateWatcherLoop);
                _activateThread.IsBackground = true;
                _activateThread.Name = "GFanActivateWatcher";
                _activateThread.Start();
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                _manualEngaged = false;
                _lastSentDuty0 = -1;
                _lastSentDuty1 = -1;
                PollAndApplyAsync(true);
            }
        }

        private void ActivateWatcherLoop()
        {
            while (!_closing)
            {
                try
                {
                    if (_activateEvent != null && _activateEvent.WaitOne(1000))
                    {
                        if (_closing) break;
                        if (IsHandleCreated)
                        {
                            BeginInvoke((MethodInvoker)delegate { ShowFromTray(); });
                        }
                    }
                }
                catch { Thread.Sleep(500); }
            }
        }

        private void ApplyWindowTheme()
        {
            ThemePalette t = ThemePalette.Get(_cfg.LightMode);
            BackColor = t.BgWindow;
            ForeColor = t.TextPrimary;
            try
            {
                int dark = _cfg.LightMode ? 0 : 1;
                NativeMethods.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
                NativeMethods.DwmSetWindowAttribute(Handle, 19, ref dark, 4);
                NativeMethods.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0027u);
            }
            catch { }
        }

        private static Icon CreateFanIcon(Color accent)
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(24, 24, 27)))
                    g.FillEllipse(bg, 1, 1, 30, 30);
                using (Pen ring = new Pen(accent, 2.2f))
                    g.DrawEllipse(ring, 2, 2, 28, 28);
                using (SolidBrush blade = new SolidBrush(accent))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        g.TranslateTransform(16, 16);
                        g.RotateTransform(i * 90);
                        g.FillPie(blade, -10, -10, 20, 20, -25, 50);
                        g.ResetTransform();
                    }
                }
                using (SolidBrush hub = new SolidBrush(Color.FromArgb(24, 24, 27)))
                    g.FillEllipse(hub, 12, 12, 8, 8);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void ShowFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized)
                NativeMethods.ShowWindow(Handle, 9);
            WindowState = FormWindowState.Normal;
            Activate();
            NativeMethods.SetForegroundWindow(Handle);
        }

        private void SetMode(FanControlMode newMode)
        {
            _cfg.Mode = newMode;
            if (newMode == FanControlMode.Manual) _showManualTab = true;
            else if (newMode == FanControlMode.CustomCurve) _showManualTab = false;
            _safetyTripped = false;
            _lastCurvePercent = -1;
            _manualEngaged = false;
            _lastSentDuty0 = -1;
            _lastSentDuty1 = -1;
            _rampDuty0 = _fan0Duty;
            _rampDuty1 = _fan1Duty;
            _lastRampUtc = DateTime.UtcNow;
            _cfg.Save(_cfgPath);
            Invalidate();
            PollAndApplyAsync(true);
        }

        private int EvaluateCurvePercent(int currentTemp)
        {
            List<CurvePoint> pts = _cfg.Curve;
            if (pts == null || pts.Count == 0) return 50;

            if (_lastCurvePercent >= 0 && currentTemp < _lastCurveTempAnchor)
            {
                if (_lastCurveTempAnchor - currentTemp < _cfg.Hysteresis)
                    return _lastCurvePercent;
            }

            int result;
            if (currentTemp <= pts[0].Temp)
                result = pts[0].Percent;
            else if (currentTemp >= pts[pts.Count - 1].Temp)
                result = pts[pts.Count - 1].Percent;
            else
            {
                result = pts[pts.Count - 1].Percent;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    CurvePoint a = pts[i];
                    CurvePoint b = pts[i + 1];
                    if (currentTemp >= a.Temp && currentTemp <= b.Temp)
                    {
                        if (b.Temp <= a.Temp)
                            result = b.Percent;
                        else
                        {
                            double ratio = (double)(currentTemp - a.Temp) / (double)(b.Temp - a.Temp);
                            result = (int)Math.Round(a.Percent + ratio * (b.Percent - a.Percent));
                        }
                        break;
                    }
                }
            }

            if (result != _lastCurvePercent)
            {
                _lastCurveTempAnchor = currentTemp;
                _lastCurvePercent = result;
            }
            return Math.Max(0, Math.Min(100, result));
        }

        private void PollAndApplyAsync(bool forceWrite)
        {
            if (forceWrite)
                _forceApplyPending = true;
            try { _wakeEvent.Set(); } catch { }
        }

        private static double SmoothSensor(double smooth, int raw)
        {
            if (smooth < 0) return raw;
            if (raw >= 82 && raw > smooth)
                return smooth + 0.65 * (raw - smooth);
            double alpha = (raw >= smooth) ? 0.45 : 0.22;
            return smooth + alpha * (raw - smooth);
        }

        private void ControlLoop()
        {
            while (!_closing)
            {
                try
                {
                    DateTime now = DateTime.UtcNow;
                    bool forceWrite = _forceApplyPending;
                    _forceApplyPending = false;

                    if (!_ec.Connected)
                    {
                        if (_ec.EnsureConnected())
                        {
                            forceWrite = true;
                        }
                    }

                    if (_ec.ConnectionGeneration != _lastSeenGeneration)
                    {
                        _lastSeenGeneration = _ec.ConnectionGeneration;
                        _manualEngaged = false;
                        _lastSentDuty0 = -1;
                        _lastSentDuty1 = -1;
                        forceWrite = true;
                    }

                    bool dueForPoll = forceWrite || ((now - _lastPollUtc).TotalMilliseconds >= 580);
                    if (dueForPoll && _ec.Connected)
                    {
                        _lastPollUtc = now;
                        PollAndUpdateTargets(forceWrite);
                    }

                    if (_ec.Connected)
                    {
                        StepRampOnControlThread();
                    }

                    if (dueForPoll && !_closing && IsHandleCreated)
                    {
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                if (_closing) return;
                                string modeLabel = !_ec.Connected ? "RECONNECTING" : (_safetyTripped ? "SAFETY" : _cfg.Mode.ToString());
                                string tip = string.Format("CPU {0}°C ({1} RPM) | GPU {2}°C ({3} RPM) [{4}]",
                                    _cpuTemp, _fan0Rpm, _gpuTemp, _fan1Rpm, modeLabel);
                                if (tip.Length > 63) tip = tip.Substring(0, 63);
                                _trayIcon.Text = tip;
                                Invalidate();
                            });
                        }
                        catch { }
                    }
                }
                catch { }

                if (_closing) break;
                _wakeEvent.WaitOne(200);
            }
        }

        private void PollAndUpdateTargets(bool forceWrite)
        {
            int ecMode, cTemp, gTemp, d0, r0, d1, r1;
            if (_ec.SendCommand("POLL", out ecMode, out cTemp, out gTemp, out d0, out r0, out d1, out r1))
            {
                _hwEcMode = ecMode;
                if (cTemp > 0 && cTemp < 130) _cpuTemp = cTemp;
                if (gTemp > 0 && gTemp < 130) _gpuTemp = gTemp;
                _fan0Duty = d0;
                _fan0Rpm = r0;
                _fan1Duty = d1;
                _fan1Rpm = r1;
            }
            else
            {
                return;
            }

            if (_ec.ConnectionGeneration != _lastSeenGeneration)
            {
                _lastSeenGeneration = _ec.ConnectionGeneration;
                _manualEngaged = false;
                _lastSentDuty0 = -1;
                _lastSentDuty1 = -1;
                forceWrite = true;
            }

            // Temperature smoothing for control decisions only (cards still show raw values).
            if (_cfg.TempSmoothingEnabled && _cfg.RampMode != 2)
            {
                _smoothCpu = SmoothSensor(_smoothCpu, _cpuTemp);
                _smoothGpu = SmoothSensor(_smoothGpu, _gpuTemp);
            }
            else
            {
                _smoothCpu = _cpuTemp;
                _smoothGpu = _gpuTemp;
            }

            int rawMax = Math.Max(_cpuTemp, _gpuTemp);
            int smoothMax = (int)Math.Round(Math.Max(_smoothCpu, _smoothGpu));

            // Safety always watches the RAW temperature for the fastest possible reaction.
            // HardSafetyTemp is an absolute floor that applies even when the user disables safety.
            int limit = _cfg.SafetyOverrideEnabled ? _cfg.SafetyTempLimit : HardSafetyTemp;
            if (_cfg.Mode != FanControlMode.EcAuto)
            {
                if (rawMax >= limit)
                    _safetyTripped = true;
                else if (_safetyTripped && rawMax <= limit - 5)
                {
                    _safetyTripped = false;
                    forceWrite = true;
                }
            }
            else
            {
                _safetyTripped = false;
            }

            if (_safetyTripped || _cfg.Mode == FanControlMode.EcAuto)
            {
                if (_hwEcMode != 0 || forceWrite)
                {
                    if (_ec.SendCommand("AUTO", out ecMode, out cTemp, out gTemp, out d0, out r0, out d1, out r1))
                    {
                        _hwEcMode = ecMode; _fan0Duty = d0; _fan0Rpm = r0; _fan1Duty = d1; _fan1Rpm = r1;
                        _rampDuty0 = d0;
                        _rampDuty1 = d1;
                    }
                    _manualEngaged = false;
                    _lastSentDuty0 = -1;
                    _lastSentDuty1 = -1;
                }
            }
            else
            {
                // Compute the target duty for this mode, then let StepRampOnControlThread glide to it.
                if (_cfg.Mode == FanControlMode.FansOff)
                {
                    _targetDuty0 = 0;
                    _targetDuty1 = 0;
                }
                else if (_cfg.Mode == FanControlMode.Manual)
                {
                    _targetDuty0 = (int)Math.Round(_cfg.ManualCpuPercent * 255.0 / 100.0);
                    _targetDuty1 = (int)Math.Round((_cfg.LinkFans ? _cfg.ManualCpuPercent : _cfg.ManualGpuPercent) * 255.0 / 100.0);
                }
                else
                {
                    int pct = EvaluateCurvePercent(smoothMax);
                    int td = (int)Math.Round(pct * 255.0 / 100.0);
                    _targetDuty0 = td;
                    _targetDuty1 = td;
                }

                // Engage manual mode once, seeded with the CURRENT duty so nothing jumps.
                if (!_manualEngaged || _hwEcMode != 1)
                {
                    _rampDuty0 = (_cfg.RampMode == 2) ? _targetDuty0 : _fan0Duty;
                    _rampDuty1 = (_cfg.RampMode == 2) ? _targetDuty1 : _fan1Duty;
                    string cmd = string.Format(CultureInfo.InvariantCulture, "MANUAL {0} {1}", _rampDuty0, _rampDuty1);
                    if (_ec.SendCommand(cmd, out ecMode, out cTemp, out gTemp, out d0, out r0, out d1, out r1))
                    {
                        _hwEcMode = ecMode; _fan0Duty = d0; _fan0Rpm = r0; _fan1Duty = d1; _fan1Rpm = r1;
                        _lastSentDuty0 = _rampDuty0;
                        _lastSentDuty1 = _rampDuty1;
                        _manualEngaged = true;
                        _lastRampUtc = DateTime.UtcNow;
                    }
                }
            }
        }

        private void StepRampOnControlThread()
        {
            if (!_ec.Connected) return;
            if (_safetyTripped) return;
            if (_cfg.Mode == FanControlMode.EcAuto) return;
            if (!_manualEngaged) return;

            DateTime now = DateTime.UtcNow;
            double dt = (now - _lastRampUtc).TotalSeconds;
            _lastRampUtc = now;
            if (dt <= 0.0) return;
            if (dt > 1.0) dt = 1.0;

            int old0 = _rampDuty0;
            int old1 = _rampDuty1;

            if (_cfg.RampMode == 2)
            {
                _rampDuty0 = _targetDuty0;
                _rampDuty1 = _targetDuty1;
            }
            else
            {
                double upSec = (_cfg.RampMode == 0) ? 2.5 : 1.0;
                double downSec = (_cfg.RampMode == 0) ? 5.0 : 2.0;

                double maxUp = 255.0 * dt / upSec;
                double maxDown = 255.0 * dt / downSec;

                _rampDuty0 = StepToward(_rampDuty0, _targetDuty0, maxUp, maxDown);
                _rampDuty1 = StepToward(_rampDuty1, _targetDuty1, maxUp, maxDown);
            }

            if (_rampDuty0 == old0 && _rampDuty1 == old1 && _rampDuty0 == _lastSentDuty0 && _rampDuty1 == _lastSentDuty1)
                return;
            if (_rampDuty0 == _lastSentDuty0 && _rampDuty1 == _lastSentDuty1)
                return;

            int send0 = _rampDuty0;
            int send1 = _rampDuty1;
            if (_ec.SendDuty(send0, send1))
            {
                _lastSentDuty0 = send0;
                _lastSentDuty1 = send1;
                _fan0Duty = send0;
                _fan1Duty = send1;
            }
            else
            {
                _manualEngaged = false;
            }
        }

        private static int StepToward(int current, int target, double maxUp, double maxDown)
        {
            if (current == target) return target;
            if (target > current)
            {
                int step = (int)Math.Floor(maxUp);
                if (step < 1) step = 1;
                return Math.Min(target, current + step);
            }
            else
            {
                int step = (int)Math.Floor(maxDown);
                if (step < 1) step = 1;
                return Math.Max(target, current - step);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            ThemePalette t = ThemePalette.Get(_cfg.LightMode);
            int pad = 16;
            int w = ClientSize.Width - pad * 2;

            // 1. Header Row (Title + Status Pill + Light/Dark Toggle + Tray Button)
            using (Font fTitle = new Font("Segoe UI", 12.5f, FontStyle.Bold))
            using (SolidBrush bPri = new SolidBrush(t.TextPrimary))
            {
                g.DrawString("Fans & Thermal", fTitle, bPri, pad, 14);
            }

            string stateText;
            Color stateColor;
            if (!_ec.Connected) { stateText = "Reconnecting"; stateColor = t.AccentAmber; }
            else if (_safetyTripped) { stateText = "Safety Override"; stateColor = t.AccentAmber; }
            else if (_cfg.Mode == FanControlMode.EcAuto) { stateText = "BIOS Auto"; stateColor = t.AccentGreen; }
            else if (_cfg.Mode == FanControlMode.FansOff) { stateText = "0 RPM Off"; stateColor = t.AccentRed; }
            else if (_cfg.Mode == FanControlMode.Manual) { stateText = "Manual PWM"; stateColor = t.AccentBlue; }
            else { stateText = "Custom Curve"; stateColor = t.AccentBlue; }

            Rectangle statusPill = new Rectangle(pad + 142, 16, 102, 22);
            _statusPillRect = statusPill;
            FillRoundedRect(g, statusPill, 6, Color.FromArgb(_cfg.LightMode ? 28 : 38, stateColor));
            using (Font fSt = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (SolidBrush bSt = new SolidBrush(stateColor))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(stateText, fSt, bSt, statusPill, sf);
            }

            // Light / Dark Mode button & Tray button
            _themeToggleRect = new Rectangle(ClientSize.Width - pad - 142, 13, 74, 28);
            _trayBtnRect = new Rectangle(ClientSize.Width - pad - 62, 13, 62, 28);

            DrawSmallHeaderButton(g, _themeToggleRect, _cfg.LightMode ? "Dark" : "Light", _cfg.LightMode, true, t);
            DrawSmallHeaderButton(g, _trayBtnRect, "Tray", false, false, t);

            // 2. Telemetry Strip (2 compact G-Helper cards side by side)
            int teleY = 50;
            int teleW = (w - 12) / 2;
            int teleH = 62;
            DrawCompactSensorCard(g, new Rectangle(pad, teleY, teleW, teleH), "CPU Fan", _cpuTemp, _fan0Rpm, _fan0Duty, t);
            DrawCompactSensorCard(g, new Rectangle(pad + teleW + 12, teleY, teleW, teleH), "GPU Fan", _gpuTemp, _fan1Rpm, _fan1Duty, t);

            // 3. G-Helper 4-Tile Mode Selector
            int modeY = teleY + teleH + 12;
            int modeH = 64;
            Rectangle modeCard = new Rectangle(pad, modeY, w, modeH);
            FillRoundedRect(g, modeCard, 10, t.BgCard);
            DrawRoundedRect(g, modeCard, 10, t.BorderCard, 1f);

            string[] modeNames = new string[] { "Auto", "0 RPM Off", "Manual", "Curve" };
            Color[] modeAccents = new Color[] { t.AccentGreen, t.AccentRed, t.AccentBlue, t.AccentBlue };
            int tileW = (w - 16 - 12) / 4;
            for (int i = 0; i < 4; i++)
            {
                Rectangle tr = new Rectangle(pad + 8 + i * (tileW + 4), modeY + 6, tileW, modeH - 12);
                _modeRects[i] = tr;
                bool active = ((int)_cfg.Mode == i);
                bool hover = tr.Contains(_mousePos);
                Color ac = modeAccents[i];

                if (active)
                {
                    FillRoundedRect(g, tr, 8, Color.FromArgb(_cfg.LightMode ? 26 : 42, ac));
                    DrawRoundedRect(g, tr, 8, ac, 1.5f);
                }
                else if (hover)
                {
                    FillRoundedRect(g, tr, 8, t.BgHover);
                }

                Color fg = active ? ac : t.TextSecondary;
                DrawModeTileIcon(g, i, new Point(tr.X + tr.Width / 2, tr.Y + 16), fg);

                using (Font fm = new Font("Segoe UI", 8.8f, active ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush bm = new SolidBrush(active ? (_cfg.LightMode ? t.TextPrimary : Color.White) : t.TextSecondary))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    Rectangle lblR = new Rectangle(tr.X, tr.Y + 28, tr.Width, 20);
                    g.DrawString(modeNames[i], fm, bm, lblR, sf);
                }
            }

            // 4. Main Control Card (Cleanly switches between Curve View and Manual Sliders View)
            int mainY = modeY + modeH + 12;
            int mainH = 224;
            Rectangle mainCard = new Rectangle(pad, mainY, w, mainH);
            FillRoundedRect(g, mainCard, 10, t.BgCard);
            DrawRoundedRect(g, mainCard, 10, t.BorderCard, 1f);

            // Subtle view switcher in top-left of card
            _tabCurveRect = new Rectangle(pad + 12, mainY + 10, 78, 25);
            _tabManualRect = new Rectangle(pad + 94, mainY + 10, 78, 25);
            DrawTabPill(g, _tabCurveRect, "Fan Curve", !_showManualTab, t);
            DrawTabPill(g, _tabManualRect, "Sliders", _showManualTab, t);

            if (_showManualTab)
            {
                DrawManualSection(g, mainCard, t);
            }
            else
            {
                DrawCurveSection(g, mainCard, t);
            }

            // 5. Compact Safety Override Footer Card
            int footY = mainY + mainH + 12;
            int footH = 48;
            Rectangle footCard = new Rectangle(pad, footY, w, footH);
            FillRoundedRect(g, footCard, 10, t.BgCard);
            DrawRoundedRect(g, footCard, 10, _safetyTripped ? t.AccentAmber : t.BorderCard, _safetyTripped ? 1.5f : 1f);

            _safetyToggleRect = new Rectangle(pad + 14, footY + 13, 140, 22);
            DrawToggleSwitch(g, _safetyToggleRect, _cfg.SafetyOverrideEnabled, "Safety Cut-In", t.AccentAmber, t);

            _safetySliderRect = new Rectangle(pad + 160, footY + 13, 122, 22);
            DrawCompactSlider(g, _safetySliderRect, _cfg.SafetyTempLimit, 70, 90, "°C", t.AccentAmber, _cfg.SafetyOverrideEnabled, t);

            _rampPillRect = new Rectangle(pad + 290, footY + 11, 88, 26);
            bool rampHover = _rampPillRect.Contains(_mousePos);
            string rampText = (_cfg.RampMode == 0) ? "Ramp: Smooth" : (_cfg.RampMode == 1 ? "Ramp: Fast" : "Ramp: Instant");
            FillRoundedRect(g, _rampPillRect, 6, rampHover ? t.BgHover : t.BgSubtle);
            DrawRoundedRect(g, _rampPillRect, 6, rampHover ? t.TextMuted : t.BorderCard, 1f);
            using (Font fr = new Font("Segoe UI", 7.8f, FontStyle.Regular))
            using (SolidBrush br = new SolidBrush(t.TextSecondary))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(rampText, fr, br, _rampPillRect, sf);
            }

            _restoreOnExitRect = new Rectangle(pad + 384, footY + 14, 84, 22);
            DrawCheckBoxCompact(g, _restoreOnExitRect, _cfg.RestoreAutoOnExit, "Auto-Exit", t.AccentGreen, t);
        }

        private void DrawSmallHeaderButton(Graphics g, Rectangle r, string text, bool isLightIcon, bool showThemeDot, ThemePalette t)
        {
            bool hover = r.Contains(_mousePos);
            FillRoundedRect(g, r, 7, hover ? t.BgHover : t.BgCard);
            DrawRoundedRect(g, r, 7, t.BorderCard, 1f);

            using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (SolidBrush b = new SolidBrush(t.TextPrimary))
            {
                if (showThemeDot)
                {
                    Color dotC = _cfg.LightMode ? Color.FromArgb(99, 102, 241) : Color.FromArgb(250, 204, 21);
                    using (SolidBrush bd = new SolidBrush(dotC))
                    {
                        g.FillEllipse(bd, r.X + 11, r.Y + 9, 10, 10);
                    }
                    g.DrawString(text, f, b, r.X + 26, r.Y + 6);
                }
                else
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(text, f, b, r, sf);
                }
            }
        }

        private void DrawCompactSensorCard(Graphics g, Rectangle r, string label, int temp, int rpm, int duty, ThemePalette t)
        {
            FillRoundedRect(g, r, 10, t.BgCard);
            DrawRoundedRect(g, r, 10, t.BorderCard, 1f);

            Color tempDot = temp >= 85 ? t.AccentRed : (temp >= 72 ? t.AccentAmber : t.AccentGreen);
            int pct = (int)Math.Round(duty * 100.0 / 255.0);

            using (SolidBrush bDot = new SolidBrush(tempDot))
                g.FillEllipse(bDot, r.X + 14, r.Y + 14, 8, 8);

            using (Font fLbl = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Font fTemp = new Font("Segoe UI", 13.5f, FontStyle.Bold))
            using (Font fRpm = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (Font fPct = new Font("Segoe UI", 8.2f, FontStyle.Regular))
            using (SolidBrush bPri = new SolidBrush(t.TextPrimary))
            using (SolidBrush bSec = new SolidBrush(t.TextSecondary))
            using (SolidBrush bMut = new SolidBrush(t.TextMuted))
            {
                g.DrawString(label, fLbl, bSec, r.X + 26, r.Y + 10);
                g.DrawString(temp + "°C", fTemp, bPri, r.X + 13, r.Y + 26);

                StringFormat sfRight = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString(rpm + " RPM", fRpm, bPri, r.Right - 12, r.Y + 12, sfRight);
                g.DrawString(pct + "% duty", fPct, bMut, r.Right - 12, r.Y + 32, sfRight);
            }

            // Slim bottom progress bar
            Rectangle barBg = new Rectangle(r.X + 12, r.Bottom - 8, r.Width - 24, 4);
            FillRoundedRect(g, barBg, 2, t.BgSubtle);
            int fillW = (int)Math.Round(barBg.Width * Math.Max(0.0, Math.Min(1.0, duty / 255.0)));
            if (fillW > 2)
            {
                FillRoundedRect(g, new Rectangle(barBg.X, barBg.Y, fillW, barBg.Height), 2, t.AccentBlue);
            }
        }

        private static void DrawModeTileIcon(Graphics g, int idx, Point center, Color c)
        {
            using (Pen p = new Pen(c, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (idx == 0) // Auto (A inside circle)
                {
                    g.DrawEllipse(p, center.X - 8, center.Y - 8, 16, 16);
                    using (Font fa = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (SolidBrush ba = new SolidBrush(c))
                    {
                        StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("A", fa, ba, new RectangleF(center.X - 8, center.Y - 8, 16, 16), sf);
                    }
                }
                else if (idx == 1) // 0 RPM Off (Power/Off symbol)
                {
                    g.DrawArc(p, center.X - 7, center.Y - 7, 14, 14, -60, 300);
                    g.DrawLine(p, center.X, center.Y - 9, center.X, center.Y - 1);
                }
                else if (idx == 2) // Manual (2 horizontal sliders)
                {
                    g.DrawLine(p, center.X - 8, center.Y - 4, center.X + 8, center.Y - 4);
                    g.DrawLine(p, center.X - 8, center.Y + 4, center.X + 8, center.Y + 4);
                    using (SolidBrush b = new SolidBrush(c))
                    {
                        g.FillEllipse(b, center.X - 3, center.Y - 7, 6, 6);
                        g.FillEllipse(b, center.X + 1, center.Y + 1, 6, 6);
                    }
                }
                else // Curve (Rising polyline with dots)
                {
                    Point[] pts = new Point[]
                    {
                        new Point(center.X - 8, center.Y + 5),
                        new Point(center.X - 2, center.Y + 3),
                        new Point(center.X + 3, center.Y - 2),
                        new Point(center.X + 8, center.Y - 6)
                    };
                    g.DrawLines(p, pts);
                }
            }
        }

        private void DrawTabPill(Graphics g, Rectangle r, string text, bool active, ThemePalette t)
        {
            bool hover = r.Contains(_mousePos);
            FillRoundedRect(g, r, 6, active ? Color.FromArgb(_cfg.LightMode ? 24 : 38, t.AccentBlue) : (hover ? t.BgHover : t.BgSubtle));
            if (active) DrawRoundedRect(g, r, 6, t.AccentBlue, 1f);
            using (Font f = new Font("Segoe UI", 8.2f, active ? FontStyle.Bold : FontStyle.Regular))
            using (SolidBrush b = new SolidBrush(active ? t.AccentBlue : t.TextSecondary))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, f, b, r, sf);
            }
        }

        private void DrawManualSection(Graphics g, Rectangle card, ThemePalette t)
        {
            _linkFansRect = new Rectangle(card.Right - 138, card.Y + 12, 126, 22);
            DrawCheckBox(g, _linkFansRect, _cfg.LinkFans, "Link CPU & GPU", t.AccentBlue, t);

            int slX = card.X + 90;
            int slW = card.Width - 150;
            _cpuSliderRect = new Rectangle(slX, card.Y + 62, slW, 24);
            _gpuSliderRect = new Rectangle(slX, card.Y + 114, slW, 24);

            DrawFullSliderRow(g, card.X + 16, card.Y + 62, "CPU Fan", _cpuSliderRect, _cfg.ManualCpuPercent, t.AccentBlue, true, t);
            int gpuPct = _cfg.LinkFans ? _cfg.ManualCpuPercent : _cfg.ManualGpuPercent;
            DrawFullSliderRow(g, card.X + 16, card.Y + 114, "GPU Fan", _gpuSliderRect, gpuPct, t.AccentBlue, !_cfg.LinkFans, t);

            // 5 clean quick buttons at bottom
            int qY = card.Bottom - 44;
            int qW = (card.Width - 32 - 4 * 8) / 5;
            for (int i = 0; i < 5; i++)
            {
                Rectangle qr = new Rectangle(card.X + 16 + i * (qW + 8), qY, qW, 28);
                _quickSpeedRects[i] = qr;
                int val = _quickSpeedValues[i];
                bool selected = (_cfg.Mode == FanControlMode.FansOff && val == 0) ||
                                (_cfg.Mode == FanControlMode.Manual && _cfg.ManualCpuPercent == val && (_cfg.LinkFans || _cfg.ManualGpuPercent == val));
                bool hover = qr.Contains(_mousePos);
                Color ac = (val == 0) ? t.AccentRed : t.AccentBlue;
                FillRoundedRect(g, qr, 6, selected ? Color.FromArgb(_cfg.LightMode ? 25 : 42, ac) : (hover ? t.BgHover : t.BgSubtle));
                DrawRoundedRect(g, qr, 6, selected ? ac : t.BorderCard, 1f);
                string lbl = (val == 0) ? "0% Off" : val + "%";
                using (Font fq = new Font("Segoe UI", 8.5f, selected ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush bq = new SolidBrush(selected ? ac : t.TextSecondary))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(lbl, fq, bq, qr, sf);
                }
            }
        }

        private void DrawCurveSection(Graphics g, Rectangle card, ThemePalette t)
        {
            // 4 compact preset pills in top-right of Curve card
            int pw = 62;
            int startX = card.Right - 12 - (pw * 4 + 3 * 5);
            for (int i = 0; i < 4; i++)
            {
                Rectangle pr = new Rectangle(startX + i * (pw + 5), card.Y + 10, pw, 25);
                _presetRects[i] = pr;
                bool active = (_cfg.ActivePreset == i);
                bool hover = pr.Contains(_mousePos);
                FillRoundedRect(g, pr, 6, active ? Color.FromArgb(_cfg.LightMode ? 25 : 40, t.AccentBlue) : (hover ? t.BgHover : t.BgSubtle));
                if (active) DrawRoundedRect(g, pr, 6, t.AccentBlue, 1f);
                using (Font fp = new Font("Segoe UI", 8f, active ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush bp = new SolidBrush(active ? t.AccentBlue : t.TextSecondary))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(_presetNames[i], fp, bp, pr, sf);
                }
            }

            _curvePlotRect = new Rectangle(card.X + 42, card.Y + 46, card.Width - 58, 150);
            int maxTemp = Math.Max(_cpuTemp, _gpuTemp);
            int targetPct = EvaluateCurvePercent(maxTemp);
            DrawCurveGraph(g, _curvePlotRect, maxTemp, targetPct, t);
        }

        private void DrawCurveGraph(Graphics g, Rectangle plot, int currentTemp, int currentTargetPct, ThemePalette t)
        {
            FillRoundedRect(g, plot, 6, t.BgSubtle);
            DrawRoundedRect(g, plot, 6, t.BorderCard, 1f);

            using (Font fAxis = new Font("Segoe UI", 7.5f, FontStyle.Regular))
            using (SolidBrush bAxis = new SolidBrush(t.TextMuted))
            using (Pen pGrid = new Pen(t.GridLine, 1f))
            {
                for (int pct = 0; pct <= 100; pct += 25)
                {
                    int py = PercentToY(plot, pct);
                    g.DrawLine(pGrid, plot.Left, py, plot.Right, py);
                    g.DrawString(pct + "%", fAxis, bAxis, plot.Left - 32, py - 6);
                }
                for (int temp = 30; temp <= 100; temp += 10)
                {
                    int px = TempToX(plot, temp);
                    g.DrawLine(pGrid, px, plot.Top, px, plot.Bottom);
                    g.DrawString(temp + "°", fAxis, bAxis, px - 9, plot.Bottom + 3);
                }
            }

            List<CurvePoint> pts = _cfg.Curve;
            if (pts == null || pts.Count < 2) return;

            PointF[] screenPts = new PointF[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                screenPts[i] = new PointF(TempToX(plot, pts[i].Temp), PercentToY(plot, pts[i].Percent));
            }

            using (GraphicsPath fillPath = new GraphicsPath())
            {
                fillPath.AddLine(plot.Left, plot.Bottom, screenPts[0].X, screenPts[0].Y);
                fillPath.AddLines(screenPts);
                fillPath.AddLine(screenPts[screenPts.Length - 1].X, screenPts[screenPts.Length - 1].Y, plot.Right, screenPts[screenPts.Length - 1].Y);
                fillPath.AddLine(plot.Right, screenPts[screenPts.Length - 1].Y, plot.Right, plot.Bottom);
                fillPath.CloseFigure();

                using (LinearGradientBrush grad = new LinearGradientBrush(
                    plot, Color.FromArgb(_cfg.LightMode ? 45 : 60, t.AccentBlue), Color.FromArgb(4, t.AccentBlue), LinearGradientMode.Vertical))
                {
                    g.FillPath(grad, fillPath);
                }
            }

            using (Pen pCurve = new Pen(t.AccentBlue, 2.2f))
            {
                g.DrawLine(pCurve, plot.Left, screenPts[0].Y, screenPts[0].X, screenPts[0].Y);
                g.DrawLines(pCurve, screenPts);
                g.DrawLine(pCurve, screenPts[screenPts.Length - 1].X, screenPts[screenPts.Length - 1].Y, plot.Right, screenPts[screenPts.Length - 1].Y);
            }

            // Live operating point
            int liveX = TempToX(plot, Math.Max(30, Math.Min(100, currentTemp)));
            int liveY = PercentToY(plot, currentTargetPct);
            using (Pen pLive = new Pen(Color.FromArgb(120, t.AccentGreen), 1.2f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pLive, liveX, plot.Top, liveX, plot.Bottom);
            }
            using (SolidBrush bCore = new SolidBrush(t.AccentGreen))
            {
                g.FillEllipse(bCore, liveX - 4, liveY - 4, 8, 8);
            }

            // Nodes
            for (int i = 0; i < screenPts.Length; i++)
            {
                PointF pt = screenPts[i];
                bool activeNode = (_draggingCurveIdx == i) ||
                    (Math.Abs(_mousePos.X - pt.X) <= 9 && Math.Abs(_mousePos.Y - pt.Y) <= 9);
                int rad = activeNode ? 6 : 4;
                using (SolidBrush bNodeBg = new SolidBrush(t.BgCard))
                using (Pen pNode = new Pen(t.AccentBlue, 2f))
                {
                    g.FillEllipse(bNodeBg, pt.X - rad, pt.Y - rad, rad * 2, rad * 2);
                    g.DrawEllipse(pNode, pt.X - rad, pt.Y - rad, rad * 2, rad * 2);
                }
                if (activeNode)
                {
                    string tip = pts[i].Temp + "°C -> " + pts[i].Percent + "%";
                    Rectangle tipR = new Rectangle((int)pt.X - 36, (int)pt.Y - 26, 72, 19);
                    if (tipR.Top < plot.Top + 2) tipR.Y = (int)pt.Y + 8;
                    FillRoundedRect(g, tipR, 4, t.BgCard);
                    DrawRoundedRect(g, tipR, 4, t.AccentBlue, 1f);
                    using (Font fTip = new Font("Segoe UI", 7.8f, FontStyle.Bold))
                    using (SolidBrush bTip = new SolidBrush(t.TextPrimary))
                    {
                        StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(tip, fTip, bTip, tipR, sf);
                    }
                }
            }
        }

        private void DrawFullSliderRow(Graphics g, int labelX, int y, string label, Rectangle trackRect, int val, Color accent, bool enabled, ThemePalette t)
        {
            using (Font f = new Font("Segoe UI", 9.2f, FontStyle.Regular))
            using (Font fVal = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush bText = new SolidBrush(enabled ? t.TextPrimary : t.TextMuted))
            using (SolidBrush bVal = new SolidBrush(enabled ? accent : t.TextMuted))
            {
                g.DrawString(label, f, bText, labelX, y + 3);
                Rectangle bar = new Rectangle(trackRect.X, trackRect.Y + 9, trackRect.Width, 6);
                FillRoundedRect(g, bar, 3, t.BgSubtle);
                double ratio = Math.Max(0.0, Math.Min(1.0, val / 100.0));
                int fillW = (int)Math.Round(bar.Width * ratio);
                if (fillW > 3)
                {
                    FillRoundedRect(g, new Rectangle(bar.X, bar.Y, fillW, bar.Height), 3, enabled ? accent : t.TextMuted);
                }
                int knobX = bar.X + fillW;
                Rectangle knob = new Rectangle(knobX - 7, trackRect.Y + 5, 14, 14);
                using (SolidBrush kb = new SolidBrush(t.BgCard))
                using (Pen kp = new Pen(enabled ? accent : t.TextMuted, 2f))
                {
                    g.FillEllipse(kb, knob);
                    g.DrawEllipse(kp, knob);
                }
                g.DrawString(val + "%", fVal, bVal, trackRect.Right + 10, y + 2);
            }
        }

        private void DrawCompactSlider(Graphics g, Rectangle trackRect, int val, int minVal, int maxVal, string unit, Color accent, bool enabled, ThemePalette t)
        {
            Rectangle bar = new Rectangle(trackRect.X, trackRect.Y + 8, trackRect.Width - 42, 6);
            FillRoundedRect(g, bar, 3, t.BgSubtle);
            double ratio = (double)(val - minVal) / (double)Math.Max(1, maxVal - minVal);
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            int fillW = (int)Math.Round(bar.Width * ratio);
            if (fillW > 3)
            {
                FillRoundedRect(g, new Rectangle(bar.X, bar.Y, fillW, bar.Height), 3, enabled ? accent : t.TextMuted);
            }
            int knobX = bar.X + fillW;
            Rectangle knob = new Rectangle(knobX - 6, trackRect.Y + 5, 12, 12);
            using (SolidBrush kb = new SolidBrush(t.BgCard))
            using (Pen kp = new Pen(enabled ? accent : t.TextMuted, 1.8f))
            {
                g.FillEllipse(kb, knob);
                g.DrawEllipse(kp, knob);
            }
            using (Font fVal = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (SolidBrush bVal = new SolidBrush(enabled ? accent : t.TextMuted))
            {
                g.DrawString(val + unit, fVal, bVal, bar.Right + 8, trackRect.Y + 2);
            }
        }

        private void DrawCheckBox(Graphics g, Rectangle r, bool checkedState, string text, Color accent, ThemePalette t)
        {
            Rectangle box = new Rectangle(r.X, r.Y + 3, 15, 15);
            FillRoundedRect(g, box, 4, checkedState ? accent : t.BgSubtle);
            DrawRoundedRect(g, box, 4, checkedState ? accent : t.BorderCard, 1.2f);
            if (checkedState)
            {
                using (Pen p = new Pen(Color.White, 1.8f))
                {
                    g.DrawLines(p, new Point[]
                    {
                        new Point(box.X + 3, box.Y + 7),
                        new Point(box.X + 6, box.Y + 11),
                        new Point(box.X + 12, box.Y + 4)
                    });
                }
            }
            using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (SolidBrush b = new SolidBrush(t.TextSecondary))
            {
                g.DrawString(text, f, b, box.Right + 7, r.Y + 2);
            }
        }

        private void DrawCheckBoxCompact(Graphics g, Rectangle r, bool checkedState, string text, Color accent, ThemePalette t)
        {
            Rectangle box = new Rectangle(r.X, r.Y + 3, 15, 15);
            FillRoundedRect(g, box, 4, checkedState ? accent : t.BgSubtle);
            DrawRoundedRect(g, box, 4, checkedState ? accent : t.BorderCard, 1.2f);
            if (checkedState)
            {
                using (Pen p = new Pen(Color.White, 1.8f))
                {
                    g.DrawLines(p, new Point[]
                    {
                        new Point(box.X + 3, box.Y + 7),
                        new Point(box.X + 6, box.Y + 11),
                        new Point(box.X + 12, box.Y + 4)
                    });
                }
            }
            using (Font f = new Font("Segoe UI", 7.8f, FontStyle.Regular))
            using (SolidBrush b = new SolidBrush(t.TextSecondary))
            {
                g.DrawString(text, f, b, box.Right + 5, r.Y + 3);
            }
        }

        private void DrawToggleSwitch(Graphics g, Rectangle r, bool on, string text, Color accent, ThemePalette t)
        {
            Rectangle sw = new Rectangle(r.X, r.Y + 2, 34, 18);
            FillRoundedRect(g, sw, 9, on ? Color.FromArgb(_cfg.LightMode ? 45 : 65, accent) : t.BgSubtle);
            DrawRoundedRect(g, sw, 9, on ? accent : t.BorderCard, 1.2f);
            int kx = on ? sw.Right - 15 : sw.X + 3;
            using (SolidBrush kb = new SolidBrush(on ? accent : t.TextMuted))
            {
                g.FillEllipse(kb, kx, sw.Y + 3, 12, 12);
            }
            using (Font f = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(t.TextPrimary))
            {
                g.DrawString(text, f, b, sw.Right + 8, r.Y + 2);
            }
        }

        private static int TempToX(Rectangle plot, int temp)
        {
            double ratio = (temp - 30.0) / 70.0;
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            return plot.Left + (int)Math.Round(plot.Width * ratio);
        }

        private static int XToTemp(Rectangle plot, int x)
        {
            double ratio = (double)(x - plot.Left) / (double)Math.Max(1, plot.Width);
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            return 30 + (int)Math.Round(ratio * 70.0);
        }

        private static int PercentToY(Rectangle plot, int pct)
        {
            double ratio = pct / 100.0;
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            return plot.Bottom - (int)Math.Round(plot.Height * ratio);
        }

        private static int YToPercent(Rectangle plot, int y)
        {
            double ratio = (double)(plot.Bottom - y) / (double)Math.Max(1, plot.Height);
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            return (int)Math.Round(ratio * 100.0);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (_statusPillRect.Contains(e.Location) && !_ec.Connected)
            {
                PollAndApplyAsync(true);
                return;
            }

            if (_themeToggleRect.Contains(e.Location))
            {
                _cfg.LightMode = !_cfg.LightMode;
                _cfg.Save(_cfgPath);
                ApplyWindowTheme();
                Invalidate();
                return;
            }

            if (_trayBtnRect.Contains(e.Location))
            {
                Hide();
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                if (_modeRects[i].Contains(e.Location))
                {
                    SetMode((FanControlMode)i);
                    return;
                }
            }

            if (_tabCurveRect.Contains(e.Location))
            {
                _showManualTab = false;
                Invalidate();
                return;
            }
            if (_tabManualRect.Contains(e.Location))
            {
                _showManualTab = true;
                Invalidate();
                return;
            }

            if (_showManualTab)
            {
                if (_linkFansRect.Contains(e.Location))
                {
                    _cfg.LinkFans = !_cfg.LinkFans;
                    if (_cfg.LinkFans) _cfg.ManualGpuPercent = _cfg.ManualCpuPercent;
                    _cfg.Save(_cfgPath);
                    Invalidate();
                    if (_cfg.Mode == FanControlMode.Manual) PollAndApplyAsync(true);
                    return;
                }
                if (_cpuSliderRect.Contains(e.Location))
                {
                    _draggingCpuSlider = true;
                    UpdateSliderFromMouse(e.X);
                    return;
                }
                if (!_cfg.LinkFans && _gpuSliderRect.Contains(e.Location))
                {
                    _draggingGpuSlider = true;
                    UpdateSliderFromMouse(e.X);
                    return;
                }
                for (int i = 0; i < _quickSpeedRects.Length; i++)
                {
                    if (_quickSpeedRects[i].Contains(e.Location))
                    {
                        int val = _quickSpeedValues[i];
                        _cfg.ManualCpuPercent = val;
                        _cfg.ManualGpuPercent = val;
                        SetMode(val == 0 ? FanControlMode.FansOff : FanControlMode.Manual);
                        _showManualTab = true;
                        Invalidate();
                        return;
                    }
                }
            }
            else
            {
                for (int i = 0; i < _cfg.Curve.Count; i++)
                {
                    int nx = TempToX(_curvePlotRect, _cfg.Curve[i].Temp);
                    int ny = PercentToY(_curvePlotRect, _cfg.Curve[i].Percent);
                    if (Math.Abs(e.X - nx) <= 10 && Math.Abs(e.Y - ny) <= 10)
                    {
                        _draggingCurveIdx = i;
                        _cfg.ActivePreset = -1;
                        if (_cfg.Mode != FanControlMode.CustomCurve)
                            _cfg.Mode = FanControlMode.CustomCurve;
                        Invalidate();
                        return;
                    }
                }
                for (int i = 0; i < _presetRects.Length; i++)
                {
                    if (_presetRects[i].Contains(e.Location))
                    {
                        if (i == 0) _cfg.SetPresetZeroRpmIdle();
                        else if (i == 1) _cfg.SetPresetSilent();
                        else if (i == 2) _cfg.SetPresetBalanced();
                        else if (i == 3) _cfg.SetPresetTurbo();
                        SetMode(FanControlMode.CustomCurve);
                        return;
                    }
                }
            }

            if (_safetyToggleRect.Contains(e.Location))
            {
                _cfg.SafetyOverrideEnabled = !_cfg.SafetyOverrideEnabled;
                _cfg.Save(_cfgPath);
                Invalidate();
                PollAndApplyAsync(true);
                return;
            }

            if (_cfg.SafetyOverrideEnabled && _safetySliderRect.Contains(e.Location))
            {
                _draggingSafetySlider = true;
                UpdateSliderFromMouse(e.X);
                return;
            }

            if (_rampPillRect.Contains(e.Location))
            {
                _cfg.RampMode = (_cfg.RampMode + 1) % 3;
                if (_cfg.RampMode == 2)
                {
                    _rampDuty0 = _targetDuty0;
                    _rampDuty1 = _targetDuty1;
                }
                _cfg.Save(_cfgPath);
                Invalidate();
                return;
            }

            if (_restoreOnExitRect.Contains(e.Location))
            {
                _cfg.RestoreAutoOnExit = !_cfg.RestoreAutoOnExit;
                _ec.SetKeepOnExit(!_cfg.RestoreAutoOnExit);
                _cfg.Save(_cfgPath);
                Invalidate();
                return;
            }
        }

        private void UpdateSliderFromMouse(int mouseX)
        {
            if (_draggingCpuSlider)
            {
                double ratio = (double)(mouseX - _cpuSliderRect.Left) / (double)Math.Max(1, _cpuSliderRect.Width);
                int pct = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, ratio)) * 100.0);
                _cfg.ManualCpuPercent = pct;
                if (_cfg.LinkFans) _cfg.ManualGpuPercent = pct;
                if (_cfg.Mode != FanControlMode.Manual) _cfg.Mode = FanControlMode.Manual;
                Invalidate();
            }
            else if (_draggingGpuSlider)
            {
                double ratio = (double)(mouseX - _gpuSliderRect.Left) / (double)Math.Max(1, _gpuSliderRect.Width);
                int pct = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, ratio)) * 100.0);
                _cfg.ManualGpuPercent = pct;
                if (_cfg.Mode != FanControlMode.Manual) _cfg.Mode = FanControlMode.Manual;
                Invalidate();
            }
            else if (_draggingSafetySlider)
            {
                int trackW = Math.Max(1, _safetySliderRect.Width - 42);
                double ratio = (double)(mouseX - _safetySliderRect.Left) / (double)trackW;
                int limit = 70 + (int)Math.Round(Math.Max(0.0, Math.Min(1.0, ratio)) * 20.0);
                _cfg.SafetyTempLimit = limit;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.Location;

            if (_draggingCpuSlider || _draggingGpuSlider || _draggingSafetySlider)
            {
                UpdateSliderFromMouse(e.X);
                return;
            }

            if (_draggingCurveIdx >= 0 && _draggingCurveIdx < _cfg.Curve.Count)
            {
                int newT = XToTemp(_curvePlotRect, e.X);
                int newP = YToPercent(_curvePlotRect, e.Y);

                int minT = (_draggingCurveIdx == 0) ? 30 : _cfg.Curve[_draggingCurveIdx - 1].Temp + 2;
                int maxT = (_draggingCurveIdx == _cfg.Curve.Count - 1) ? 100 : _cfg.Curve[_draggingCurveIdx + 1].Temp - 2;
                newT = Math.Max(minT, Math.Min(maxT, newT));
                newP = Math.Max(0, Math.Min(100, newP));

                _cfg.Curve[_draggingCurveIdx].Temp = newT;
                _cfg.Curve[_draggingCurveIdx].Percent = newP;

                for (int i = _draggingCurveIdx + 1; i < _cfg.Curve.Count; i++)
                {
                    if (_cfg.Curve[i].Percent < newP) _cfg.Curve[i].Percent = newP;
                }
                for (int i = 0; i < _draggingCurveIdx; i++)
                {
                    if (_cfg.Curve[i].Percent > newP) _cfg.Curve[i].Percent = newP;
                }

                Invalidate();
                return;
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_draggingCpuSlider || _draggingGpuSlider || _draggingSafetySlider || _draggingCurveIdx >= 0)
            {
                _draggingCpuSlider = false;
                _draggingGpuSlider = false;
                _draggingSafetySlider = false;
                _draggingCurveIdx = -1;
                _lastCurvePercent = -1;
                _cfg.Save(_cfgPath);
                PollAndApplyAsync(true);
                Invalidate();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _closing = true;
            try { SystemEvents.PowerModeChanged -= OnPowerModeChanged; } catch { }
            try { _wakeEvent.Set(); } catch { }
            try { if (_activateEvent != null) _activateEvent.Set(); } catch { }
            try { if (_controlThread != null && _controlThread.IsAlive) _controlThread.Join(1200); } catch { }
            _trayIcon.Visible = false;
            _cfg.Save(_cfgPath);
            _ec.Shutdown(_cfg.RestoreAutoOnExit);
            _ec.Dispose();
            base.OnFormClosing(e);
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void FillRoundedRect(Graphics g, Rectangle r, int radius, Color c)
        {
            using (GraphicsPath path = CreateRoundedRectPath(r, radius))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillPath(b, path);
            }
        }

        private static void DrawRoundedRect(Graphics g, Rectangle r, int radius, Color c, float width)
        {
            using (GraphicsPath path = CreateRoundedRectPath(r, radius))
            using (Pen p = new Pen(c, width))
            {
                g.DrawPath(p, path);
            }
        }
    }

    internal static class Program
    {
        private const string SingleInstanceMutexName = @"Global\TufFanControl_SingleInstance_Mutex";
        private const string ActivateEventName = @"Global\TufFanControl_Activate_Event";

        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length >= 4 && args[0] == "--worker")
            {
                int port, parentPid;
                if (int.TryParse(args[1], out port) && int.TryParse(args[2], out parentPid))
                {
                    SystemEcWorker.Run(port, parentPid, args[3]);
                }
                return;
            }

            if (!IsAdministrator())
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = Application.ExecutablePath;
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    Process.Start(psi);
                }
                catch { }
                return;
            }

            bool createdNew = true;
            Mutex instanceMutex = null;
            EventWaitHandle activateEvent = null;
            try
            {
                activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
                instanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
            }
            catch
            {
                createdNew = true;
            }

            if (!createdNew)
            {
                try { if (activateEvent != null) activateEvent.Set(); } catch { }
                return;
            }

            try
            {
                try { NativeMethods.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                string appDir = Path.GetDirectoryName(Application.ExecutablePath);
                string cfgPath = Path.Combine(appDir, "config.ini");
                AppConfig cfg = AppConfig.Load(cfgPath);

                EcBridge ec = new EcBridge();
                ec.Start();

                Application.Run(new MainForm(cfg, cfgPath, ec, activateEvent));
            }
            finally
            {
                try { if (instanceMutex != null) instanceMutex.ReleaseMutex(); } catch { }
                try { if (instanceMutex != null) instanceMutex.Close(); } catch { }
                try { if (activateEvent != null) activateEvent.Close(); } catch { }
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}

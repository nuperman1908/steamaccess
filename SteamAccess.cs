
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Steam Access")]
[assembly: System.Reflection.AssemblyProduct("Steam Access")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace SteamAccess
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "SteamAccess_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show(L.T("already_running"),
                        "Steam Access", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // The startup task passes --startup; a double-click on the exe does not
                bool fromStartup = Array.IndexOf(args, "--startup") >= 0;
                Application.Run(new TrayApp(!fromStartup));
            }
        }
    }

    // UI language: English / Chinese / Vietnamese. The choice is saved in the registry.
    static class L
    {
        public const int En = 0, Zh = 1, Vi = 2;
        public static readonly string[] Names = { "English", "中文", "Tiếng Việt" };
        public static int Lang;

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>();

        static void Add(string key, string en, string zh, string vi)
        {
            Table[key] = new[] { en, zh, vi };
        }

        static L()
        {
            Add("already_running",
                "Steam Access is already running (see the system tray icon).",
                "Steam Access 已在运行（请查看系统托盘图标）。",
                "Steam Access đang chạy rồi (xem biểu tượng ở khay hệ thống).");
            Add("status_wait", "Status: ...", "状态：...", "Trạng thái: ...");
            Add("status_on", "Status: ON", "状态：已开启", "Trạng thái: ĐANG BẬT");
            Add("status_off", "Status: off", "状态：已关闭", "Trạng thái: đang tắt");
            Add("tray_on", "Steam Access: on", "Steam Access：已开启", "Steam Access: đang bật");
            Add("tray_off", "Steam Access: off", "Steam Access：已关闭", "Steam Access: đang tắt");
            Add("enable", "Turn bypass on", "启用绕过封锁", "Bật vượt chặn");
            Add("disable", "Turn bypass off", "停用绕过封锁", "Tắt vượt chặn");
            Add("restart_steam", "Restart Steam", "重启 Steam", "Khởi động lại Steam");
            Add("run_startup", "Run at Windows startup", "开机自动运行", "Chạy cùng Windows");
            Add("open_log", "Open log", "打开日志", "Mở nhật ký");
            Add("language", "Language", "语言", "Ngôn ngữ");
            Add("exit", "Exit", "退出", "Thoát");
            Add("port_busy",
                "Could not turn on: port {0} is in use.\n{1}",
                "无法启用：端口 {0} 已被占用。\n{1}",
                "Không bật được: cổng {0} đang bị dùng.\n{1}");
            Add("relay_warn",
                "The store is reachable, but game downloads could not be enabled: {0}",
                "商店已可访问，但游戏下载功能未能启用：{0}",
                "Cửa hàng đã vào được, nhưng phần tải game chưa bật được: {0}");
            Add("balloon_on",
                "Bypass enabled. If Steam still can't connect, choose \"{0}\".",
                "已启用绕过封锁。如果仍无法进入 Steam，请选择“{0}”。",
                "Đã bật vượt chặn. Nếu Steam chưa vào được, chọn \"{0}\".");
            Add("launched_on",
                "Steam Access is on and running in the system tray.",
                "Steam Access 已开启，正在系统托盘中运行。",
                "Steam Access đã bật và đang chạy ở khay hệ thống.");
            Add("launched_off",
                "Steam Access is running but turned off. Click the tray icon to turn it on.",
                "Steam Access 已运行但处于关闭状态。点击托盘图标即可开启。",
                "Steam Access đang chạy nhưng đang tắt. Bấm vào biểu tượng ở khay để bật.");
            Add("balloon_off", "Bypass disabled.", "已停用绕过封锁。", "Đã tắt vượt chặn.");
            Add("startup_fail",
                "Could not change the startup setting: {0}",
                "无法更改开机自启设置：{0}",
                "Không đổi được tự chạy: {0}");
            Add("startup_on",
                "Will run automatically when Windows starts.",
                "将在 Windows 启动时自动运行。",
                "Sẽ tự chạy khi khởi động Windows.");
            Add("startup_off",
                "Run at startup turned off.",
                "已关闭开机自动运行。",
                "Đã tắt tự chạy khi khởi động.");
            Add("steam_missing",
                "Steam was not found on this PC.",
                "未在本机找到 Steam。",
                "Không tìm thấy Steam trên máy.");
            Add("steam_restarting",
                "Restarting Steam...",
                "正在重启 Steam...",
                "Đang khởi động lại Steam...");
            Add("schtasks_error", "schtasks error {0}", "schtasks 出错 {0}", "schtasks lỗi {0}");

            Lang = LoadSaved();
        }

        // Vietnamese stays the default until the user picks something else
        static int LoadSaved()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\SteamAccess"))
                {
                    var v = k == null ? null : k.GetValue("Language");
                    if (v is int && (int)v >= 0 && (int)v < Names.Length) return (int)v;
                }
            }
            catch { }
            return Vi;
        }

        public static string T(string key, params object[] args)
        {
            string[] row;
            string text = Table.TryGetValue(key, out row) ? row[Lang] : key;
            return args.Length > 0 ? string.Format(text, args) : text;
        }
    }

    static class Log
    {
        static readonly object Lock = new object();
        public static readonly string PathName = Path.Combine(
            Path.GetDirectoryName(Application.ExecutablePath), "SteamAccess.log");

        public static void Write(string msg)
        {
            lock (Lock)
            {
                try
                {
                    var fi = new FileInfo(PathName);
                    if (fi.Exists && fi.Length > 1024 * 1024) fi.Delete();
                    File.AppendAllText(PathName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n");
                }
                catch { }
            }
        }
    }

    class TrayApp : ApplicationContext
    {
        const int ProxyPort = 8881;
        const string TaskName = "SteamAccess";
        const string OldRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string InetKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        const string AppKey = @"Software\SteamAccess";
        static readonly string PacUrl = "http://127.0.0.1:" + ProxyPort + "/proxy.pac";

        readonly NotifyIcon tray;
        readonly ToolStripMenuItem statusItem, toggleItem, startupItem;
        readonly ToolStripMenuItem restartItem, logItem, langItem, signItem, exitItem;
        readonly ToolStripMenuItem[] langItems = new ToolStripMenuItem[L.Names.Length];
        readonly Icon iconOn, iconOff;
        readonly Bitmap globe = MakeGlobe();
        readonly FragProxy proxy = new FragProxy();
        readonly PassThrough tls = new PassThrough(443, false);
        readonly PassThrough http = new PassThrough(80, true);
        readonly HostsManager hosts = new HostsManager();
        bool enabled;
        readonly bool announceLaunch;

        public TrayApp(bool announceLaunch)
        {
            this.announceLaunch = announceLaunch;
            iconOn = LoadTrayIcon("app.ico", Color.FromArgb(46, 160, 67));
            iconOff = LoadTrayIcon("app-off.ico", Color.FromArgb(214, 48, 49));

            var menu = new ContextMenuStrip();
            statusItem = new ToolStripMenuItem(L.T("status_wait")) { Enabled = false };
            toggleItem = new ToolStripMenuItem(L.T("enable"), null, delegate { SetEnabled(!enabled, true); });
            restartItem = new ToolStripMenuItem(L.T("restart_steam"), null, delegate { RestartSteam(); });
            startupItem = new ToolStripMenuItem(L.T("run_startup"), null, delegate { ToggleStartup(); });
            logItem = new ToolStripMenuItem(L.T("open_log"), null, delegate
            {
                try { Process.Start("notepad.exe", "\"" + Log.PathName + "\""); } catch { }
            });
            signItem = new ToolStripMenuItem("Made by Nuperman") { Enabled = false };

            exitItem = new ToolStripMenuItem(L.T("exit"), null, delegate { ExitApp(); });

            langItem = new ToolStripMenuItem(L.T("language"), globe);
            for (int i = 0; i < langItems.Length; i++)
            {
                int idx = i; 
                langItems[i] = new ToolStripMenuItem(L.Names[i], null, delegate { SetLanguage(idx); });
                langItems[i].Checked = (i == L.Lang);
                langItem.DropDownItems.Add(langItems[i]);
            }

            menu.Items.AddRange(new ToolStripItem[] {
                statusItem, new ToolStripSeparator(), toggleItem, restartItem,
                new ToolStripSeparator(), startupItem, logItem, langItem,
                new ToolStripSeparator(), signItem, exitItem });

            tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += delegate(object s, MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) SetEnabled(!enabled, true);
            };

            MigrateOldStartup();
            UpgradeStartupTask();
            startupItem.Checked = IsStartupEnabled();
            Application.ApplicationExit += delegate { Cleanup(); };
            SystemEvents.SessionEnding += delegate { Cleanup(); };

            Log.Write("App started (v1.0)");
            // Turn on by default unless the user turned it off last time
            SetEnabled(ReadSetting("Enabled", 1) == 1, false);
        }

        // ---------- enable / disable ----------
        void SetEnabled(bool on, bool userAction)
        {
            string warn = null;
            bool errorShown = false;
            if (on)
            {
                StopOldScript();
                try { proxy.Start(ProxyPort); }
                catch (Exception ex)
                {
                    on = false;
                    errorShown = true;
                    Log.Write("Proxy start failed: " + ex.Message);
                    tray.ShowBalloonTip(5000, "Steam Access",
                        L.T("port_busy", ProxyPort, ex.Message), ToolTipIcon.Error);
                }
                if (on)
                {
                    SetPac(true);
                    try { tls.Start(); http.Start(); hosts.Start(); }
                    catch (Exception ex)
                    {
                        warn = L.T("relay_warn", ex.Message);
                        Log.Write("Download relay start failed: " + ex.Message);
                    }
                }
            }
            else
            {
                SetPac(false);
                hosts.Stop();
                tls.Stop(); http.Stop();
                proxy.Stop();
            }
            enabled = on;
            Log.Write(on ? "Enabled" : "Disabled");
            if (userAction) WriteSetting("Enabled", on ? 1 : 0);
            UpdateUi();
            if (warn != null)
                tray.ShowBalloonTip(6000, "Steam Access", warn, ToolTipIcon.Warning);
            else if (!userAction && announceLaunch && !errorShown)
                tray.ShowBalloonTip(3000, "Steam Access", L.T(on ? "launched_on" : "launched_off"), ToolTipIcon.Info);
            else if (userAction)
                tray.ShowBalloonTip(3000, "Steam Access",
                    on ? L.T("balloon_on", L.T("restart_steam")) : L.T("balloon_off"), ToolTipIcon.Info);
        }

        void UpdateUi()
        {
            tray.Icon = enabled ? iconOn : iconOff;
            tray.Text = L.T(enabled ? "tray_on" : "tray_off");
            statusItem.Text = L.T(enabled ? "status_on" : "status_off");
            toggleItem.Text = L.T(enabled ? "disable" : "enable");
        }

        // ---------- language ----------
        void SetLanguage(int lang)
        {
            L.Lang = lang;
            WriteSetting("Language", lang);
            Log.Write("Language: " + L.Names[lang]);
            restartItem.Text = L.T("restart_steam");
            startupItem.Text = L.T("run_startup");
            logItem.Text = L.T("open_log");
            langItem.Text = L.T("language");
            exitItem.Text = L.T("exit");
            for (int i = 0; i < langItems.Length; i++) langItems[i].Checked = (i == lang);
            UpdateUi();
        }

        // Point Windows at our PAC file (only Steam domains use the proxy)
        static void SetPac(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(InetKey))
            {
                if (on) k.SetValue("AutoConfigURL", PacUrl);
                else if (PacUrl.Equals(k.GetValue("AutoConfigURL") as string))
                    k.DeleteValue("AutoConfigURL", false);
            }
            InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0); // SETTINGS_CHANGED
            InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0); // REFRESH
        }

        [DllImport("wininet.dll", SetLastError = true)]
        static extern bool InternetSetOption(IntPtr h, int opt, IntPtr buf, int len);

        // The earlier PowerShell version may still be holding the port
        static void StopOldScript()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("powershell"))
                {
                    try
                    {
                        using (var s = new System.Management.ManagementObjectSearcher(
                            "SELECT CommandLine FROM Win32_Process WHERE ProcessId=" + p.Id))
                            foreach (var o in s.Get())
                            {
                                var cmd = o["CommandLine"] as string;
                                if (cmd != null && cmd.IndexOf("steam-unblock.ps1", StringComparison.OrdinalIgnoreCase) >= 0)
                                { p.Kill(); p.WaitForExit(3000); }
                            }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ---------- startup (Task Scheduler, so it runs as admin without a UAC prompt) ----------
        static int Schtasks(string args)
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi))
            {
                p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        static bool IsStartupEnabled() { return Schtasks("/query /tn " + TaskName) == 0; }

        static void CreateStartupTask()
        {
            string user = Esc(Environment.UserDomainName + "\\" + Environment.UserName);
            string xml =
"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
"<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
"  <RegistrationInfo><Description>Steam Access - vuot chan Steam</Description></RegistrationInfo>\r\n" +
"  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId></LogonTrigger></Triggers>\r\n" +
"  <Principals><Principal id=\"Author\"><UserId>" + user + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
"  <Settings>\r\n" +
"    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n" +
"    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
"    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
"    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" +
"    <Enabled>true</Enabled>\r\n" +
"  </Settings>\r\n" +
"  <Actions Context=\"Author\"><Exec><Command>\"" + Esc(Application.ExecutablePath) + "\"</Command><Arguments>--startup</Arguments></Exec></Actions>\r\n" +
"</Task>\r\n";
            string tmp = Path.Combine(Path.GetTempPath(), "SteamAccessTask.xml");
            File.WriteAllText(tmp, xml, Encoding.Unicode);
            int rc = Schtasks("/create /tn " + TaskName + " /xml \"" + tmp + "\" /f");
            try { File.Delete(tmp); } catch { }
            if (rc != 0) throw new Exception(L.T("schtasks_error", rc));
        }

        static string Esc(string s) { return System.Security.SecurityElement.Escape(s); }

        void ToggleStartup()
        {
            try
            {
                if (IsStartupEnabled()) Schtasks("/delete /tn " + TaskName + " /f");
                else CreateStartupTask();
            }
            catch (Exception ex)
            {
                tray.ShowBalloonTip(4000, "Steam Access", L.T("startup_fail", ex.Message), ToolTipIcon.Error);
            }
            startupItem.Checked = IsStartupEnabled();
            tray.ShowBalloonTip(2500, "Steam Access",
                L.T(startupItem.Checked ? "startup_on" : "startup_off"),
                ToolTipIcon.Info);
        }

        // Tasks created by older builds start the exe without --startup, which would
        // make every Windows logon look like a manual launch. Recreate them once.
        static void UpgradeStartupTask()
        {
            try
            {
                if (!IsStartupEnabled()) return;
                var psi = new ProcessStartInfo("schtasks.exe", "/query /tn " + TaskName + " /xml")
                { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                string xml;
                using (var p = Process.Start(psi))
                {
                    xml = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
                    p.WaitForExit();
                }
                if (xml.IndexOf("--startup", StringComparison.OrdinalIgnoreCase) < 0) CreateStartupTask();
            }
            catch (Exception ex) { Log.Write("Startup task upgrade failed: " + ex.Message); }
        }

        // Version 1.0 used the Run key, which can't start an admin app; move it to a task
        static void MigrateOldStartup()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(OldRunKey, true))
                {
                    if (k != null && k.GetValue(TaskName) != null)
                    {
                        k.DeleteValue(TaskName, false);
                        CreateStartupTask();
                    }
                }
            }
            catch (Exception ex) { Log.Write("Startup migrate failed: " + ex.Message); }
        }

        static int ReadSetting(string name, int def)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(AppKey))
            {
                if (k == null) return def;
                var v = k.GetValue(name);
                return v is int ? (int)v : def;
            }
        }

        static void WriteSetting(string name, int value)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(AppKey))
                k.SetValue(name, value, RegistryValueKind.DWord);
        }

        // ---------- Steam ----------
        void RestartSteam()
        {
            string exe = SteamInfo.Exe;
            if (exe == null)
            {
                tray.ShowBalloonTip(4000, "Steam Access", L.T("steam_missing"), ToolTipIcon.Warning);
                return;
            }
            tray.ShowBalloonTip(2500, "Steam Access", L.T("steam_restarting"), ToolTipIcon.Info);
            var t = new Thread(delegate()
            {
                try
                {
                    if (Process.GetProcessesByName("steam").Length > 0)
                    {
                        Process.Start(exe, "-shutdown");
                        for (int i = 0; i < 60 && Process.GetProcessesByName("steam").Length > 0; i++)
                            Thread.Sleep(500);
                        Thread.Sleep(1500);
                    }
                    Process.Start(exe);
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- exit ----------
        bool cleaned;
        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            try { SetPac(false); } catch { }
            try { hosts.Stop(); } catch { }
            try { tls.Stop(); http.Stop(); } catch { }
            try { proxy.Stop(); } catch { }
            try { tray.Visible = false; } catch { }
            Log.Write("App exited");
        }

        void ExitApp()
        {
            Cleanup();
            tray.Dispose();
            Application.Exit();
        }

        // ---------- icon ----------
        public static Icon MakeIcon(Color color)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var b = new SolidBrush(color)) g.FillEllipse(b, 1, 1, 30, 30);
                    using (var pen = new Pen(Color.White, 3f))
                    {
                        g.DrawEllipse(pen, 7, 16, 9, 9);
                        g.DrawEllipse(pen, 17, 7, 9, 9);
                        g.DrawLine(pen, 14, 18, 19, 14);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        // Loads an .ico embedded in the exe (see build.ps1) at the tray's icon size.
        // Falls back to a plain drawn icon if the resource is missing.
        static Icon LoadTrayIcon(string resourceName, Color fallback)
        {
            try
            {
                int size = SystemInformation.SmallIconSize.Width;
                using (var stream = typeof(TrayApp).Assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null) return new Icon(stream, size, size);
                }
            }
            catch { }
            return MakeIcon(fallback);
        }

        // Globe icon for the language menu item (drawn at 32px, scaled down by the menu)
        static Bitmap MakeGlobe()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var c = Color.FromArgb(52, 120, 200);
                using (var fill = new SolidBrush(Color.FromArgb(45, c))) g.FillEllipse(fill, 3, 3, 26, 26);
                using (var pen = new Pen(c, 2f))
                {
                    g.DrawEllipse(pen, 3, 3, 26, 26);   // outline
                    g.DrawEllipse(pen, 10, 3, 12, 26);  // meridians
                    g.DrawLine(pen, 16, 3, 16, 29);
                    g.DrawLine(pen, 3, 16, 29, 16);     // equator
                    g.DrawLine(pen, 5, 9, 27, 9);       // latitudes
                    g.DrawLine(pen, 5, 23, 27, 23);
                }
            }
            return bmp;
        }
    }

    static class SteamInfo
    {
        public static string Exe
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var exe = k == null ? null : k.GetValue("SteamExe") as string;
                    return !string.IsNullOrEmpty(exe) && File.Exists(exe) ? exe : null;
                }
            }
        }

        public static string ContentLog
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var p = k == null ? null : k.GetValue("SteamPath") as string;
                    return string.IsNullOrEmpty(p) ? null : Path.Combine(p.Replace('/', '\\'), @"logs\content_log.txt");
                }
            }
        }
    }

    // Keeps the hosts file pointing Steam's download servers at 127.0.0.1.
    class HostsManager
    {
        const string Tag = "# SteamAccess";
        static readonly string HostsPath = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
        static readonly Regex HostRx = new Regex(@"\b((?:[a-z0-9\-]+\.)*steamcontent\.com|steampipe\.akamaized\.net)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex PopRx = new Regex(@"^cache\d+-([a-z]+\d+)\.steamcontent\.com$", RegexOptions.IgnoreCase);

        readonly HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly object sync = new object();
        System.Threading.Timer timer;

        public void Start()
        {
            lock (sync)
            {
                names.Clear();
                // Known download servers; more are added from Steam's log
                foreach (var pop in new[] { "sgp1", "hkg1" }) AddPop(pop);
                foreach (var n in new[] { "steampipe.akamaized.net", "fastly.cdn.steampipe.steamcontent.com",
                    "google2.cdn.steampipe.steamcontent.com", "alibaba.cdn.steampipe.steamcontent.com",
                    "edgenext.cdn.steampipe.steamcontent.com", "akamai.cdn.steampipe.steamcontent.com",
                    "steampipe-kr.akamaized.net", "steampipe-partner.akamaized.net" })
                    names.Add(n);
                ScanLog(false);
                Write(true);
            }
            timer = new System.Threading.Timer(delegate { lock (sync) { if (ScanLog(true)) Write(true); } }, null, 15000, 15000);
        }

        public void Stop()
        {
            if (timer != null) { timer.Dispose(); timer = null; }
            lock (sync) { try { Write(false); } catch (Exception ex) { Log.Write("Hosts cleanup failed: " + ex.Message); } }
        }

        void AddPop(string pop)
        {
            for (int i = 1; i <= 30; i++) names.Add("cache" + i + "-" + pop + ".steamcontent.com");
        }

        // Pick up server names from Steam's content log; returns true if any were new
        bool ScanLog(bool tailOnly)
        {
            var path = SteamInfo.ContentLog;
            if (path == null || !File.Exists(path)) return false;
            int before = names.Count;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long from = tailOnly ? Math.Max(0, fs.Length - 256 * 1024) : Math.Max(0, fs.Length - 4 * 1024 * 1024);
                    fs.Seek(from, SeekOrigin.Begin);
                    var text = new StreamReader(fs).ReadToEnd();
                    foreach (Match m in HostRx.Matches(text))
                    {
                        var h = m.Groups[1].Value.ToLowerInvariant();
                        if (h == "lancache.steamcontent.com" || h == "steamcontent.com") continue;
                        names.Add(h);
                        var pm = PopRx.Match(h);
                        if (pm.Success) AddPop(pm.Groups[1].Value.ToLowerInvariant());
                    }
                }
            }
            catch { }
            if (names.Count != before) { Log.Write("Download servers: " + names.Count); return true; }
            return false;
        }

        void Write(bool on)
        {
            var lines = new List<string>();
            if (File.Exists(HostsPath))
                foreach (var l in File.ReadAllLines(HostsPath))
                    if (l.IndexOf(Tag, StringComparison.Ordinal) < 0) lines.Add(l);
            if (on)
            {
                var sorted = new List<string>(names);
                sorted.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (var n in sorted) lines.Add("127.0.0.1 " + n + " " + Tag);
            }
            for (int i = 0; ; i++)
            {
                try { File.WriteAllLines(HostsPath, lines.ToArray()); break; }
                catch (IOException) { if (i >= 5) throw; Thread.Sleep(300); }
            }
            try { DnsFlushResolverCache(); } catch { }
        }

        [DllImport("dnsapi.dll")]
        static extern bool DnsFlushResolverCache();
    }

    // Looks up real addresses directly from public DNS (the hosts file points
    // these names at us, so the normal resolver can't be used).
    static class Resolver
    {
        static readonly IPAddress[] Servers = { IPAddress.Parse("1.1.1.1"), IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.0.0.1") };
        static readonly Dictionary<string, KeyValuePair<IPAddress, DateTime>> Cache =
            new Dictionary<string, KeyValuePair<IPAddress, DateTime>>(StringComparer.OrdinalIgnoreCase);
        static readonly Random Rng = new Random();

        public static IPAddress Resolve(string host)
        {
            lock (Cache)
            {
                KeyValuePair<IPAddress, DateTime> e;
                if (Cache.TryGetValue(host, out e) && e.Value > DateTime.UtcNow) return e.Key;
            }
            Exception last = null;
            foreach (var server in Servers)
            {
                try
                {
                    var ip = Query(server, host);
                    if (ip != null)
                    {
                        lock (Cache) Cache[host] = new KeyValuePair<IPAddress, DateTime>(ip, DateTime.UtcNow.AddMinutes(5));
                        return ip;
                    }
                }
                catch (Exception ex) { last = ex; }
            }
            throw new Exception("Không tra được địa chỉ của " + host + (last != null ? ": " + last.Message : ""));
        }

        static IPAddress Query(IPAddress server, string host)
        {
            var q = new List<byte>();
            int id;
            lock (Rng) id = Rng.Next(65536);
            q.AddRange(new byte[] { (byte)(id >> 8), (byte)id, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 });
            foreach (var label in host.TrimEnd('.').Split('.'))
            {
                var b = Encoding.ASCII.GetBytes(label);
                q.Add((byte)b.Length); q.AddRange(b);
            }
            q.AddRange(new byte[] { 0, 0, 1, 0, 1 }); // end, type A, class IN

            using (var udp = new UdpClient())
            {
                udp.Client.ReceiveTimeout = 2500;
                udp.Connect(server, 53);
                udp.Send(q.ToArray(), q.Count);
                var ep = new IPEndPoint(IPAddress.Any, 0);
                var r = udp.Receive(ref ep);
                if (r.Length < 12 || r[0] != (byte)(id >> 8) || r[1] != (byte)id) return null;
                int qd = (r[4] << 8) | r[5], an = (r[6] << 8) | r[7];
                int pos = 12;
                for (int i = 0; i < qd; i++) { pos = SkipName(r, pos); pos += 4; }
                for (int i = 0; i < an && pos < r.Length; i++)
                {
                    pos = SkipName(r, pos);
                    int type = (r[pos] << 8) | r[pos + 1];
                    int len = (r[pos + 8] << 8) | r[pos + 9];
                    pos += 10;
                    if (type == 1 && len == 4) return new IPAddress(new[] { r[pos], r[pos + 1], r[pos + 2], r[pos + 3] });
                    pos += len;
                }
                return null;
            }
        }

        static int SkipName(byte[] r, int pos)
        {
            while (pos < r.Length)
            {
                int l = r[pos];
                if (l == 0) return pos + 1;
                if ((l & 0xC0) == 0xC0) return pos + 2;
                pos += l + 1;
            }
            return pos;
        }
    }

    // Shared helpers
    static class Net
    {
        // Send the first `limit` bytes in tiny pieces so the filter can't read the name
        public static void SendFragmented(Stream s, byte[] data, int offset, int len, int limit)
        {
            int pos = 0;
            while (pos < len && pos < limit)
            {
                int sz = Math.Min(3, len - pos);
                s.Write(data, offset + pos, sz);
                s.Flush();
                pos += sz;
                Thread.Sleep(1);
            }
            if (pos < len) { s.Write(data, offset + pos, len - pos); s.Flush(); }
        }

        public static void Pump(Stream from, Stream to)
        {
            var b = new byte[65536];
            try
            {
                int r;
                while ((r = from.Read(b, 0, b.Length)) > 0) to.Write(b, 0, r);
            }
            catch { }
            try { to.Close(); } catch { }
        }

        // Run both directions until either side closes
        public static void Bridge(Stream a, Stream b)
        {
            var t = new Thread(delegate() { Pump(b, a); });
            t.IsBackground = true;
            t.Start();
            Pump(a, b);
            t.Join(2000);
        }

        // Read until end of HTTP headers; returns bytes read, sets hdrEnd
        public static int ReadHeaders(Stream s, byte[] buf, out int hdrEnd)
        {
            int n = 0;
            hdrEnd = -1;
            while (true)
            {
                int r = s.Read(buf, n, buf.Length - n);
                if (r <= 0) return 0;
                n += r;
                hdrEnd = IndexOf(buf, n, "\r\n\r\n");
                if (hdrEnd >= 0) { hdrEnd += 4; return n; }
                if (n >= buf.Length) return 0;
            }
        }

        public static int IndexOf(byte[] b, int len, string s)
        {
            var p = Encoding.ASCII.GetBytes(s);
            for (int i = 0; i + p.Length <= len; i++)
            {
                int j = 0;
                while (j < p.Length && b[i + j] == p[j]) j++;
                if (j == p.Length) return i;
            }
            return -1;
        }

        public static void ReadExact(Stream s, byte[] buf, int offset, int count)
        {
            while (count > 0)
            {
                int r = s.Read(buf, offset, count);
                if (r <= 0) throw new IOException("closed");
                offset += r; count -= r;
            }
        }
    }

    // Stream wrapper that sends the first write (TLS ClientHello) in tiny pieces
    class FragmentingStream : Stream
    {
        readonly Stream inner;
        bool first = true;
        public FragmentingStream(Stream s) { inner = s; }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (first) { first = false; Net.SendFragmented(inner, buffer, offset, count, 200); }
            else inner.Write(buffer, offset, count);
        }
        public override int Read(byte[] buffer, int offset, int count) { return inner.Read(buffer, offset, count); }
        public override void Flush() { inner.Flush(); }
        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override long Seek(long o, SeekOrigin so) { throw new NotSupportedException(); }
        public override void SetLength(long v) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    // Base for a loopback TCP server
    abstract class LoopbackServer
    {
        TcpListener listener;
        volatile bool running;

        protected void StartOn(int port)
        {
            if (running) return;
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(200);
            running = true;
            var t = new Thread(delegate()
            {
                while (running)
                {
                    TcpClient c;
                    try { c = listener.AcceptTcpClient(); }
                    catch { break; }
                    ThreadPool.QueueUserWorkItem(delegate {
                        try { c.NoDelay = true; Handle(c); }
                        catch (Exception ex) { OnError(ex); }
                        finally { c.Close(); }
                    });
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        public void Stop()
        {
            running = false;
            try { if (listener != null) listener.Stop(); } catch { }
        }

        protected abstract void Handle(TcpClient client);
        protected virtual void OnError(Exception ex) { }
    }

    // Game downloads: Steam connects here (hosts file) thinking it's the real
    // server. We read the server name (TLS SNI on 443, Host header on 80),
    // connect to the real address and pass bytes through unchanged.
    class PassThrough : LoopbackServer
    {
        readonly int port;
        readonly bool isHttp;
        public PassThrough(int port, bool isHttp) { this.port = port; this.isHttp = isHttp; }
        public void Start() { StartOn(port); }

        protected override void Handle(TcpClient client)
        {
            var cs = client.GetStream();
            byte[] first; int firstLen; string host;
            if (isHttp)
            {
                first = new byte[65536];
                int hdrEnd;
                firstLen = Net.ReadHeaders(cs, first, out hdrEnd);
                if (firstLen == 0) return;
                host = null;
                foreach (var line in Encoding.ASCII.GetString(first, 0, hdrEnd).Split(new[] { "\r\n" }, StringSplitOptions.None))
                    if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) host = line.Substring(5).Trim();
                if (host != null && host.IndexOf(':') > 0) host = host.Substring(0, host.IndexOf(':'));
            }
            else
            {
                // One TLS record: 5-byte header + body
                var hdr = new byte[5];
                Net.ReadExact(cs, hdr, 0, 5);
                if (hdr[0] != 0x16) return; // not a handshake
                int len = (hdr[3] << 8) | hdr[4];
                first = new byte[5 + len];
                Buffer.BlockCopy(hdr, 0, first, 0, 5);
                Net.ReadExact(cs, first, 5, len);
                firstLen = first.Length;
                host = ParseSni(first);
            }
            if (string.IsNullOrEmpty(host) || host == "127.0.0.1" || host == "localhost") return;

            var ip = Resolver.Resolve(host);
            using (var server = new TcpClient())
            {
                server.NoDelay = true;
                if (isHttp)
                {
                    // The ISP resets plain HTTP to Steam servers even when split,
                    // so carry the same HTTP traffic over HTTPS to port 443.
                    server.Connect(ip, 443);
                    using (var ssl = new SslStream(new FragmentingStream(server.GetStream()), false,
                        // Steam chose plain HTTP here and verifies the content itself,
                        // so a certificate mismatch on this upgraded link is accepted.
                        delegate { return true; }))
                    {
                        ssl.AuthenticateAsClient(host, null, (SslProtocols)3072 /* TLS 1.2 */, false);
                        ssl.Write(first, 0, firstLen);
                        ssl.Flush();
                        Net.Bridge(cs, ssl);
                    }
                }
                else
                {
                    server.Connect(ip, port);
                    var ss = server.GetStream();
                    Net.SendFragmented(ss, first, 0, firstLen, 200);
                    Net.Bridge(cs, ss);
                }
            }
        }

        // Extract server_name from a TLS ClientHello record
        static string ParseSni(byte[] b)
        {
            try
            {
                int p = 5;
                if (b[p] != 1) return null;       // ClientHello
                p += 4;                           // handshake header
                p += 2 + 32;                      // version + random
                p += 1 + b[p];                    // session id
                p += 2 + ((b[p] << 8) | b[p + 1]); // cipher suites
                p += 1 + b[p];                    // compression
                int extEnd = p + 2 + ((b[p] << 8) | b[p + 1]);
                p += 2;
                while (p + 4 <= extEnd)
                {
                    int type = (b[p] << 8) | b[p + 1];
                    int len = (b[p + 2] << 8) | b[p + 3];
                    p += 4;
                    if (type == 0)
                    {
                        int q = p + 2; // list length
                        if (b[q] == 0)
                        {
                            int nlen = (b[q + 1] << 8) | b[q + 2];
                            return Encoding.ASCII.GetString(b, q + 3, nlen);
                        }
                    }
                    p += len;
                }
            }
            catch { }
            return null;
        }

        static int errors;
        protected override void OnError(Exception ex)
        {
            if (Interlocked.Increment(ref errors) <= 200) Log.Write("Download relay (" + port + ") error: " + ex.Message);
        }
    }

    // Store/Community: HTTP proxy that fragments the first packet of each connection
    class FragProxy : LoopbackServer
    {
        static readonly string[] SteamDomains = {
            "steampowered.com", "steamcommunity.com", "steamstatic.com", "steamgames.com",
            "steam-chat.com", "s.team", "steamcdn-a.akamaihd.net", "steamusercontent.com" };

        int port;

        public void Start(int p) { port = p; StartOn(p); }

        string Pac()
        {
            var sb = new StringBuilder("function FindProxyForURL(url, host) {\n  var d = [");
            for (int i = 0; i < SteamDomains.Length; i++)
                sb.Append(i > 0 ? "," : "").Append("'").Append(SteamDomains[i]).Append("'");
            sb.Append("];\n  for (var i = 0; i < d.length; i++) if (host == d[i] || dnsDomainIs(host, '.' + d[i])) return 'PROXY 127.0.0.1:")
              .Append(port).Append("';\n  return 'DIRECT';\n}\n");
            return sb.ToString();
        }

        protected override void Handle(TcpClient client)
        {
            var cs = client.GetStream();
            var buf = new byte[65536];
            int hdrEnd;
            int n = Net.ReadHeaders(cs, buf, out hdrEnd);
            if (n == 0) return;
            string header = Encoding.ASCII.GetString(buf, 0, hdrEnd);
            string[] first = header.Substring(0, header.IndexOf("\r\n")).Split(' ');
            if (first.Length < 3) return;

            if (first[0] != "CONNECT" && first[1].StartsWith("/proxy.pac"))
            {
                var body = Encoding.ASCII.GetBytes(Pac());
                var resp = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/x-ns-proxy-autoconfig\r\nContent-Length: "
                    + body.Length + "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
                cs.Write(resp, 0, resp.Length);
                cs.Write(body, 0, body.Length);
                return;
            }

            bool isConnect = first[0] == "CONNECT";
            string host; int dport; byte[] pending = null;
            if (isConnect)
            {
                var hp = first[1].Split(':');
                host = hp[0];
                dport = hp.Length > 1 ? int.Parse(hp[1]) : 443;
            }
            else
            {
                Uri uri;
                if (!Uri.TryCreate(first[1], UriKind.Absolute, out uri)) return;
                host = uri.Host; dport = uri.Port;
                string rest = header.Substring(header.IndexOf("\r\n") + 2);
                var head = Encoding.ASCII.GetBytes(first[0] + " " + uri.PathAndQuery + " " + first[2] + "\r\n" + rest);
                pending = new byte[head.Length + (n - hdrEnd)];
                Buffer.BlockCopy(head, 0, pending, 0, head.Length);
                Buffer.BlockCopy(buf, hdrEnd, pending, head.Length, n - hdrEnd);
            }

            using (var server = new TcpClient())
            {
                server.NoDelay = true;
                server.Connect(host, dport);
                var ss = server.GetStream();
                if (isConnect)
                {
                    var ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                    cs.Write(ok, 0, ok.Length);
                    int r = cs.Read(buf, 0, buf.Length);
                    if (r <= 0) return;
                    Net.SendFragmented(ss, buf, 0, r, 200);
                }
                else Net.SendFragmented(ss, pending, 0, pending.Length, pending.Length);
                Net.Bridge(cs, ss);
            }
        }
    }
}

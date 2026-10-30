// תוכנת רקע לקיצורי המקלדת של האוזניות.
// רושמת את Ctrl+Alt+N/T/R/A/B ברמת המערכת, כך שהם עובדים מכל חלון וגוברים על קיצורי דפדפן.
// כל לחיצה מפעילה את openscq30.exe (גרסת שורת הפקודה) בלי חלון. הודעה קופצת רק בבקשת
// סוללה או כשמשהו נכשל.
// הידור (מהדר C# 5 שמובנה ב־Windows):
//   csc /target:winexe /codepage:65001 /out:SoundcoreHotkeys.exe SoundcoreHotkeys.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

static class SoundcoreHotkeys
{
    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    static extern int GetMessage(out Msg msg, IntPtr hWnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [StructLayout(LayoutKind.Sequential)]
    struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int x;
        public int y;
    }

    const uint ModAlt = 0x1, ModControl = 0x2, ModNoRepeat = 0x4000, WmHotkey = 0x312;
    const uint IconInfo = 0x40, IconWarning = 0x30;
    // ימין לשמאל, מיושר לימין, מעל כל החלונות ומקבל פוקוס
    const uint RtlTopmost = 0x80000 | 0x100000 | 0x40000 | 0x10000;
    const string Title = "אוזניות Soundcore";
    const string NotConnected = "לא הצלחתי להתחבר לאוזניות.\n\n"
        + "ודא שהן דלוקות, מחוץ לקופסה ומחוברות למחשב,\n"
        + "ושהתוכנה \"אוזניות Soundcore\" סגורה.";

    // לכל קיצור: האות, הערך שנשלח לתוכנה, והשם בעברית להודעות
    static readonly string[][] Keys =
    {
        new[] { "N", "NoiseCanceling", "סינון רעשים" },
        new[] { "T", "Transparency", "מצב שקוף" },
        new[] { "R", "Normal", "מצב רגיל" },
        new[] { "A", "Adaptive", "אדפטיבי" },
        new[] { "B", "Battery", "סוללה" },
    };

    static readonly string CliPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "openscq30.exe");
    // 1 בזמן שפקודה רצה, כדי שלחיצות מהירות לא יפתחו כמה חיבורים לאוזניות בבת אחת
    static int busy;
    static string mac;

    static void Main()
    {
        bool isFirstInstance;
        using (new Mutex(true, "SoundcoreHotkeys-single-instance", out isFirstInstance))
        {
            if (!isFirstInstance) return;

            var failed = new StringBuilder();
            for (int i = 0; i < Keys.Length; i++)
            {
                if (!RegisterHotKey(IntPtr.Zero, i + 1, ModControl | ModAlt | ModNoRepeat, Keys[i][0][0]))
                    failed.AppendLine("Ctrl+Alt+" + Keys[i][0] + "  (" + Keys[i][2] + ")");
            }
            if (failed.Length > 0)
                Show("הקיצורים האלה תפוסים על ידי תוכנה אחרת ולא יעבדו:\n\n" + failed, IconWarning);

            Msg msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message != WmHotkey) continue;
                int index = (int)msg.wParam - 1;
                if (index < 0 || index >= Keys.Length) continue;
                if (Interlocked.Exchange(ref busy, 1) == 1) continue;

                string action = Keys[index][1];
                var worker = new Thread(() =>
                {
                    try { Run(action); }
                    finally { busy = 0; }
                });
                worker.IsBackground = true;
                worker.Start();
            }
        }
    }

    static void Run(string action)
    {
        if (mac == null) mac = PairedMac();
        if (mac == null)
        {
            Show("לא נמצאו אוזניות Liberty 5 Pro ברשימת התוכנה.\n"
                + "פתח את \"אוזניות Soundcore\" והוסף אותן.", IconWarning);
            return;
        }

        string args = "device --mac-address " + mac + " setting " + (action == "Battery"
            ? "-g batteryLevelLeft -g batteryLevelRight -g caseBatteryLevel"
            : "--set ambientSoundMode=" + action);
        string output;
        if (Exec(args, out output) != 0)
        {
            Show(NotConnected, IconWarning);
            return;
        }
        if (action == "Battery") Show(BatteryText(output), IconInfo);
    }

    // הכתובת של האוזניות כפי שהתוכנה שמרה אותן, כדי שלא תהיה כתובה כאן קבוע
    static string PairedMac()
    {
        string output;
        if (Exec("paired-devices list --json", out output) != 0) return null;
        var match = Regex.Match(output,
            "\"macAddress\"\\s*:\\s*\"([0-9A-Fa-f:]+)\"\\s*,\\s*\"model\"\\s*:\\s*\"SoundcoreD1203\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    static string BatteryText(string output)
    {
        string left = "?", right = "?", caseLevel = "?";
        foreach (string line in output.Split('\n'))
        {
            string[] parts = line.Split('\t');
            if (parts.Length < 2) continue;
            string percent = parts[1].Trim().Split('/')[0] + "%";
            switch (parts[0].Trim())
            {
                case "batteryLevelLeft": left = percent; break;
                case "batteryLevelRight": right = percent; break;
                case "caseBatteryLevel": caseLevel = percent; break;
            }
        }
        // הקופסה מדווחת 1 עד שהאוזניות היו בתוכה מאז החיבור
        if (caseLevel == "1%") caseLevel = "לא ידוע (צריך להכניס את האוזניות לקופסה)";
        return "שמאל:  " + left + "\nימין:  " + right + "\nקופסה:  " + caseLevel;
    }

    static int Exec(string args, out string output)
    {
        var info = new ProcessStartInfo(CliPath, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using (var process = Process.Start(info))
        {
            // stderr נקרא במקביל כדי שהתהליך לא ייתקע על באפר מלא
            var stderr = process.StandardError.ReadToEndAsync();
            output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(30000))
            {
                try { process.Kill(); } catch { }
                return -1;
            }
            stderr.Wait();
            return process.ExitCode;
        }
    }

    static void Show(string text, uint icon)
    {
        MessageBox(IntPtr.Zero, text, Title, icon | RtlTopmost);
    }
}

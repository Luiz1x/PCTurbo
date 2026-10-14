using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace PCTurbo
{
    // ------------------------------------------------------------------
    // Log: mostra na janela e grava um arquivo na Area de Trabalho
    // ------------------------------------------------------------------
    public static class Log
    {
        public static Action<string> Sink;
        private static readonly object fileLock = new object();

        public static void Write(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
            Action<string> s = Sink;
            if (s != null)
            {
                try { s(line); } catch { }
            }
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PCTurbo_log.txt");
                lock (fileLock)
                {
                    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd ") + line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------
    // Chamadas nativas do Windows
    // ------------------------------------------------------------------
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemTimes(
            out System.Runtime.InteropServices.ComTypes.FILETIME idle,
            out System.Runtime.InteropServices.ComTypes.FILETIME kernel,
            out System.Runtime.InteropServices.ComTypes.FILETIME user);

        public static ulong ToUlong(System.Runtime.InteropServices.ComTypes.FILETIME ft)
        {
            return (((ulong)(uint)ft.dwHighDateTime) << 32) | (uint)ft.dwLowDateTime;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
    }

    // ------------------------------------------------------------------
    // Formatacao
    // ------------------------------------------------------------------
    public static class Fmt
    {
        public static string Bytes(long b)
        {
            double v = b;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (v >= 1024 && i < units.Length - 1)
            {
                v /= 1024;
                i++;
            }
            return v.ToString(i == 0 ? "0" : "0.0") + " " + units[i];
        }
    }

    // ------------------------------------------------------------------
    // Pastas: tamanho e limpeza (ignora arquivos em uso)
    // ------------------------------------------------------------------
    public static class Dirs
    {
        public static long Size(string path)
        {
            long total = 0;
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return 0;

                foreach (string f in Directory.GetFiles(path))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
                foreach (string d in Directory.GetDirectories(path))
                {
                    try
                    {
                        if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue;
                        total += Size(d);
                    }
                    catch { }
                }
            }
            catch { }
            return total;
        }

        public static long DeleteContents(string path)
        {
            long freed = 0;
            try
            {
                if (File.Exists(path))
                {
                    long fileLen = new FileInfo(path).Length;
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    return fileLen;
                }
                if (!Directory.Exists(path)) return 0;

                foreach (string f in Directory.GetFiles(path))
                {
                    try
                    {
                        long len = new FileInfo(f).Length;
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                        freed += len;
                    }
                    catch { }
                }
                foreach (string d in Directory.GetDirectories(path))
                {
                    try
                    {
                        if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue;
                        freed += DeleteContents(d);
                        Directory.Delete(d);
                    }
                    catch { }
                }
            }
            catch { }
            return freed;
        }
    }

    // ------------------------------------------------------------------
    // Executar programas externos
    // ------------------------------------------------------------------
    public static class Proc
    {
        private static Encoding ConsoleEncoding()
        {
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { return Encoding.Default; }
        }

        private static string Clean(string s)
        {
            if (s == null) return "";
            s = s.Replace("\0", "");
            if (s.IndexOf('\r') >= 0)
            {
                string[] parts = s.Split('\r');
                string last = "";
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Trim().Length > 0) last = parts[i];
                }
                s = last;
            }
            return s.Trim();
        }

        // Roda e espera terminar; envia cada linha de saida para onLine (pode ser null)
        public static int Run(string file, string args, Action<string> onLine)
        {
            return Run(file, args, onLine, ConsoleEncoding());
        }

        public static int Run(string file, string args, Action<string> onLine, Encoding enc)
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = enc;
            psi.StandardErrorEncoding = enc;

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                DataReceivedEventHandler handler = delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null || onLine == null) return;
                    string t = Clean(e.Data);
                    if (t.Length > 1) onLine(t);
                };
                p.OutputDataReceived += handler;
                p.ErrorDataReceived += handler;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        // Roda uma linha de comando pelo cmd.exe (janelas do proprio programa podem aparecer)
        public static int RunCmd(string commandLine)
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + commandLine + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        // Roda um script PowerShell (codificado, sem problemas de aspas)
        public static int PowerShell(string script, Action<string> onLine)
        {
            string full = "$ProgressPreference='SilentlyContinue';" + script;
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
            return Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded, delegate(string t)
            {
                if (t.StartsWith("#<") || t.StartsWith("<")) return;
                if (onLine != null) onLine(t);
            });
        }
    }

    // ------------------------------------------------------------------
    // Registro do Windows
    // ------------------------------------------------------------------
    public static class R
    {
        public static void Set(RegistryKey root, string path, string name, object value, RegistryValueKind kind)
        {
            using (RegistryKey k = root.CreateSubKey(path))
            {
                if (k != null) k.SetValue(name, value, kind);
            }
        }

        public static void SetDword(RegistryKey root, string path, string name, int value)
        {
            Set(root, path, name, value, RegistryValueKind.DWord);
        }

        public static void SetString(RegistryKey root, string path, string name, string value)
        {
            Set(root, path, name, value, RegistryValueKind.String);
        }

        public static void Del(RegistryKey root, string path, string name)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(path, true))
                {
                    if (k != null) k.DeleteValue(name, false);
                }
            }
            catch { }
        }

        public static string Str(RegistryKey k, string name)
        {
            try
            {
                object o = k.GetValue(name);
                return o == null ? "" : o.ToString();
            }
            catch { return ""; }
        }

        public static int Int(RegistryKey k, string name)
        {
            try
            {
                object o = k.GetValue(name);
                if (o is int) return (int)o;
            }
            catch { }
            return 0;
        }
    }
}

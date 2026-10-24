using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCTurbo
{
    public class GuardItem
    {
        public string Name = "";
        public string Path = "";
        public string Version = "";
        public bool MetaOnly;   // true = so confere se o arquivo existe (nao abre o arquivo)
    }

    // Vigia de programas: tira uma "foto" (so leitura) dos launchers e programas importantes
    // antes e depois do OTIMIZAR TUDO. Se algum sumiu ou voltou para uma versao antiga
    // (como aconteceu com a Epic), o app avisa. Nunca altera nada: so le arquivos.
    public static class Guard
    {
        private static void Add(List<GuardItem> l, string name, string path, bool metaOnly = false)
        {
            if (path == null || path.Length == 0) return;
            GuardItem g = new GuardItem();
            g.Name = name;
            g.Path = path;
            g.MetaOnly = metaOnly;
            l.Add(g);
        }

        private static string SteamPath()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null);
                if (v != null) return v.ToString().Replace('/', '\\');
            }
            catch { }
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\steam.exe");
        }

        private static List<GuardItem> Candidates()
        {
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string sys = Environment.GetEnvironmentVariable("SystemDrive");
            if (string.IsNullOrEmpty(sys)) sys = "C:";

            List<GuardItem> l = new List<GuardItem>();
            Add(l, "Epic Games Launcher", System.IO.Path.Combine(pf86, @"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"));
            Add(l, "Epic Games Launcher (32 bits)", System.IO.Path.Combine(pf86, @"Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe"));
            Add(l, "Steam", SteamPath());
            Add(l, "Riot Client", sys + @"\Riot Games\Riot Client\RiotClientServices.exe", true);
            Add(l, "Valorant", sys + @"\Riot Games\VALORANT\live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe", true);
            Add(l, "League of Legends", sys + @"\Riot Games\League of Legends\LeagueClient.exe", true);
            Add(l, "Riot Vanguard", System.IO.Path.Combine(pf, @"Riot Vanguard\vgc.exe"), true);
            Add(l, "Battle.net", System.IO.Path.Combine(pf86, @"Battle.net\Battle.net.exe"));
            Add(l, "Rockstar Games Launcher", System.IO.Path.Combine(pf, @"Rockstar Games\Launcher\Launcher.exe"));
            Add(l, "Discord", System.IO.Path.Combine(local, @"Discord\Update.exe"));
            Add(l, "Google Chrome", System.IO.Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"));
            Add(l, "Google Chrome (32 bits)", System.IO.Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"));
            Add(l, "Driver NVIDIA", System.IO.Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"));
            return l;
        }

        // Foto do que existe agora (so o que esta instalado)
        public static List<GuardItem> Snapshot()
        {
            List<GuardItem> r = new List<GuardItem>();
            foreach (GuardItem c in Candidates())
            {
                try
                {
                    if (!File.Exists(c.Path)) continue;
                    if (c.MetaOnly) { c.Version = ""; r.Add(c); continue; }
                    FileVersionInfo fv = FileVersionInfo.GetVersionInfo(c.Path);
                    string v = fv.FileVersion;
                    if (string.IsNullOrEmpty(v)) v = fv.ProductVersion;
                    c.Version = v == null ? "" : v.Trim();
                    r.Add(c);
                }
                catch { }
            }
            return r;
        }

        private static System.Version Parse(string s)
        {
            if (s == null) return null;
            Match m = Regex.Match(s, "\\d+(\\.\\d+){1,3}");
            if (!m.Success) return null;
            System.Version v;
            return System.Version.TryParse(m.Value, out v) ? v : null;
        }

        // Compara as duas fotos e devolve avisos em portugues (lista vazia = tudo igual ou melhor)
        public static List<string> Compare(List<GuardItem> before, List<GuardItem> after)
        {
            List<string> w = new List<string>();
            foreach (GuardItem b in before)
            {
                GuardItem a = null;
                foreach (GuardItem x in after)
                {
                    if (x.Name == b.Name) { a = x; break; }
                }
                if (a == null)
                {
                    w.Add(b.Name + " não está mais no lugar de antes (" + b.Path + "). Reinstale pelo site oficial se não abrir.");
                    continue;
                }
                System.Version vb = Parse(b.Version);
                System.Version va = Parse(a.Version);
                if (vb != null && va != null)
                {
                    if (va < vb) w.Add(b.Name + " voltou para uma versão mais antiga (" + b.Version + " para " + a.Version + ").");
                }
                else if (b.Version != a.Version)
                {
                    w.Add(b.Name + " mudou de versão (" + b.Version + " para " + a.Version + ").");
                }
            }
            return w;
        }
    }

    public static class Machine
    {
        // true em notebook (tem bateria). Em notebook o app nao liga os ajustes de energia que gastam bateria.
        public static bool IsLaptop()
        {
            try
            {
                BatteryChargeStatus s = SystemInformation.PowerStatus.BatteryChargeStatus;
                if (s == BatteryChargeStatus.Unknown) return false;
                return (s & BatteryChargeStatus.NoSystemBattery) == 0;
            }
            catch { return false; }
        }
    }
}

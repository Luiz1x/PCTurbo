using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PCTurbo
{
    // Configuracoes simples guardadas em %APPDATA%\PCTurbo (listas e liga/desliga).
    // A janela principal e o icone da bandeja sao programas separados, entao os dois leem os mesmos arquivos.
    public static class Settings
    {
        public static readonly string[] DefaultGames =
        {
            "VALORANT-Win64-Shipping", "League of Legends", "RocketLeague", "GTA5", "GTA5_Enhanced",
            "FortniteClient-Win64-Shipping", "cs2", "RobloxPlayerBeta"
        };

        private static readonly object fileLock = new object();

        public static string Dir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCTurbo");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }

        // Tira espacos e o ".exe" do final
        public static string Clean(string s)
        {
            if (s == null) return "";
            s = s.Trim();
            if (s.ToLowerInvariant().EndsWith(".exe")) s = s.Substring(0, s.Length - 4);
            return s.Trim();
        }

        public static bool Has(List<string> list, string name)
        {
            if (list == null) return false;
            foreach (string x in list)
            {
                if (string.Equals(x, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // Le a lista. Se o arquivo ainda nao existe, devolve os valores padrao (se houver).
        public static List<string> ReadList(string file, string[] defaults)
        {
            List<string> r = new List<string>();
            string p = Path.Combine(Dir(), file);
            try
            {
                lock (fileLock)
                {
                    if (File.Exists(p))
                    {
                        foreach (string line in File.ReadAllLines(p, Encoding.UTF8))
                        {
                            string n = Clean(line);
                            if (n.Length > 0 && !Has(r, n)) r.Add(n);
                        }
                        return r;
                    }
                }
            }
            catch { }
            if (defaults != null)
            {
                foreach (string d in defaults)
                {
                    string n = Clean(d);
                    if (n.Length > 0 && !Has(r, n)) r.Add(n);
                }
            }
            return r;
        }

        public static void WriteList(string file, List<string> items)
        {
            try
            {
                lock (fileLock)
                {
                    File.WriteAllLines(Path.Combine(Dir(), file), items.ToArray(), Encoding.UTF8);
                }
            }
            catch { }
        }

        private static string ConfigFile()
        {
            return Path.Combine(Dir(), "config.txt");
        }

        public static bool GetBool(string key, bool def)
        {
            try
            {
                lock (fileLock)
                {
                    string f = ConfigFile();
                    if (!File.Exists(f)) return def;
                    foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                    {
                        int i = line.IndexOf('=');
                        if (i <= 0) continue;
                        if (line.Substring(0, i).Trim() == key) return line.Substring(i + 1).Trim() == "1";
                    }
                }
            }
            catch { }
            return def;
        }

        public static void SetBool(string key, bool value)
        {
            try
            {
                lock (fileLock)
                {
                    string f = ConfigFile();
                    List<string> lines = new List<string>();
                    bool done = false;
                    if (File.Exists(f))
                    {
                        foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                        {
                            int i = line.IndexOf('=');
                            if (i > 0 && line.Substring(0, i).Trim() == key)
                            {
                                lines.Add(key + "=" + (value ? "1" : "0"));
                                done = true;
                            }
                            else if (line.Trim().Length > 0) lines.Add(line);
                        }
                    }
                    if (!done) lines.Add(key + "=" + (value ? "1" : "0"));
                    File.WriteAllLines(f, lines.ToArray(), Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}

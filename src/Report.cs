using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace PCTurbo
{
    // Foto do estado do PC (processos, RAM, CPU parada, disco)
    public class Snap
    {
        public string When = "";
        public int Procs;
        public int Threads;
        public long RamUsedMB;
        public long RamTotalMB;
        public double CpuAvg = -1;
        public long DiskFreeMB;
        public long DiskTotalMB;
    }

    // Guarda o "antes" na primeira otimizacao e compara com o "depois" quando a pessoa pede.
    // Tudo fica em %APPDATA%\PCTurbo\ganho.txt. Nada e enviado para a internet.
    // O relatorio nao leva nome de usuario nem arquivos: so o hardware e os numeros.
    public static class Report
    {
        private static readonly object fileLock = new object();

        private static string DataDir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCTurbo");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }

        private static string DataFile()
        {
            return Path.Combine(DataDir(), "ganho.txt");
        }

        public static string ReportFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PCTurbo_relatorio.txt");
        }

        private static Dictionary<string, string> Load()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            try
            {
                string f = DataFile();
                if (!File.Exists(f)) return d;
                foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            }
            catch { }
            return d;
        }

        private static void Save(Dictionary<string, string> d)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> kv in d)
                {
                    sb.AppendLine(kv.Key + "=" + kv.Value);
                }
                File.WriteAllText(DataFile(), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string N(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static double ReadNum(Dictionary<string, string> d, string key, double def)
        {
            string s;
            double v;
            if (d.TryGetValue(key, out s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return def;
        }

        // ---- foto do sistema ----
        public static Snap Take()
        {
            Snap s = new Snap();
            s.When = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            try
            {
                Process[] all = Process.GetProcesses();
                s.Procs = all.Length;
                int th = 0;
                foreach (Process p in all)
                {
                    try { th += p.Threads.Count; }
                    catch { }
                    try { p.Dispose(); }
                    catch { }
                }
                s.Threads = th;
            }
            catch { }

            try
            {
                Native.MEMORYSTATUSEX m = new Native.MEMORYSTATUSEX();
                if (Native.GlobalMemoryStatusEx(m))
                {
                    s.RamTotalMB = (long)(m.ullTotalPhys / (1024 * 1024));
                    s.RamUsedMB = (long)((m.ullTotalPhys - m.ullAvailPhys) / (1024 * 1024));
                }
            }
            catch { }

            try
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                DriveInfo c = new DriveInfo(Path.GetPathRoot(win));
                s.DiskFreeMB = c.AvailableFreeSpace / (1024 * 1024);
                s.DiskTotalMB = c.TotalSize / (1024 * 1024);
            }
            catch { }

            // CPU media: 5 leituras de meio segundo
            try
            {
                using (PerformanceCounter pc = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    pc.NextValue();
                    double sum = 0;
                    int n = 0;
                    for (int i = 0; i < 5; i++)
                    {
                        System.Threading.Thread.Sleep(500);
                        sum += pc.NextValue();
                        n++;
                    }
                    if (n > 0) s.CpuAvg = sum / n;
                }
            }
            catch { }

            return s;
        }

        private static void Put(Dictionary<string, string> d, string prefix, Snap s)
        {
            d[prefix + "when"] = s.When;
            d[prefix + "procs"] = s.Procs.ToString(CultureInfo.InvariantCulture);
            d[prefix + "threads"] = s.Threads.ToString(CultureInfo.InvariantCulture);
            d[prefix + "ram_used"] = s.RamUsedMB.ToString(CultureInfo.InvariantCulture);
            d[prefix + "ram_total"] = s.RamTotalMB.ToString(CultureInfo.InvariantCulture);
            d[prefix + "cpu"] = N(s.CpuAvg);
            d[prefix + "disk_free"] = s.DiskFreeMB.ToString(CultureInfo.InvariantCulture);
            d[prefix + "disk_total"] = s.DiskTotalMB.ToString(CultureInfo.InvariantCulture);
        }

        private static Snap Get(Dictionary<string, string> d, string prefix)
        {
            Snap s = new Snap();
            string w;
            if (d.TryGetValue(prefix + "when", out w)) s.When = w;
            s.Procs = (int)ReadNum(d, prefix + "procs", 0);
            s.Threads = (int)ReadNum(d, prefix + "threads", 0);
            s.RamUsedMB = (long)ReadNum(d, prefix + "ram_used", 0);
            s.RamTotalMB = (long)ReadNum(d, prefix + "ram_total", 0);
            s.CpuAvg = ReadNum(d, prefix + "cpu", -1);
            s.DiskFreeMB = (long)ReadNum(d, prefix + "disk_free", 0);
            s.DiskTotalMB = (long)ReadNum(d, prefix + "disk_total", 0);
            return s;
        }

        // Guarda o resumo da ultima otimizacao: lixo que tinha, o que foi limpo, o que foi corrigido, avisos
        public static void SaveRun(long found, List<string> cleaned, List<string> fixes, List<string> warns)
        {
            try
            {
                lock (fileLock)
                {
                    Dictionary<string, string> d = Load();
                    List<string> old = new List<string>();
                    foreach (string k in d.Keys)
                    {
                        if (k.StartsWith("ult_")) old.Add(k);
                    }
                    foreach (string k in old) d.Remove(k);

                    d["ult_quando"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                    d["ult_achado"] = N(found);
                    if (!d.ContainsKey("primeiro_achado")) d["primeiro_achado"] = N(found);
                    PutList(d, "ult_l_", cleaned);
                    PutList(d, "ult_f_", fixes);
                    PutList(d, "ult_w_", warns);
                    Save(d);
                }
            }
            catch { }
        }

        private static void PutList(Dictionary<string, string> d, string prefix, List<string> items)
        {
            if (items == null) return;
            d[prefix + "n"] = items.Count.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < items.Count; i++)
            {
                d[prefix + i.ToString(CultureInfo.InvariantCulture)] = items[i].Replace("\r", " ").Replace("\n", " ");
            }
        }

        private static List<string> GetList(Dictionary<string, string> d, string prefix)
        {
            List<string> r = new List<string>();
            int n = (int)ReadNum(d, prefix + "n", 0);
            for (int i = 0; i < n; i++)
            {
                string v;
                if (d.TryGetValue(prefix + i.ToString(CultureInfo.InvariantCulture), out v)) r.Add(v);
            }
            return r;
        }

        public static bool LoadRun(out long found, out string when, out List<string> cleaned, out List<string> fixes, out List<string> warns)
        {
            lock (fileLock)
            {
                Dictionary<string, string> d = Load();
                found = (long)ReadNum(d, "ult_achado", 0);
                string w;
                when = d.TryGetValue("ult_quando", out w) ? w : "";
                cleaned = GetList(d, "ult_l_");
                fixes = GetList(d, "ult_f_");
                warns = GetList(d, "ult_w_");
                return when.Length > 0;
            }
        }

        // Medicoes de FPS (which = "antes" ou "depois")
        public static void SaveFps(string which, FpsResult r)
        {
            try
            {
                lock (fileLock)
                {
                    Dictionary<string, string> d = Load();
                    string p = "fps_" + which + "_";
                    d[p + "proc"] = r.Proc;
                    d[p + "when"] = r.When;
                    d[p + "avg"] = N(r.Avg);
                    d[p + "low1"] = N(r.Low1);
                    d[p + "low01"] = N(r.Low01);
                    d[p + "frames"] = r.Frames.ToString(CultureInfo.InvariantCulture);
                    d[p + "secs"] = r.Secs.ToString(CultureInfo.InvariantCulture);
                    Save(d);
                }
            }
            catch { }
        }

        public static FpsResult LoadFps(string which)
        {
            lock (fileLock)
            {
                Dictionary<string, string> d = Load();
                string p = "fps_" + which + "_";
                if (!d.ContainsKey(p + "avg")) return null;
                FpsResult r = new FpsResult();
                string v;
                if (d.TryGetValue(p + "proc", out v)) r.Proc = v;
                if (d.TryGetValue(p + "when", out v)) r.When = v;
                r.Avg = ReadNum(d, p + "avg", 0);
                r.Low1 = ReadNum(d, p + "low1", 0);
                r.Low01 = ReadNum(d, p + "low01", 0);
                r.Frames = (int)ReadNum(d, p + "frames", 0);
                r.Secs = (int)ReadNum(d, p + "secs", 0);
                return r;
            }
        }

        public static string FpsLine(FpsResult r)
        {
            return N(Math.Round(r.Avg, 1)) + " FPS médio   |   1% low: " + N(Math.Round(r.Low1, 1)) + "   |   0,1% low: " + N(Math.Round(r.Low01, 1)) +
                "   (" + r.Proc + ", " + r.Secs + " s, " + r.Frames + " quadros, " + r.When + ")";
        }

        // Para a aba "Meu ganho": o "antes" guardado (ou null) e os totais
        public static Snap LoadBefore()
        {
            lock (fileLock)
            {
                Dictionary<string, string> d = Load();
                if (!d.ContainsKey("antes_when")) return null;
                return Get(d, "antes_");
            }
        }

        public static void Totals(out long cleanedBytes, out int cleanings, out int runs, out string last)
        {
            lock (fileLock)
            {
                Dictionary<string, string> d = Load();
                cleanedBytes = (long)ReadNum(d, "limpo_bytes", 0);
                cleanings = (int)ReadNum(d, "limpezas", 0);
                runs = (int)ReadNum(d, "otimizacoes", 0);
                string l;
                last = d.TryGetValue("ultima_otimizacao", out l) ? l : "";
            }
        }

        // better: 1 melhorou, -1 piorou, 0 igual
        public static string Diff(double before, double after, string unit, bool lowerIsBetter, out int better)
        {
            double diff = after - before;
            if (Math.Abs(diff) < 0.0001) better = 0;
            else better = ((diff < 0) == lowerIsBetter) ? 1 : -1;
            string sign = diff > 0 ? "+" : (diff < 0 ? "-" : "");
            double pct = before > 0 ? Math.Abs(diff) / before * 100.0 : 0;
            return sign + N(Math.Round(Math.Abs(diff), 1)) + unit + " (" + pct.ToString("0") + "%)";
        }

        // Chamado no comeco do OTIMIZAR TUDO. So grava o "antes" uma vez (o primeiro de todos).
        public static void StartBaseline()
        {
            try
            {
                lock (fileLock)
                {
                    Dictionary<string, string> d = Load();
                    if (d.ContainsKey("antes_when")) return;
                    Snap s = Take();
                    Put(d, "antes_", s);
                    Save(d);
                    Log.Write("Relatório: foto do PC \"antes\" guardada (" + s.Procs + " processos, RAM em uso " + s.RamUsedMB + " MB).");
                }
            }
            catch (Exception ex) { Log.Write("Relatório: não consegui guardar o antes: " + ex.Message); }
        }

        // Soma o espaco liberado (otimizacao, limpeza semanal e automatica)
        public static void AddCleaned(long bytes, bool isOptimize, int tweaksOk)
        {
            try
            {
                lock (fileLock)
                {
                    Dictionary<string, string> d = Load();
                    double total = ReadNum(d, "limpo_bytes", 0) + (bytes > 0 ? bytes : 0);
                    d["limpo_bytes"] = N(total);
                    d["limpezas"] = N(ReadNum(d, "limpezas", 0) + 1);
                    if (isOptimize)
                    {
                        d["otimizacoes"] = N(ReadNum(d, "otimizacoes", 0) + 1);
                        d["ultima_otimizacao"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        d["ajustes_ultima"] = tweaksOk.ToString(CultureInfo.InvariantCulture);
                    }
                    Save(d);
                }
            }
            catch { }
        }

        // ---- hardware (sem dados pessoais) ----
        private static string Reg(string key, string name)
        {
            try
            {
                object v = Registry.GetValue(key, name, null);
                if (v != null) return v.ToString().Trim();
            }
            catch { }
            return "";
        }

        private static string WindowsName()
        {
            string k = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string build = Reg(k, "CurrentBuild");
            int b = 0;
            int.TryParse(build, out b);
            string name = b >= 22000 ? "Windows 11" : "Windows 10";
            string ver = Reg(k, "DisplayVersion");
            if (ver.Length == 0) ver = Reg(k, "ReleaseId");
            return name + (ver.Length > 0 ? " " + ver : "") + (build.Length > 0 ? " (build " + build + ")" : "");
        }

        private static string Gpu()
        {
            string baseKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\";
            string found = "";
            for (int i = 0; i < 4; i++)
            {
                string n = Reg(baseKey + i.ToString("0000"), "DriverDesc");
                if (n.Length == 0) continue;
                if (found.Length > 0) found += " + ";
                found += n;
            }
            return found.Length > 0 ? found : "não identificada";
        }

        private static string Change(double before, double after, string unit, bool lowerIsBetter)
        {
            double diff = after - before;
            string sign = diff > 0 ? "+" : (diff < 0 ? "-" : "");
            string verdict;
            if (Math.Abs(diff) < 0.0001) verdict = "igual";
            else if ((diff < 0) == lowerIsBetter) verdict = "melhorou";
            else verdict = "piorou";
            double pct = before > 0 ? Math.Abs(diff) / before * 100.0 : 0;
            return N(Math.Round(before, 1)) + unit + "  ->  " + N(Math.Round(after, 1)) + unit + "   (" + sign + N(Math.Round(Math.Abs(diff), 1)) + unit + ", " + pct.ToString("0") + "%)  " + verdict;
        }

        // Monta o texto do relatorio (demora ~3 s por causa da leitura da CPU)
        public static string Build()
        {
            Dictionary<string, string> d;
            lock (fileLock) { d = Load(); }
            Snap now = Take();
            bool hasBase = d.ContainsKey("antes_when");
            Snap before = hasBase ? Get(d, "antes_") : null;

            StringBuilder sb = new StringBuilder();
            string line = "------------------------------------------------------------";
            sb.AppendLine("PC TURBO - RELATORIO DE GANHO");
            sb.AppendLine(line);
            sb.AppendLine("App: versão " + Updater.Current + "      Gerado em: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            sb.AppendLine("Processador: " + Reg(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") + "  (" + Environment.ProcessorCount + " núcleos lógicos)");
            sb.AppendLine("Placa de vídeo: " + Gpu());
            sb.AppendLine("Memória RAM: " + N(Math.Round(now.RamTotalMB / 1024.0, 1)) + " GB");
            sb.AppendLine("Sistema: " + WindowsName());
            sb.AppendLine("Disco C: " + N(Math.Round(now.DiskTotalMB / 1024.0, 0)) + " GB");
            sb.AppendLine();

            if (!hasBase)
            {
                sb.AppendLine("AINDA NÃO TEM \"ANTES\".");
                sb.AppendLine("O \"antes\" é guardado quando você clica em OTIMIZAR TUDO pela primeira vez.");
                sb.AppendLine("Clique em OTIMIZAR TUDO, reinicie o PC e gere este relatório de novo.");
                sb.AppendLine();
                sb.AppendLine("Como o PC está agora:");
                sb.AppendLine("  Programas rodando: " + now.Procs + "   Threads: " + now.Threads);
                sb.AppendLine("  RAM em uso: " + now.RamUsedMB + " MB");
                if (now.CpuAvg >= 0) sb.AppendLine("  CPU média (alguns segundos): " + N(Math.Round(now.CpuAvg, 1)) + "%");
                sb.AppendLine("  Espaço livre no C: " + N(Math.Round(now.DiskFreeMB / 1024.0, 1)) + " GB");
            }
            else
            {
                sb.AppendLine("ANTES (" + before.When + ")  ->  DEPOIS (" + now.When + ")");
                sb.AppendLine(line);
                sb.AppendLine("Programas rodando : " + Change(before.Procs, now.Procs, "", true));
                sb.AppendLine("Threads           : " + Change(before.Threads, now.Threads, "", true));
                sb.AppendLine("RAM em uso        : " + Change(before.RamUsedMB, now.RamUsedMB, " MB", true));
                if (before.CpuAvg >= 0 && now.CpuAvg >= 0)
                    sb.AppendLine("CPU média (parado): " + Change(before.CpuAvg, now.CpuAvg, "%", true));
                sb.AppendLine("Livre no C:       : " + Change(Math.Round(before.DiskFreeMB / 1024.0, 1), Math.Round(now.DiskFreeMB / 1024.0, 1), " GB", false));
            }

            sb.AppendLine();
            sb.AppendLine("LIMPEZA E OTIMIZAÇÕES (somando tudo desde que instalou)");
            sb.AppendLine(line);
            double cleaned = ReadNum(d, "limpo_bytes", 0);
            sb.AppendLine("Lixo removido: " + Fmt.Bytes((long)cleaned) + " em " + (int)ReadNum(d, "limpezas", 0) + " limpezas (manual, semanal e em segundo plano)");
            sb.AppendLine("OTIMIZAR TUDO usado: " + (int)ReadNum(d, "otimizacoes", 0) + " vez(es)");
            string last;
            if (d.TryGetValue("ultima_otimizacao", out last))
                sb.AppendLine("Última otimização: " + last + " (" + (int)ReadNum(d, "ajustes_ultima", 0) + " ajustes aplicados)");

            sb.AppendLine();
            sb.AppendLine("FPS (teste opcional com o PresentMon)");
            sb.AppendLine(line);
            FpsResult fa = LoadFps("antes");
            FpsResult fd = LoadFps("depois");
            if (fa == null && fd == null)
            {
                sb.AppendLine("Não medido. Para provar o ganho no jogo: aba Meu ganho > Teste de FPS (meça antes e depois).");
                sb.AppendLine("Ou anote à mão -> Jogo: ______________   FPS antes: ______   FPS depois: ______");
            }
            else
            {
                if (fa != null) sb.AppendLine("ANTES : " + FpsLine(fa));
                if (fd != null) sb.AppendLine("DEPOIS: " + FpsLine(fd));
                if (fa != null && fd != null)
                {
                    sb.AppendLine("FPS médio : " + Change(fa.Avg, fd.Avg, " FPS", false));
                    sb.AppendLine("1% low    : " + Change(fa.Low1, fd.Low1, " FPS", false));
                    sb.AppendLine("0,1% low  : " + Change(fa.Low01, fd.Low01, " FPS", false));
                    if (!string.Equals(fa.Proc, fd.Proc, StringComparison.OrdinalIgnoreCase))
                        sb.AppendLine("ATENÇÃO: as duas medições foram em programas diferentes, então não dá para comparar.");
                }
            }
            sb.AppendLine();
            sb.AppendLine("COMO LER ESTES NÚMEROS");
            sb.AppendLine("- Os números variam conforme o que estava aberto na hora. Para comparar de");
            sb.AppendLine("  verdade, gere o relatório logo depois de reiniciar o PC, sem abrir nada.");
            sb.AppendLine("- Menos programas, menos RAM e menos CPU parada = PC mais leve em segundo plano.");
            sb.AppendLine("- Isso não é FPS. O ganho em jogo depende do PC. O jeito certo de provar é medir o");
            sb.AppendLine("  FPS do mesmo jogo, no mesmo lugar e do mesmo jeito, antes e depois. Uma partida");
            sb.AppendLine("  nunca é igual a outra, então diferenças pequenas (1 ou 2%) são ruído.");
            sb.AppendLine("- Este arquivo não tem nome de usuário nem arquivos pessoais.");
            return sb.ToString();
        }

        // Gera, salva na Area de Trabalho e devolve o texto
        public static string Generate()
        {
            string text = Build();
            try { File.WriteAllText(ReportFile(), text, Encoding.UTF8); }
            catch (Exception ex) { Log.Write("Relatório: não consegui salvar: " + ex.Message); }
            Log.Write("Relatório de ganho salvo em: " + ReportFile());
            return text;
        }
    }
}

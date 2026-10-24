using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PCTurbo
{
    public class FpsResult
    {
        public string Proc = "";
        public string When = "";
        public double Avg;
        public double Low1;
        public double Low01;
        public int Frames;
        public int Secs;
    }

    // Teste de FPS opcional, usando o PresentMon (ferramenta gratuita da Intel, codigo aberto).
    // Ele so le, pelo proprio Windows (ETW), quando cada quadro foi apresentado na tela.
    // Nao injeta nada no jogo e nao altera nada. Roda so quando a pessoa clica em medir.
    public static class FpsTest
    {
        private static string DataDir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCTurbo");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }

        public static string ToolPath()
        {
            return Path.Combine(DataDir(), "PresentMon.exe");
        }

        // Acha o PresentMon: o que o app baixou, ou qualquer PresentMon*.exe que a pessoa tenha colocado na pasta
        public static string FindTool()
        {
            string p = ToolPath();
            if (File.Exists(p)) return p;
            try
            {
                string[] other = Directory.GetFiles(DataDir(), "PresentMon*.exe");
                if (other.Length > 0) return other[0];
            }
            catch { }
            return null;
        }

        // Devolve null se o PresentMon esta pronto, ou a mensagem de erro
        public static string EnsureTool()
        {
            if (FindTool() != null) return null;
            try
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
                string json;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "PCTurbo");
                    wc.Headers.Add("Accept", "application/vnd.github+json");
                    json = wc.DownloadString("https://api.github.com/repos/GameTechDev/PresentMon/releases/latest");
                }
                string url = null;
                foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+\\.exe)\""))
                {
                    string u = m.Groups[1].Value;
                    string low = u.ToLowerInvariant();
                    if (!low.Contains("presentmon")) continue;
                    if (low.Contains("setup") || low.Contains("install")) continue;
                    if (url == null || (low.Contains("x64") && !url.ToLowerInvariant().Contains("x64"))) url = u;
                }
                if (url == null)
                    return "Não achei o arquivo do PresentMon na página oficial. Baixe o PresentMon (GameTechDev/PresentMon no GitHub), renomeie para PresentMon.exe e coloque na pasta " + DataDir();

                Log.Write("Baixando o PresentMon (ferramenta da Intel): " + url);
                string tmp = ToolPath() + ".baixando";
                using (WebClient wc2 = new WebClient())
                {
                    wc2.Headers.Add("User-Agent", "PCTurbo");
                    wc2.DownloadFile(url, tmp);
                }
                if (!File.Exists(tmp) || new FileInfo(tmp).Length < 100000)
                {
                    try { File.Delete(tmp); } catch { }
                    return "O download do PresentMon veio incompleto.";
                }
                if (File.Exists(ToolPath())) File.Delete(ToolPath());
                File.Move(tmp, ToolPath());

                // Confere quem assinou o arquivo (so informa no log)
                string sig = "";
                Proc.PowerShell("$s=Get-AuthenticodeSignature -LiteralPath '" + ToolPath().Replace("'", "''") + "'; Write-Output ($s.Status.ToString() + '|' + $s.SignerCertificate.Subject)",
                    delegate(string line) { if (line.Contains("|")) sig = line.Trim(); });
                if (sig.StartsWith("Valid") && sig.ToLowerInvariant().Contains("intel"))
                    Log.Write("PresentMon: assinatura válida da Intel.");
                else
                    Log.Write("PresentMon: não consegui confirmar a assinatura (" + sig + "). Baixado do repositório oficial GameTechDev/PresentMon.");
                return null;
            }
            catch (Exception ex)
            {
                return "Não consegui baixar o PresentMon: " + ex.Message + ". Baixe o PresentMon (GameTechDev/PresentMon no GitHub), renomeie para PresentMon.exe e coloque na pasta " + DataDir();
            }
        }

        // Roda o PresentMon por 'secs' segundos no processo 'proc'. Devolve null se deu certo, ou o erro.
        public static string Measure(string proc, int secs, out FpsResult res)
        {
            res = null;
            string tool = FindTool();
            if (tool == null) return "PresentMon não encontrado.";
            string csv = Path.Combine(DataDir(), "fps_medicao.csv");
            string lastOut = "";

            // 1a tentativa: opcoes do PresentMon 2.x. 2a: opcoes do PresentMon 1.x
            string[] argSets = new string[2];
            argSets[0] = "--process_name \"" + proc + "\" --output_file \"" + csv + "\" --timed " + secs + " --terminate_after_timed --no_console_stats --stop_existing_session --session_name PCTurboFps";
            argSets[1] = "-process_name \"" + proc + "\" -output_file \"" + csv + "\" -timed " + secs + " -terminate_after_timed -stop_existing_session -session_name PCTurboFps";

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try { if (File.Exists(csv)) File.Delete(csv); } catch { }
                DateTime start = DateTime.Now;
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(tool, argSets[attempt]);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.WorkingDirectory = DataDir();
                    StringBuilder sb = new StringBuilder();
                    using (Process p = Process.Start(psi))
                    {
                        p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null && sb.Length < 2000) sb.AppendLine(e.Data); };
                        p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null && sb.Length < 2000) sb.AppendLine(e.Data); };
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        if (!p.WaitForExit((secs + 40) * 1000))
                        {
                            try { p.Kill(); } catch { }
                        }
                    }
                    lastOut = sb.ToString().Trim();
                }
                catch (Exception ex)
                {
                    lastOut = ex.Message;
                }

                if (File.Exists(csv) && new FileInfo(csv).Length > 200) break;
                // Se terminou em poucos segundos sem gerar arquivo, provavelmente nao entendeu as opcoes: tenta o outro jeito
                if ((DateTime.Now - start).TotalSeconds > 12) break;
            }

            if (!File.Exists(csv))
            {
                return "O PresentMon não gerou resultado. " + (lastOut.Length > 0 ? "Ele disse: " + Short(lastOut) : "O jogo estava aberto?");
            }

            string err;
            FpsResult r = ParseCsv(csv, proc, secs, out err);
            try { File.Delete(csv); } catch { }
            if (r == null) return err;
            res = r;
            return null;
        }

        private static string Short(string s)
        {
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 220 ? s.Substring(0, 220) + "..." : s;
        }

        internal static FpsResult ParseCsv(string csv, string proc, int secs, out string err)
        {
            err = null;
            List<double> ms = new List<double>();
            try
            {
                using (FileStream fs = new FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs))
                {
                    string header = sr.ReadLine();
                    if (header == null) { err = "O arquivo de medição veio vazio."; return null; }
                    string[] cols = header.Split(',');
                    int idx = -1;
                    for (int i = 0; i < cols.Length; i++)
                    {
                        if (cols[i].Trim() == "MsBetweenPresents") { idx = i; break; }
                    }
                    if (idx < 0)
                    {
                        for (int i = 0; i < cols.Length; i++)
                        {
                            if (cols[i].Trim() == "FrameTime") { idx = i; break; }
                        }
                    }
                    if (idx < 0) { err = "Não entendi o formato do arquivo do PresentMon (sem coluna de tempo de quadro)."; return null; }

                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] f = line.Split(',');
                        if (f.Length <= idx) continue;
                        double v;
                        if (!double.TryParse(f[idx], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                        if (v <= 0.01 || v > 10000) continue;
                        ms.Add(v);
                    }
                }
            }
            catch (Exception ex)
            {
                err = "Não consegui ler o resultado: " + ex.Message;
                return null;
            }

            if (ms.Count < 120)
            {
                err = "Captei só " + ms.Count + " quadros de " + proc + ". O jogo estava aberto, em primeiro plano e com o nome do programa certo?";
                return null;
            }

            double sum = 0;
            foreach (double v in ms) sum += v;
            double mean = sum / ms.Count;

            ms.Sort();
            FpsResult r = new FpsResult();
            r.Proc = proc;
            r.When = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            r.Secs = secs;
            r.Frames = ms.Count;
            r.Avg = 1000.0 / mean;
            r.Low1 = 1000.0 / WorstAverage(ms, 0.01);
            r.Low01 = 1000.0 / WorstAverage(ms, 0.001);
            return r;
        }

        // media dos tempos de quadro dos piores 'frac' (os mais lentos). A lista ja esta em ordem crescente.
        private static double WorstAverage(List<double> sorted, double frac)
        {
            int n = (int)Math.Ceiling(sorted.Count * frac);
            if (n < 1) n = 1;
            double s = 0;
            for (int i = sorted.Count - n; i < sorted.Count; i++) s += sorted[i];
            return s / n;
        }
    }
}

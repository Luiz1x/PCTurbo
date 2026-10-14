using System;
using System.Text;

namespace PCTurbo
{
    public class DiagResult
    {
        public string Refresh = "não consegui ler";
        public string Ram = "não consegui ler";
        public string Bar = "não consegui ler (nvidia-smi indisponível)";
        public string Driver = "não consegui ler";
        public string Plan = "não consegui ler";
        public bool PlanKnown, PlanOk;
        public bool RefreshOk, RamOk, BarOk;
        public bool RefreshKnown, RamKnown, BarKnown;
    }

    // Verificacoes que nao se resolvem por software: so avisa o que esta fora do ideal
    public static class Diag
    {
        public static DiagResult Run()
        {
            DiagResult d = new DiagResult();

            try
            {
                string script =
                    "$r=(Get-CimInstance Win32_VideoController | Where-Object {$_.CurrentRefreshRate} | Select-Object -First 1).CurrentRefreshRate;" +
                    "\"REF|$r\";" +
                    "$m=@(Get-CimInstance Win32_PhysicalMemory);" +
                    "\"RAM|$($m.Count)|$(($m | Select-Object -First 1).ConfiguredClockSpeed)|$(($m | Select-Object -First 1).Speed)\"";
                Proc.PowerShell(script, delegate(string line)
                {
                    string[] p = line.Split('|');
                    if (p.Length >= 2 && p[0] == "REF")
                    {
                        int hz;
                        if (int.TryParse(p[1].Trim(), out hz) && hz > 0)
                        {
                            d.RefreshKnown = true;
                            d.RefreshOk = hz >= 200;
                            d.Refresh = hz + " Hz" + (d.RefreshOk ? "" : " - seu monitor faz 240 Hz: ajuste em Configurações > Tela > Tela avançada");
                        }
                    }
                    else if (p.Length >= 4 && p[0] == "RAM")
                    {
                        int count, conf, spd;
                        int.TryParse(p[1].Trim(), out count);
                        int.TryParse(p[2].Trim(), out conf);
                        int.TryParse(p[3].Trim(), out spd);
                        int mhz = conf > 0 ? conf : spd;
                        if (count > 0)
                        {
                            d.RamKnown = true;
                            d.RamOk = count >= 2 && mhz >= 3000;
                            string txt = count + " pente(s) a " + mhz + " MHz";
                            if (count < 2) txt += " - só 1 pente = single channel (perde desempenho)";
                            else if (mhz < 3000) txt += " - abaixo de 3200: veja XMP/velocidade na BIOS";
                            else txt += " - ok";
                            d.Ram = txt;
                        }
                    }
                });
            }
            catch { }

            try
            {
                long total = 0;
                bool[] inBar = new bool[1];
                Proc.Run("nvidia-smi.exe", "-q -d MEMORY", delegate(string line)
                {
                    if (total != 0) return;
                    if (line.IndexOf("BAR1", StringComparison.OrdinalIgnoreCase) >= 0) inBar[0] = true;
                    else if (inBar[0] && line.StartsWith("Total"))
                    {
                        string[] p = line.Split(':');
                        long v;
                        if (p.Length > 1 && long.TryParse(p[1].Replace("MiB", "").Trim(), out v)) total = v;
                    }
                });
                if (total > 0)
                {
                    d.BarKnown = true;
                    d.BarOk = total > 256;
                    d.Bar = d.BarOk ? "Ativo (BAR1 = " + total + " MiB)"
                        : "Desligado (BAR1 = " + total + " MiB) - ative 'Above 4G Decoding' e 'Re-Size BAR' na BIOS";
                }
            }
            catch { }

            try
            {
                StringBuilder sb = new StringBuilder();
                Proc.Run("nvidia-smi.exe", "--query-gpu=driver_version --format=csv,noheader", delegate(string line) { sb.Append(line.Trim()); });
                string v = sb.ToString().Trim();
                if (v.Length > 0 && v.Length < 20) d.Driver = v + " (confira se é o mais novo em nvidia.com/drivers; instale em modo Limpo)";
            }
            catch { }

            try
            {
                string plan = "";
                Proc.Run("powercfg.exe", "/getactivescheme", delegate(string line)
                {
                    int a = line.IndexOf('(');
                    int b = line.LastIndexOf(')');
                    if (a >= 0 && b > a) plan = line.Substring(a + 1, b - a - 1);
                });
                if (plan.Length > 0)
                {
                    d.PlanKnown = true;
                    string l = plan.ToLowerInvariant();
                    d.PlanOk = l.Contains("desempenho") || l.Contains("ultimate") || l.Contains("alto desempenho") || l.Contains("high performance") || l.Contains("turbo");
                    d.Plan = plan + (d.PlanOk ? "" : " - use o ajuste de Desempenho Máximo na aba Otimização");
                }
            }
            catch { }

            return d;
        }
    }
}
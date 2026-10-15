using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace PCTurbo
{
    // Fica na bandeja (perto do relogio) e limpa o lixo sozinho quando ele passa de um limite.
    // Nunca limpa com jogo ou tela cheia aberta, para nao atrapalhar o desempenho.
    public class TrayApp : ApplicationContext
    {
        private const long TempLimit = 3L * 1024 * 1024 * 1024;     // 3 GB de lixo temporario
        private const long TempLimitLowDisk = 300L * 1024 * 1024;   // 300 MB se o disco estiver quase cheio
        private const double LowDiskPercent = 10.0;                 // disco C: com menos de 10% livre
        private const long BinLimit = 2L * 1024 * 1024 * 1024;      // 2 GB na Lixeira
        private const int CheckMinutes = 10;
        private const int CooldownMinutes = 30;

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out int state);

        private NotifyIcon icon;
        private System.Windows.Forms.Timer timer;
        private Control sync;
        private bool working;
        private DateTime lastClean = DateTime.MinValue;
        private Balancer balancer = new Balancer();
        private System.Windows.Forms.Timer balanceTimer;

        public TrayApp()
        {
            sync = new Control();
            IntPtr h = sync.Handle;

            icon = new NotifyIcon();
            try { icon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { icon.Icon = SystemIcons.Application; }
            icon.Text = "PC Turbo: limpeza automática ligada";

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Abrir o PC Turbo", null, delegate { OpenMain(); });
            menu.Items.Add("Limpar agora", null, delegate { Check(true); });
            ToolStripMenuItem bal = new ToolStripMenuItem("Equilibrar processos em segundo plano (estilo ProBalance)");
            bal.CheckOnClick = true;
            bal.Checked = true;
            bal.CheckedChanged += delegate { balancer.Enabled = bal.Checked; if (!bal.Checked) balancer.RestoreAll(); };
            menu.Items.Add(bal);
            menu.Items.Add("Sair (desliga a limpeza automática até o próximo login)", null, delegate { Quit(); });
            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate { OpenMain(); };
            icon.Visible = true;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = CheckMinutes * 60 * 1000;
            timer.Tick += delegate { Check(false); };
            timer.Start();

            balanceTimer = new System.Windows.Forms.Timer();
            balanceTimer.Interval = 3000;
            balanceTimer.Tick += delegate { try { balancer.Tick(); } catch { } };
            balanceTimer.Start();

            Log.Write("Limpeza automática em segundo plano iniciada.");
        }

        private void OpenMain()
        {
            try { Process.Start(Application.ExecutablePath); }
            catch (Exception ex) { Log.Write("Não consegui abrir o PC Turbo: " + ex.Message); }
        }

        private void Quit()
        {
            timer.Stop();
            balanceTimer.Stop();
            balancer.RestoreAll();
            icon.Visible = false;
            icon.Dispose();
            ExitThread();
        }

        private static bool GameOrFullscreen()
        {
            try
            {
                int state;
                if (SHQueryUserNotificationState(out state) == 0)
                {
                    // 2 = tela cheia, 3 = jogo em tela cheia, 4 = apresentacao
                    return state == 2 || state == 3 || state == 4;
                }
            }
            catch { }
            return false;
        }

        private void Check(bool force)
        {
            if (working) return;
            if (!force)
            {
                if (GameOrFullscreen()) return;
                if ((DateTime.Now - lastClean).TotalMinutes < CooldownMinutes) return;
            }
            working = true;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    long temp = Dirs.Size(Path.GetTempPath()) + Dirs.Size(Path.Combine(win, "Temp"));

                    double freePct = 100.0;
                    try
                    {
                        DriveInfo c = new DriveInfo(Path.GetPathRoot(win));
                        freePct = 100.0 * c.AvailableFreeSpace / c.TotalSize;
                    }
                    catch { }

                    long bin = 0;
                    foreach (CleanTarget t in Cleaner.Targets())
                    {
                        if (t.Name == "Lixeira") { try { bin = t.Size(); } catch { } }
                    }

                    bool need = force || temp > TempLimit || bin > BinLimit || (freePct < LowDiskPercent && temp > TempLimitLowDisk);
                    if (!need) return;

                    Log.Write("Limpeza automática: lixo temporário em " + Fmt.Bytes(temp) + ", Lixeira com " + Fmt.Bytes(bin) + ", disco C: com " + freePct.ToString("0") + "% livre.");
                    long freed = 0;
                    foreach (CleanTarget t in Cleaner.Targets())
                    {
                        if (!t.Default) continue;
                        if (!force && GameOrFullscreen()) break;
                        try { freed += t.Clean(); }
                        catch (Exception ex) { Log.Write("Falhou: " + t.Name + " - " + ex.Message); }
                    }
                    lastClean = DateTime.Now;
                    Log.Write("Limpeza automática concluída. Espaço liberado: " + Fmt.Bytes(freed));
                    string msg = "Limpei " + Fmt.Bytes(freed) + " de lixo (temporários e Lixeira).";
                    try
                    {
                        sync.BeginInvoke(new Action(delegate { icon.ShowBalloonTip(4000, "PC Turbo", msg, ToolTipIcon.Info); }));
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    Log.Write("Erro na limpeza automática: " + ex.Message);
                }
                finally
                {
                    working = false;
                }
            });
        }
    }

    // Parecido com o ProBalance do Process Lasso: quando o processador fica no limite,
    // baixa a prioridade de programas em segundo plano que estao pesando, e devolve depois.
    // Nunca mexe no programa que esta na frente, em jogos/anti-cheat, drivers ou no Windows.
    public class Balancer
    {
        public bool Enabled = true;

        private const int HighPercent = 85;
        private const int LowPercent = 60;
        private const double HogShare = 12.0;   // % do processador total para ser considerado pesado

        private static readonly Regex Protected = new Regex(
            "^(system|idle|registry|memcompression|smss|csrss|wininit|winlogon|services|lsass|svchost|dwm|explorer|fontdrvhost|audiodg|sihost|taskhostw|ctfmon|searchhost|searchindexer|startmenuexperiencehost|shellexperiencehost|textinputhost|runtimebroker|securityhealth.*|msmpeng|nissrv|nvcontainer|nvdisplay.*|nvidia.*|nvspcap.*|nvtelemetry.*|vgc|vgtray|riot.*|valorant.*|league.*|easyanticheat.*|beservice|battleye.*|xbox.*|gamebar.*|gamingservices.*|steam.*|epicgames.*|epicwebhelper|discord.*|obs.*|pcturbo|msedgewebview2|attack.*|havit.*|conhost|wudfhost|dllhost|lsaiso|spoolsv)$",
            RegexOptions.IgnoreCase);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        private ulong prevIdle, prevTotal;
        private bool hasPrev;
        private int highTicks, lowTicks;
        private DateTime lastSample = DateTime.MinValue;
        private Dictionary<int, TimeSpan> lastCpu = new Dictionary<int, TimeSpan>();
        private Dictionary<int, ProcessPriorityClass> lowered = new Dictionary<int, ProcessPriorityClass>();

        private int CpuPercent()
        {
            System.Runtime.InteropServices.ComTypes.FILETIME i, k, u;
            if (!Native.GetSystemTimes(out i, out k, out u)) return -1;
            ulong idle = Native.ToUlong(i);
            ulong total = Native.ToUlong(k) + Native.ToUlong(u);
            if (!hasPrev)
            {
                prevIdle = idle;
                prevTotal = total;
                hasPrev = true;
                return -1;
            }
            ulong dIdle = idle - prevIdle;
            ulong dTotal = total - prevTotal;
            prevIdle = idle;
            prevTotal = total;
            if (dTotal == 0) return -1;
            return (int)(100 - (dIdle * 100 / dTotal));
        }

        private static int ForegroundPid()
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                return (int)pid;
            }
            catch { return -1; }
        }

        public void Tick()
        {
            int cpu = CpuPercent();
            if (!Enabled || cpu < 0) return;

            int fg = ForegroundPid();

            // Se um programa baixado voltou para a frente, devolve a prioridade na hora
            if (lowered.Count > 0 && lowered.ContainsKey(fg)) RestoreOne(fg);

            if (cpu >= HighPercent) { highTicks++; lowTicks = 0; }
            else if (cpu <= LowPercent) { lowTicks++; highTicks = 0; }
            else { highTicks = 0; }

            if (highTicks >= 3) LowerHogs(fg);
            else if (highTicks == 0 && lastCpu.Count > 0) lastCpu.Clear();

            if (lowTicks >= 5 && lowered.Count > 0) RestoreAll();
        }

        private void LowerHogs(int fg)
        {
            DateTime now = DateTime.Now;
            double dt = lastSample == DateTime.MinValue ? 0 : (now - lastSample).TotalSeconds;
            lastSample = now;
            int me = Process.GetCurrentProcess().Id;
            Dictionary<int, TimeSpan> current = new Dictionary<int, TimeSpan>();

            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    int pid = p.Id;
                    if (pid == me || pid == fg || p.SessionId == 0) continue;
                    if (Protected.IsMatch(p.ProcessName)) continue;

                    TimeSpan t = p.TotalProcessorTime;
                    current[pid] = t;

                    TimeSpan before;
                    if (dt > 0.5 && lastCpu.TryGetValue(pid, out before) && !lowered.ContainsKey(pid))
                    {
                        double share = (t - before).TotalSeconds / (dt * Environment.ProcessorCount) * 100.0;
                        if (share >= HogShare && p.PriorityClass == ProcessPriorityClass.Normal)
                        {
                            p.PriorityClass = ProcessPriorityClass.BelowNormal;
                            lowered[pid] = ProcessPriorityClass.Normal;
                            Log.Write("Equilíbrio de CPU: baixei a prioridade de " + p.ProcessName + " (usava " + share.ToString("0") + "% do processador em segundo plano).");
                        }
                    }
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }
            lastCpu = current;
        }

        private void RestoreOne(int pid)
        {
            ProcessPriorityClass orig;
            if (!lowered.TryGetValue(pid, out orig)) return;
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    p.PriorityClass = orig;
                }
            }
            catch { }
            lowered.Remove(pid);
        }

        public void RestoreAll()
        {
            List<int> ids = new List<int>(lowered.Keys);
            foreach (int pid in ids) RestoreOne(pid);
            if (ids.Count > 0) Log.Write("Equilíbrio de CPU: devolvi a prioridade normal de " + ids.Count + " programa(s).");
        }
    }
}

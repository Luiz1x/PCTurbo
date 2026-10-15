using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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
        private const int CheckMinutes = 10;
        private const int CooldownMinutes = 30;

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out int state);

        private NotifyIcon icon;
        private System.Windows.Forms.Timer timer;
        private Control sync;
        private bool working;
        private DateTime lastClean = DateTime.MinValue;

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
            menu.Items.Add("Sair (desliga a limpeza automática até o próximo login)", null, delegate { Quit(); });
            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate { OpenMain(); };
            icon.Visible = true;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = CheckMinutes * 60 * 1000;
            timer.Tick += delegate { Check(false); };
            timer.Start();

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

                    bool need = force || temp > TempLimit || (freePct < LowDiskPercent && temp > TempLimitLowDisk);
                    if (!need) return;

                    Log.Write("Limpeza automática: lixo temporário em " + Fmt.Bytes(temp) + ", disco C: com " + freePct.ToString("0") + "% livre.");
                    long freed = 0;
                    foreach (CleanTarget t in Cleaner.Targets())
                    {
                        if (!t.Default || t.Name == "Lixeira") continue;
                        if (!force && GameOrFullscreen()) break;
                        try { freed += t.Clean(); }
                        catch (Exception ex) { Log.Write("Falhou: " + t.Name + " - " + ex.Message); }
                    }
                    lastClean = DateTime.Now;
                    Log.Write("Limpeza automática concluída. Espaço liberado: " + Fmt.Bytes(freed));
                    string msg = "Limpei " + Fmt.Bytes(freed) + " de lixo temporário.";
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
}

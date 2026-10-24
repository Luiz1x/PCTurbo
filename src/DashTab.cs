using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCTurbo
{
    public class DashTab : UserControl
    {
        private Label lblOs;
        private Label lblCpu;
        private Label lblGpu;
        private Label lblBoard;
        private Label lblUp;
        private Label lblHz, lblRamInfo, lblBar, lblDriver, lblPlan, lblDisk, lblBoot;
        private ProgressBar barCpu, barRam, barDisk, barGpuTemp, barGpuUse, barVram;
        private Label valCpu, valRam, valDisk, valGpuTemp, valGpuUse, valVram;
        private System.Windows.Forms.Timer timer;
        private ulong prevIdle;
        private ulong prevTotal;
        private bool hasPrev;
        private int tick;
        private bool gpuBusy;

        public Action QuickAction;
        public Action UndoAction;
        public Action ReportAction;
        public Action PreviewAction;
        public CheckBox ChkFull;

        public DashTab()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(16);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;

            flow.Controls.Add(Section("Comece por aqui"));
            Label note = new Label();
            note.Width = 900;
            note.Height = 84;
            note.Text = "Cria um ponto de restauração, limpa os arquivos inúteis e aplica os ajustes recomendados (os mesmos que já vêm marcados nas abas Limpeza e Otimização), as configs de FPS da NVIDIA, o relógio preciso para jogos, o TRIM do SSD e a limpeza semanal automática. É só um clique: ele faz tudo sem perguntar nada e, no fim, oferece reiniciar.";
            flow.Controls.Add(note);

            Button big = Ui.BigButton("OTIMIZAR TUDO (1 clique)", 420, 64, delegate
            {
                if (QuickAction != null) QuickAction();
            });
            big.Margin = new Padding(6);
            flow.Controls.Add(big);

            ChkFull = new CheckBox();
            ChkFull.Text = "Incluir manutenção completa (reparar arquivos do Windows e limpar componentes: alguns minutos). Não atualiza nenhum programa";
            ChkFull.AutoSize = true;
            ChkFull.Margin = new Padding(8, 4, 0, 4);
            flow.Controls.Add(ChkFull);

            Label safe = new Label();
            safe.Width = 900;
            safe.Height = 86;
            safe.Margin = new Padding(6, 4, 0, 0);
            safe.Padding = new Padding(10, 6, 10, 6);
            safe.BackColor = Color.FromArgb(18, 48, 34);
            safe.ForeColor = Color.FromArgb(150, 232, 182);
            safe.Text = "Garantia: o OTIMIZAR TUDO nunca remove nada que você possa usar ou precisar. Não desinstala programas, não apaga jogos, fotos, downloads nem documentos (a Lixeira é esvaziada: só o que você já apagou), e não mexe em Xbox, Game Pass, Riot/Vanguard, Epic, Steam, Discord nem nos drivers. " +
                "Ele só limpa lixo temporário (caches) e muda ajustes do Windows que você pode desfazer com o botão Desfazer.";
            flow.Controls.Add(safe);

            flow.Controls.Add(Section("Seu computador"));
            lblOs = Info(flow, "Sistema");
            lblCpu = Info(flow, "Processador");
            lblGpu = Info(flow, "Placa de vídeo");
            lblBoard = Info(flow, "Placa-mãe");
            lblUp = Info(flow, "Ligado há");

            flow.Controls.Add(Section("Uso agora"));
            Meter(flow, "Processador", out barCpu, out valCpu);
            Meter(flow, "Memória RAM", out barRam, out valRam);
            Meter(flow, "Disco C:", out barDisk, out valDisk);
            Meter(flow, "GPU: temperatura", out barGpuTemp, out valGpuTemp);
            Meter(flow, "GPU: uso", out barGpuUse, out valGpuUse);
            Meter(flow, "GPU: memória", out barVram, out valVram);

            flow.Controls.Add(Section("Diagnóstico para jogos"));
            lblHz = Info(flow, "Taxa da tela");
            lblRamInfo = Info(flow, "Memória RAM");
            lblBar = Info(flow, "Resizable BAR");
            lblPlan = Info(flow, "Plano de energia");
            lblDisk = Info(flow, "Saúde do disco");
            lblBoot = Info(flow, "Secure Boot / TPM");
            lblDriver = Info(flow, "Driver NVIDIA");
            Button bDiag = Ui.Btn("Atualizar diagnóstico", 190, delegate { RunDiag(); });
            Button bGame = Ui.Btn("Modo Jogo (fechar apps)", 210, delegate
            {
                using (GameModeForm f = new GameModeForm()) { f.ShowDialog(this); }
            });
            Button bUndo = Ui.Btn("Desfazer ajustes do Otimizar", 230, delegate
            {
                if (UndoAction != null) UndoAction();
            });
            FlowLayoutPanel diagButtons = Ui.Buttons(bDiag, bGame, bUndo);
            diagButtons.WrapContents = false;
            diagButtons.AutoSize = true;
            flow.Controls.Add(diagButtons);

            Button bRep = Ui.Btn("Gerar relatório de ganho (antes e depois)", 320, delegate
            {
                if (ReportAction != null) ReportAction();
            });
            Button bPrev = Ui.Btn("Ver o que vai mudar (sem aplicar)", 280, delegate
            {
                if (PreviewAction != null) PreviewAction();
            });
            FlowLayoutPanel repButtons = Ui.Buttons(bPrev, bRep);
            repButtons.WrapContents = false;
            repButtons.AutoSize = true;
            flow.Controls.Add(repButtons);


            this.Controls.Add(flow);

            LoadStatic();
            RunDiag();

            CpuPercent();

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += OnTick;
            timer.Start();
        }

        private void RunDiag()
        {
            lblHz.Text = lblRamInfo.Text = lblBar.Text = lblPlan.Text = lblDriver.Text = lblDisk.Text = lblBoot.Text = "verificando...";
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                DiagResult d = Diag.Run();
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        ShowDiag(lblHz, d.Refresh, d.RefreshKnown, d.RefreshOk);
                        ShowDiag(lblRamInfo, d.Ram, d.RamKnown, d.RamOk);
                        ShowDiag(lblBar, d.Bar, d.BarKnown, d.BarOk);
                        ShowDiag(lblPlan, d.Plan, d.PlanKnown, d.PlanOk);
                        ShowDiag(lblDisk, d.Disk, d.DiskKnown, d.DiskOk);
                        ShowDiag(lblBoot, d.Boot, d.BootKnown, d.BootOk);
                        lblDriver.Text = d.Driver;
                    }));
                }
                catch { }
            });
        }

        private static void ShowDiag(Label l, string text, bool known, bool ok)
        {
            l.Text = text;
            l.ForeColor = !known ? Theme.Text : (ok ? Theme.AccentText : Theme.Warn);
        }

        private Label Section(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Width = 900;
            l.Height = 32;
            l.Margin = new Padding(0, 14, 0, 2);
            l.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            l.ForeColor = Theme.AccentText;
            l.TextAlign = ContentAlignment.BottomLeft;
            return l;
        }

        private Label Info(FlowLayoutPanel host, string title)
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.FlowDirection = FlowDirection.LeftToRight;
            row.WrapContents = false;
            row.Width = 900;
            row.Height = 28;
            row.Margin = new Padding(0);

            Label t = new Label();
            t.Text = title;
            t.Width = 160;
            t.Height = 24;
            t.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            t.TextAlign = ContentAlignment.MiddleLeft;

            Label v = new Label();
            v.Text = "...";
            v.Width = 720;
            v.Height = 24;
            v.TextAlign = ContentAlignment.MiddleLeft;

            row.Controls.Add(t);
            row.Controls.Add(v);
            host.Controls.Add(row);
            return v;
        }

        private void Meter(FlowLayoutPanel host, string title, out ProgressBar bar, out Label val)
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.FlowDirection = FlowDirection.LeftToRight;
            row.WrapContents = false;
            row.Width = 900;
            row.Height = 30;
            row.Margin = new Padding(0);

            Label t = new Label();
            t.Text = title;
            t.Width = 160;
            t.Height = 24;
            t.TextAlign = ContentAlignment.MiddleLeft;

            ProgressBar pb = new FlatBar();
            pb.Minimum = 0;
            pb.Maximum = 100;
            pb.Width = 400;
            pb.Height = 20;
            pb.Margin = new Padding(3, 5, 3, 3);

            Label v = new Label();
            v.Text = "...";
            v.Width = 320;
            v.Height = 24;
            v.TextAlign = ContentAlignment.MiddleLeft;

            row.Controls.Add(t);
            row.Controls.Add(pb);
            row.Controls.Add(v);
            host.Controls.Add(row);

            bar = pb;
            val = v;
        }

        private void LoadStatic()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k != null)
                    {
                        int build = 0;
                        int.TryParse(R.Str(k, "CurrentBuildNumber"), out build);
                        string name = build >= 22000 ? "Windows 11" : "Windows 10";
                        lblOs.Text = name + " " + R.Str(k, "EditionID") + " " + R.Str(k, "DisplayVersion") +
                            " (compilação " + R.Str(k, "CurrentBuild") + "." + R.Int(k, "UBR") + ")";
                    }
                }
            }
            catch { lblOs.Text = "Windows"; }

            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (k != null)
                        lblCpu.Text = R.Str(k, "ProcessorNameString").Trim() + " (" + Environment.ProcessorCount + " threads)";
                }
            }
            catch { }

            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (k != null)
                    {
                        string maker = R.Str(k, "BaseBoardManufacturer").Trim();
                        string product = R.Str(k, "BaseBoardProduct").Trim();
                        lblBoard.Text = (maker + " " + product).Trim();
                    }
                }
            }
            catch { }

            lblGpu.Text = "lendo...";
        }

        private void OnTick(object sender, EventArgs e)
        {
            tick++;

            try
            {
                int cpu = CpuPercent();
                if (cpu >= 0)
                {
                    if (cpu > 100) cpu = 100;
                    barCpu.Value = cpu;
                    valCpu.Text = cpu + " %";
                }
            }
            catch { }

            try
            {
                Native.MEMORYSTATUSEX m = new Native.MEMORYSTATUSEX();
                if (Native.GlobalMemoryStatusEx(m))
                {
                    long total = (long)m.ullTotalPhys;
                    long used = total - (long)m.ullAvailPhys;
                    int pct = (int)m.dwMemoryLoad;
                    if (pct > 100) pct = 100;
                    barRam.Value = pct;
                    valRam.Text = Fmt.Bytes(used) + " de " + Fmt.Bytes(total) + " (" + pct + " %)";
                }
            }
            catch { }

            try
            {
                DriveInfo c = new DriveInfo("C");
                long tot = c.TotalSize;
                long free = c.AvailableFreeSpace;
                int pct = (int)(((tot - free) * 100) / tot);
                barDisk.Value = Math.Max(0, Math.Min(100, pct));
                valDisk.Text = Fmt.Bytes(free) + " livres de " + Fmt.Bytes(tot) + " (" + pct + " % usado)";
                valDisk.ForeColor = pct >= 85 ? Theme.Warn : Theme.Text;
            }
            catch { }

            try
            {
                TimeSpan up = TimeSpan.FromMilliseconds((double)Native.GetTickCount64());
                lblUp.Text = up.Days + " d " + up.Hours + " h " + up.Minutes + " min";
            }
            catch { }

            if (tick % 3 == 1) PollGpu();
        }

        // Uso total da CPU (%), calculado pela diferenca entre duas leituras
        private int CpuPercent()
        {
            System.Runtime.InteropServices.ComTypes.FILETIME ftIdle, ftKernel, ftUser;
            if (!Native.GetSystemTimes(out ftIdle, out ftKernel, out ftUser)) return -1;

            ulong idle = Native.ToUlong(ftIdle);
            ulong total = Native.ToUlong(ftKernel) + Native.ToUlong(ftUser);
            int result = 0;

            if (hasPrev)
            {
                ulong dIdle = idle - prevIdle;
                ulong dTotal = total - prevTotal;
                if (dTotal > 0)
                {
                    long pctIdle = (long)(dIdle * 100 / dTotal);
                    result = (int)(100 - pctIdle);
                    if (result < 0) result = 0;
                    if (result > 100) result = 100;
                }
            }

            prevIdle = idle;
            prevTotal = total;
            hasPrev = true;
            return result;
        }

        private void PollGpu()
        {
            if (gpuBusy) return;
            gpuBusy = true;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                string[] parts = null;
                try
                {
                    StringBuilder sb = new StringBuilder();
                    Proc.Run("nvidia-smi.exe",
                        "--query-gpu=name,temperature.gpu,utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits",
                        delegate(string line) { sb.Append(line); });
                    string text = sb.ToString().Trim();
                    if (text.Length > 0) parts = text.Split(',');
                }
                catch { }

                try
                {
                    BeginInvoke(new Action(delegate { ApplyGpu(parts); }));
                }
                catch { }
                gpuBusy = false;
            });
        }

        private void ApplyGpu(string[] parts)
        {
            if (parts == null || parts.Length < 5)
            {
                lblGpu.Text = "Não consegui ler a placa de vídeo (nvidia-smi não encontrado)";
                valGpuTemp.Text = "-";
                valGpuUse.Text = "-";
                valVram.Text = "-";
                return;
            }

            lblGpu.Text = parts[0].Trim();

            int temp = 0, use = 0, memUsed = 0, memTotal = 0;
            int.TryParse(parts[1].Trim(), out temp);
            int.TryParse(parts[2].Trim(), out use);
            int.TryParse(parts[3].Trim(), out memUsed);
            int.TryParse(parts[4].Trim(), out memTotal);

            barGpuTemp.Value = Math.Max(0, Math.Min(100, temp));
            valGpuTemp.Text = temp + " °C";
            valGpuTemp.ForeColor = temp >= 80 ? Theme.Warn : Theme.Text;

            barGpuUse.Value = Math.Max(0, Math.Min(100, use));
            valGpuUse.Text = use + " %";

            int vpct = 0;
            if (memTotal > 0) vpct = (memUsed * 100) / memTotal;
            barVram.Value = Math.Max(0, Math.Min(100, vpct));
            valVram.Text = memUsed + " MB de " + memTotal + " MB (" + vpct + " %)";
        }
    }
}

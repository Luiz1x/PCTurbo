using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTurbo
{
    public class MainForm : Form
    {
        private TabControl tabs;
        private TextBox txtLog;
        private DashTab dash;

        public MainForm()
        {
            this.Text = "PC Turbo 1.0";
            this.ClientSize = new Size(1100, 780);
            this.MinimumSize = new Size(960, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            try { this.Icon = MakeIcon(); } catch { }

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));

            // Cabecalho
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.FromArgb(24, 24, 28);

            Label title = new Label();
            title.Text = "PC Turbo";
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(16, 6);

            Label sub = new Label();
            sub.Text = "Limpeza, otimização e manutenção do Windows 11";
            sub.ForeColor = Color.Gainsboro;
            sub.Font = new Font("Segoe UI", 9f);
            sub.AutoSize = true;
            sub.Location = new Point(19, 42);

            Button quick = Ui.BigButton("OTIMIZAR TUDO (1 clique)", 330, 52, delegate { QuickOptimize(); });
            quick.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            quick.Location = new Point(1100 - 330 - 16, 11);

            header.Controls.Add(title);
            header.Controls.Add(sub);
            header.Controls.Add(quick);
            header.Resize += delegate { quick.Left = header.ClientSize.Width - quick.Width - 16; };

            // Abas
            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;

            dash = new DashTab();
            dash.QuickAction = QuickOptimize;

            AddTab("Painel", dash);
            AddTab("Limpeza", new CleanTab());
            AddTab("Otimização", new TweaksTab());
            AddTab("NVIDIA 3D", new NvidiaTab());
            AddTab("Programas", new ProgramsTab());
            AddTab("Inicialização", new StartupTab());
            AddTab("Manutenção", new MaintTab());

            // Log
            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = new Font("Consolas", 9f);
            txtLog.BackColor = Color.FromArgb(18, 18, 18);
            txtLog.ForeColor = Color.FromArgb(120, 230, 140);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(tabs, 0, 1);
            root.Controls.Add(txtLog, 0, 2);
            this.Controls.Add(root);

            Ui.BusyHost = tabs;
            Log.Sink = delegate(string line)
            {
                if (txtLog.IsDisposed) return;
                if (txtLog.InvokeRequired)
                {
                    try
                    {
                        txtLog.BeginInvoke(new Action(delegate { txtLog.AppendText(line + "\r\n"); }));
                    }
                    catch { }
                }
                else
                {
                    txtLog.AppendText(line + "\r\n");
                }
            };

            Log.Write("PC Turbo iniciado. Tudo o que o programa faz aparece aqui e também no arquivo PCTurbo_log.txt na Área de Trabalho.");
        }

        private void AddTab(string title, Control content)
        {
            TabPage page = new TabPage(title);
            page.UseVisualStyleBackColor = true;
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            tabs.TabPages.Add(page);
        }

        private void QuickOptimize()
        {
            if (Ui.Busy)
            {
                MessageBox.Show("Já tem uma tarefa rodando. Espere terminar.", "PC Turbo");
                return;
            }
            DialogResult r = MessageBox.Show(
                "Vou criar um ponto de restauração, limpar os arquivos inúteis e aplicar os ajustes recomendados (os que vêm marcados nas abas Limpeza e Otimização), as configs de FPS da NVIDIA e um diagnóstico de tela/RAM/Resizable BAR.\n\nContinuar?",
                "Limpar e otimizar tudo", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            Ui.Background("Limpeza e otimização completas", delegate
            {
                Maint.RestorePoint("PC Turbo - antes da otimizacao");

                long total = 0;
                foreach (CleanTarget t in Cleaner.Targets())
                {
                    if (!t.Default) continue;
                    long freed = 0;
                    try { freed = t.Clean(); }
                    catch (Exception ex) { Log.Write("Falhou: " + t.Name + " - " + ex.Message); }
                    total += freed;
                    Log.Write("Limpo: " + t.Name + " (" + Fmt.Bytes(freed) + ")");
                }
                Log.Write("Espaço liberado: " + Fmt.Bytes(total));

                int okCount = 0, failCount = 0;
                foreach (Tweak tw in Tweaks.All())
                {
                    if (!tw.Default) continue;
                    try
                    {
                        tw.Apply();
                        okCount++;
                        Log.Write("Aplicado: " + tw.Name);
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        Log.Write("Falhou: " + tw.Name + " - " + ex.Message);
                    }
                }

                string nv = NvidiaProfile.ApplyBest();
                if (nv == null) Log.Write("Aplicado: configs de FPS da NVIDIA (perfil global)");
                else Log.Write("NVIDIA ignorado: " + nv);

                DiagResult dg = Diag.Run();
                Log.Write("Taxa da tela: " + dg.Refresh);
                Log.Write("Memória RAM: " + dg.Ram);
                Log.Write("Resizable BAR: " + dg.Bar);

                string report = "Pronto!\n\nEspaço liberado: " + Fmt.Bytes(total) +
                    "\nAjustes aplicados: " + okCount + (failCount > 0 ? " (" + failCount + " falharam, veja o quadro de baixo)" : "") +
                    "\nNVIDIA: " + (nv == null ? "configs de FPS aplicadas" : "não aplicado") +
                    "\n\nTela: " + dg.Refresh + "\nRAM: " + dg.Ram + "\nResizable BAR: " + dg.Bar +
                    "\n\nPara tudo valer, o PC precisa reiniciar. Reiniciar agora?";
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        DialogResult rr = MessageBox.Show(report, "PC Turbo", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (rr == DialogResult.Yes)
                        {
                            try { Proc.Run("shutdown.exe", "/r /t 15 /c \"PC Turbo: reiniciando para aplicar as mudanças\"", null); }
                            catch { }
                            Log.Write("Reiniciando em 15 segundos. Para cancelar: CMD > shutdown /a");
                        }
                    }));
                }
                catch { }
            });
        }

        private static System.Drawing.Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush b = new SolidBrush(Color.FromArgb(46, 204, 113)))
                {
                    g.FillEllipse(b, 1, 1, 30, 30);
                }
                Point[] bolt =
                {
                    new Point(18, 4), new Point(8, 18), new Point(15, 18),
                    new Point(13, 28), new Point(24, 13), new Point(17, 13)
                };
                g.FillPolygon(Brushes.White, bolt);
                IntPtr h = bmp.GetHicon();
                return System.Drawing.Icon.FromHandle(h);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Log.Write("Erro inesperado: " + e.Exception.Message);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                MessageBox.Show("Erro: " + (ex != null ? ex.Message : "desconhecido"), "PC Turbo");
            };

            Application.Run(new MainForm());
        }
    }
}

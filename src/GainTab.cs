using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace PCTurbo
{
    // Aba "Meu ganho": mostra o antes e o depois do PC dentro do proprio app
    public class GainTab : UserControl
    {
        private readonly string[] names = { "Programas rodando", "Threads", "RAM em uso", "CPU média (parado)", "Livre no C:" };
        private Label[] before = new Label[5];
        private Label[] now = new Label[5];
        private Label[] change = new Label[5];
        private Label lblStatus;
        private Label lblClean;
        private Label lblRuns;
        private Label lblWhen;
        private Label lblFound;
        private Label lblRemoved;
        private TextBox txtCleaned;
        private TextBox txtFixes;
        private TextBox txtWarns;
        private TextBox txtHist;
        private Button btnRefresh;
        private bool loadedOnce;
        private ComboBox cmbProc;
        private NumericUpDown nudSecs;
        private Button btnFpsA;
        private Button btnFpsD;
        private Label lblFps;
        private bool fpsBusy;
        private bool fpsAsked;
        private bool busy;

        public GainTab()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(16);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;

            flow.Controls.Add(Section("Resumo da última otimização"));
            lblWhen = new Label();
            lblWhen.Width = 900;
            lblWhen.Height = 24;
            lblWhen.ForeColor = Theme.Muted;
            lblWhen.Text = "...";
            flow.Controls.Add(lblWhen);

            lblFound = Big("Lixo que o PC tinha: ...");
            flow.Controls.Add(lblFound);
            lblRemoved = Big("Lixo removido: ...");
            flow.Controls.Add(lblRemoved);

            flow.Controls.Add(Section("O que foi limpo"));
            txtCleaned = Box(112);
            flow.Controls.Add(txtCleaned);

            flow.Controls.Add(Section("Ajustes aplicados no PC"));
            txtFixes = Box(112);
            flow.Controls.Add(txtFixes);

            flow.Controls.Add(Section("O que precisa de atenção (o app não resolve por software)"));
            txtWarns = Box(72);
            flow.Controls.Add(txtWarns);

            flow.Controls.Add(Section("Leveza do PC: antes e agora"));
            lblStatus = new Label();
            lblStatus.Width = 900;
            lblStatus.Height = 52;
            lblStatus.Text = "Carregando...";
            flow.Controls.Add(lblStatus);

            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 4;
            t.RowCount = 6;
            t.Width = 900;
            t.Height = 6 * 32;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            t.Margin = new Padding(0, 4, 0, 4);

            AddCell(t, "O que mede", 0, 0, true, Theme.AccentText);
            AddCell(t, "Antes", 1, 0, true, Theme.AccentText);
            AddCell(t, "Agora", 2, 0, true, Theme.AccentText);
            AddCell(t, "Diferença", 3, 0, true, Theme.AccentText);
            for (int i = 0; i < names.Length; i++)
            {
                AddCell(t, names[i], 0, i + 1, true, Theme.Text);
                before[i] = AddCell(t, "-", 1, i + 1, false, Theme.Text);
                now[i] = AddCell(t, "-", 2, i + 1, false, Theme.Text);
                change[i] = AddCell(t, "-", 3, i + 1, false, Theme.Text);
            }
            flow.Controls.Add(t);

            flow.Controls.Add(Section("Soma de tudo desde que instalou"));
            lblClean = new Label();
            lblClean.Width = 900;
            lblClean.Height = 26;
            lblClean.Text = "-";
            flow.Controls.Add(lblClean);
            lblRuns = new Label();
            lblRuns.Width = 900;
            lblRuns.Height = 26;
            lblRuns.Text = "-";
            flow.Controls.Add(lblRuns);

            flow.Controls.Add(Section("Histórico das otimizações"));
            txtHist = Box(100);
            flow.Controls.Add(txtHist);

            Label note = new Label();
            note.Width = 900;
            note.Height = 96;
            note.ForeColor = Theme.Muted;
            note.Text = "Como ler: o \"antes\" é a foto do PC no seu primeiro OTIMIZAR TUDO. O \"agora\" é medido na hora. " +
                "Os números mudam conforme o que está aberto, então o melhor é conferir logo depois de reiniciar o PC, sem abrir nada. " +
                "Menos programas, menos RAM e menos CPU parada significam um PC mais leve em segundo plano. Isso não é FPS: para provar o ganho em jogo, " +
                "anote o FPS do mesmo jogo, no mesmo lugar, antes e depois.";
            flow.Controls.Add(note);

            flow.Controls.Add(Section("Teste de FPS (opcional, para provar o ganho no jogo)"));
            Label how = new Label();
            how.Width = 900;
            how.Height = 70;
            how.ForeColor = Theme.Muted;
            how.Text = "1) ANTES de otimizar: abra o jogo, vá para um lugar sempre igual (por exemplo, o campo de treino), clique em \"Medir ANTES\" e volte para o jogo. " +
                "2) Otimize e reinicie o PC. 3) Repita no mesmo lugar e clique em \"Medir DEPOIS\". O resultado entra no relatório.";
            flow.Controls.Add(how);

            FlowLayoutPanel fpsRow = new FlowLayoutPanel();
            fpsRow.FlowDirection = FlowDirection.LeftToRight;
            fpsRow.WrapContents = false;
            fpsRow.Width = 900;
            fpsRow.Height = 40;
            fpsRow.Margin = new Padding(0);
            Label lp = new Label();
            lp.Text = "Programa do jogo:";
            lp.Width = 120;
            lp.Height = 28;
            lp.TextAlign = ContentAlignment.MiddleLeft;
            cmbProc = new ComboBox();
            cmbProc.Width = 290;
            cmbProc.Text = "VALORANT-Win64-Shipping.exe";
            cmbProc.DropDown += delegate { FillProcs(); };
            Label ls = new Label();
            ls.Text = "Duração (s):";
            ls.Width = 90;
            ls.Height = 28;
            ls.Margin = new Padding(16, 3, 0, 0);
            ls.TextAlign = ContentAlignment.MiddleLeft;
            nudSecs = new NumericUpDown();
            nudSecs.Minimum = 30;
            nudSecs.Maximum = 180;
            nudSecs.Value = 60;
            nudSecs.Width = 60;
            fpsRow.Controls.Add(lp);
            fpsRow.Controls.Add(cmbProc);
            fpsRow.Controls.Add(ls);
            fpsRow.Controls.Add(nudSecs);
            flow.Controls.Add(fpsRow);

            btnFpsA = Ui.Btn("Medir FPS: ANTES", 190, delegate { StartFps("antes"); });
            btnFpsD = Ui.Btn("Medir FPS: DEPOIS", 190, delegate { StartFps("depois"); });
            FlowLayoutPanel fpsButtons = Ui.Buttons(btnFpsA, btnFpsD);
            fpsButtons.WrapContents = false;
            fpsButtons.AutoSize = true;
            flow.Controls.Add(fpsButtons);

            lblFps = new Label();
            lblFps.Width = 900;
            lblFps.Height = 96;
            lblFps.Text = "...";
            flow.Controls.Add(lblFps);

            btnRefresh = Ui.Btn("Atualizar agora", 170, delegate { Refresh2(); });
            Button btnFile = Ui.Btn("Salvar relatório em arquivo", 230, delegate
            {
                if (busy) return;
                busy = true;
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    Report.Generate();
                    busy = false;
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            MessageBox.Show("Salvei o arquivo PCTurbo_relatorio.txt na Área de Trabalho. É ele que você pode mandar para quem pediu.", "PC Turbo");
                            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + Report.ReportFile() + "\""); }
                            catch { }
                        }));
                    }
                    catch { }
                });
            });
            Button btnCard = Ui.Btn("Gerar cartão de resultado (imagem)", 280, delegate
            {
                if (busy) return;
                busy = true;
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    string path = null;
                    string error = null;
                    try { path = Card.Make(); }
                    catch (Exception ex) { error = ex.Message; }
                    busy = false;
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            if (error != null)
                            {
                                MessageBox.Show("Não consegui gerar a imagem: " + error, "PC Turbo");
                                return;
                            }
                            MessageBox.Show("Salvei a imagem PCTurbo_resultado.png na Área de Trabalho. É só mandar para os seus amigos.", "PC Turbo");
                            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); }
                            catch { }
                        }));
                    }
                    catch { }
                });
            });
            FlowLayoutPanel buttons = Ui.Buttons(btnRefresh, btnFile, btnCard);
            buttons.WrapContents = false;
            buttons.AutoSize = true;
            flow.Controls.Add(buttons);

            this.Controls.Add(flow);

            // Carrega na primeira vez que a aba aparece
            this.VisibleChanged += delegate
            {
                if (this.Visible && !loadedOnce)
                {
                    loadedOnce = true;
                    Refresh2();
                }
            };
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

        private Label Big(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.Width = 900;
            l.Height = 34;
            l.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            l.ForeColor = Theme.Text;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private TextBox Box(int height)
        {
            TextBox t = new TextBox();
            t.Multiline = true;
            t.ReadOnly = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.Width = 880;
            t.Height = height;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = new Font("Segoe UI", 9.5f);
            t.Text = "...";
            return t;
        }

        private static string Bullets(System.Collections.Generic.List<string> items, string empty)
        {
            if (items == null || items.Count == 0) return empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string i in items) sb.Append("• " + i + "\r\n");
            return sb.ToString();
        }

        private Label AddCell(TableLayoutPanel t, string text, int col, int row, bool bold, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = color;
            l.Font = new Font("Segoe UI", 9.5f, bold ? FontStyle.Bold : FontStyle.Regular);
            t.Controls.Add(l, col, row);
            return l;
        }

        private void FillProcs()
        {
            try
            {
                string cur = cmbProc.Text;
                cmbProc.Items.Clear();
                System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
                foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && p.ProcessName.Length > 0)
                        {
                            string n = p.ProcessName + ".exe";
                            if (!names.Contains(n)) names.Add(n);
                        }
                    }
                    catch { }
                    try { p.Dispose(); }
                    catch { }
                }
                names.Sort();
                foreach (string n in names) cmbProc.Items.Add(n);
                cmbProc.Text = cur;
            }
            catch { }
        }

        private void PostFps(string text)
        {
            try { BeginInvoke(new Action(delegate { lblFps.Text = text; })); }
            catch { }
        }

        private void FinishFps(string text)
        {
            fpsBusy = false;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    btnFpsA.Enabled = true;
                    btnFpsD.Enabled = true;
                    lblFps.Text = text;
                    try { System.Media.SystemSounds.Asterisk.Play(); }
                    catch { }
                }));
            }
            catch { }
        }

        private string StoredFpsText()
        {
            FpsResult a = Report.LoadFps("antes");
            FpsResult d = Report.LoadFps("depois");
            if (a == null && d == null) return "Ainda não há medição de FPS.";
            string t = "";
            if (a != null) t += "ANTES : " + Report.FpsLine(a) + "\r\n";
            if (d != null) t += "DEPOIS: " + Report.FpsLine(d) + "\r\n";
            if (a != null && d != null && string.Equals(a.Proc, d.Proc, StringComparison.OrdinalIgnoreCase) && a.Avg > 0)
            {
                int bt;
                t += "Diferença no FPS médio: " + Report.Diff(a.Avg, d.Avg, " FPS", false, out bt) + "   |   no 1% low: " + Report.Diff(a.Low1, d.Low1, " FPS", false, out bt);
            }
            return t;
        }

        private void StartFps(string which)
        {
            if (fpsBusy || Ui.Busy)
            {
                MessageBox.Show("Já tem uma tarefa rodando. Espere terminar.", "PC Turbo");
                return;
            }
            string proc = cmbProc.Text.Trim();
            if (proc.Length == 0)
            {
                MessageBox.Show("Digite o nome do programa do jogo (exemplo: VALORANT-Win64-Shipping.exe) ou escolha na lista com o jogo aberto.", "PC Turbo");
                return;
            }
            if (!proc.ToLowerInvariant().EndsWith(".exe")) proc += ".exe";
            int secs = (int)nudSecs.Value;

            if (!fpsAsked)
            {
                DialogResult r = MessageBox.Show(
                    "O teste usa o PresentMon, uma ferramenta gratuita e de código aberto da Intel. Ele só lê, pelo próprio Windows, quando cada quadro aparece na tela. " +
                    "Não injeta nada no jogo e não altera arquivos dele.\n\n" +
                    "A Riot não publica uma lista oficial de programas aceitos pelo Vanguard. Esse tipo de ferramenta é muito usado em Valorant, mas a decisão é sua. " +
                    "Se preferir, meça em um jogo sem anti-cheat.\n\n" +
                    "Na primeira vez o app baixa o PresentMon do GitHub oficial (GameTechDev/PresentMon).\n\nContinuar?",
                    "PC Turbo - teste de FPS", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
                fpsAsked = true;
            }

            fpsBusy = true;
            btnFpsA.Enabled = false;
            btnFpsD.Enabled = false;
            string label = which == "antes" ? "ANTES" : "DEPOIS";
            lblFps.Text = "Preparando...";
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    PostFps("Preparando o PresentMon...");
                    string e1 = FpsTest.EnsureTool();
                    if (e1 != null) { FinishFps(e1); return; }

                    for (int i = 15; i > 0; i--)
                    {
                        PostFps("Medição " + label + ": volte para o jogo, vá para o lugar do teste e jogue normalmente. Começa em " + i + " s (dura " + secs + " s).");
                        Thread.Sleep(1000);
                    }
                    PostFps("Medindo " + proc + " por " + secs + " s. Continue jogando...");
                    FpsResult res;
                    string err = FpsTest.Measure(proc, secs, out res);
                    if (err != null) { FinishFps("Não deu certo: " + err); return; }

                    Report.SaveFps(which, res);
                    Log.Write("Teste de FPS (" + which + "): " + Report.FpsLine(res));
                    FinishFps("Pronto! Medição " + label + " guardada.\r\n" + StoredFpsText());
                }
                catch (Exception ex)
                {
                    FinishFps("Erro no teste de FPS: " + ex.Message);
                }
            });
        }

        private void Refresh2()
        {
            if (busy) return;
            busy = true;
            btnRefresh.Enabled = false;
            lblStatus.Text = "Medindo o PC agora (leva uns 3 segundos)...";
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                Snap n = null;
                Snap b = null;
                long cleaned = 0;
                int cleanings = 0, runs = 0;
                string last = "";
                long found = 0;
                string when = "";
                System.Collections.Generic.List<string> lc = null, lf = null, lw = null;
                bool hasRun = false;
                try
                {
                    hasRun = Report.LoadRun(out found, out when, out lc, out lf, out lw);
                    n = Report.Take();
                    b = Report.LoadBefore();
                    Report.Totals(out cleaned, out cleanings, out runs, out last);
                }
                catch { }
                try
                {
                    BeginInvoke(new Action(delegate { Show2(n, b, cleaned, cleanings, runs, last, hasRun, found, when, lc, lf, lw); }));
                }
                catch { }
                busy = false;
            });
        }

        private static string Gb(long mb)
        {
            return (mb / 1024.0).ToString("0.0") + " GB";
        }

        private void ShowHistory()
        {
            System.Collections.Generic.List<Report.RunRec> h = Report.History();
            if (h.Count == 0)
            {
                txtHist.Text = "Nenhuma otimização registrada ainda.";
                return;
            }
            long max = 1;
            foreach (Report.RunRec x in h)
            {
                if (x.Removed > max) max = x.Removed;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = h.Count - 1; i >= 0; i--)
            {
                Report.RunRec r = h[i];
                int n = (int)(r.Removed * 16 / max);
                if (r.Removed > 0 && n < 1) n = 1;
                sb.Append("• " + r.When + "  " + new string('█', n).PadRight(16, '·') + "  tinha " + Fmt.Bytes(r.Found) + " de lixo, removeu " + Fmt.Bytes(r.Removed) + ", " + r.Fixes + " ajustes, " +
                    (r.Warns == 0 ? "sem avisos" : r.Warns + " aviso(s)") + "\r\n");
            }
            txtHist.Text = sb.ToString();
        }

        private void Set(int i, string b, string n, string c, int better)
        {
            before[i].Text = b;
            now[i].Text = n;
            change[i].Text = c;
            if (better > 0) change[i].ForeColor = Theme.AccentText;
            else if (better < 0) change[i].ForeColor = Theme.Warn;
            else change[i].ForeColor = Theme.Text;
        }

        private void Show2(Snap n, Snap b, long cleaned, int cleanings, int runs, string last,
            bool hasRun, long found, string when, System.Collections.Generic.List<string> lc, System.Collections.Generic.List<string> lf, System.Collections.Generic.List<string> lw)
        {
            btnRefresh.Enabled = true;
            if (!fpsBusy) lblFps.Text = StoredFpsText();
            ShowHistory();
            if (hasRun)
            {
                lblWhen.Text = "Feita em " + when;
                lblFound.Text = "Lixo que o PC tinha: " + Fmt.Bytes(found);
                lblRemoved.Text = "Lixo removido: " + Fmt.Bytes(cleaned) + " (soma de tudo)";
                txtCleaned.Text = Bullets(lc, "Nada para limpar.");
                txtFixes.Text = Bullets(lf, "Nenhum ajuste aplicado.");
                txtWarns.Text = Bullets(lw, "Nada fora do ideal no diagnóstico.");
            }
            else
            {
                lblWhen.Text = "Você ainda não clicou em OTIMIZAR TUDO.";
                lblFound.Text = "Lixo que o PC tinha: ainda não medido";
                lblRemoved.Text = "Lixo removido: " + Fmt.Bytes(cleaned);
                txtCleaned.Text = "Clique em OTIMIZAR TUDO e o resumo aparece aqui.";
                txtFixes.Text = "Clique em OTIMIZAR TUDO e o resumo aparece aqui.";
                txtWarns.Text = "Clique em OTIMIZAR TUDO e o resumo aparece aqui.";
            }
            if (n == null)
            {
                lblStatus.Text = "Não consegui medir o PC agora. Tente de novo.";
                return;
            }

            int bt;
            if (b == null)
            {
                lblStatus.Text = "Ainda não existe um \"antes\". Ele é guardado quando você clica em OTIMIZAR TUDO pela primeira vez. Depois reinicie o PC e volte aqui. Por enquanto, estes são os números de agora:";
                Set(0, "-", n.Procs.ToString(), "-", 0);
                Set(1, "-", n.Threads.ToString(), "-", 0);
                Set(2, "-", n.RamUsedMB + " MB", "-", 0);
                Set(3, "-", n.CpuAvg >= 0 ? n.CpuAvg.ToString("0.0") + "%" : "?", "-", 0);
                Set(4, "-", Gb(n.DiskFreeMB), "-", 0);
            }
            else
            {
                lblStatus.Text = "Comparando a foto de " + b.When + " (antes) com agora (" + n.When + "). Verde é melhor, laranja é pior.";
                Set(0, b.Procs.ToString(), n.Procs.ToString(), Report.Diff(b.Procs, n.Procs, "", true, out bt), bt);
                Set(1, b.Threads.ToString(), n.Threads.ToString(), Report.Diff(b.Threads, n.Threads, "", true, out bt), bt);
                Set(2, b.RamUsedMB + " MB", n.RamUsedMB + " MB", Report.Diff(b.RamUsedMB, n.RamUsedMB, " MB", true, out bt), bt);
                if (b.CpuAvg >= 0 && n.CpuAvg >= 0)
                    Set(3, b.CpuAvg.ToString("0.0") + "%", n.CpuAvg.ToString("0.0") + "%", Report.Diff(b.CpuAvg, n.CpuAvg, "%", true, out bt), bt);
                else
                    Set(3, "?", "?", "-", 0);
                Set(4, Gb(b.DiskFreeMB), Gb(n.DiskFreeMB), Report.Diff(Math.Round(b.DiskFreeMB / 1024.0, 1), Math.Round(n.DiskFreeMB / 1024.0, 1), " GB", false, out bt), bt);
            }

            lblClean.Text = "Lixo removido: " + Fmt.Bytes(cleaned) + " em " + cleanings + " limpezas (manual, semanal e em segundo plano).";
            lblRuns.Text = "OTIMIZAR TUDO usado: " + runs + " vez(es)" + (last.Length > 0 ? ". Última: " + last : ".");
        }
    }
}

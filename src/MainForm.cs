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
        private Button btnUpd;
        private bool goingToTray;

        public MainForm()
        {
            this.Text = "PC Turbo " + Updater.Current;
            this.ClientSize = new Size(1100, 780);
            this.MinimumSize = new Size(960, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Theme.Bg;
            this.ForeColor = Theme.Text;
            this.Font = new Font("Segoe UI", 9.5f);
            try { this.Icon = MakeIcon(); } catch { }

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));

            // Cabecalho
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.FromArgb(17, 20, 26);
            header.Tag = "keep";

            Panel accentLine = new Panel();
            accentLine.Dock = DockStyle.Bottom;
            accentLine.Height = 2;
            accentLine.BackColor = Theme.Accent;
            accentLine.Tag = "keep";

            Label title = new Label();
            title.Text = "PC Turbo";
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(16, 6);

            Label sub = new Label();
            sub.Text = "Limpeza, otimização e manutenção do Windows 11  -  versão " + Updater.Current;
            sub.ForeColor = Color.Gainsboro;
            sub.Font = new Font("Segoe UI", 9f);
            sub.AutoSize = true;
            sub.Location = new Point(19, 42);

            Button quick = Ui.BigButton("OTIMIZAR TUDO (1 clique)", 330, 52, delegate { QuickOptimize(); });
            quick.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            quick.Location = new Point(1100 - 330 - 16, 11);

            btnUpd = new Button();
            btnUpd.Text = "Atualizar";
            btnUpd.Width = 120;
            btnUpd.Click += delegate { DoUpdate(); };
            btnUpd.MouseEnter += delegate { btnUpd.BackColor = Color.FromArgb(48, 56, 72); };
            btnUpd.MouseLeave += delegate { btnUpd.BackColor = Color.FromArgb(34, 40, 52); };
            btnUpd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUpd.Height = 52;
            btnUpd.FlatStyle = FlatStyle.Flat;
            btnUpd.FlatAppearance.BorderSize = 1;
            btnUpd.FlatAppearance.BorderColor = Color.FromArgb(70, 78, 92);
            btnUpd.BackColor = Color.FromArgb(34, 40, 52);
            btnUpd.ForeColor = Color.White;
            btnUpd.UseVisualStyleBackColor = false;
            btnUpd.Cursor = Cursors.Hand;
            btnUpd.Location = new Point(1100 - 330 - 16 - 120 - 12, 11);

            header.Controls.Add(accentLine);
            header.Controls.Add(btnUpd);
            header.Controls.Add(title);
            header.Controls.Add(sub);
            header.Controls.Add(quick);
            header.Resize += delegate { quick.Left = header.ClientSize.Width - quick.Width - 16; btnUpd.Left = quick.Left - btnUpd.Width - 12; };

            // Abas
            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(114, 38);
            tabs.Padding = new Point(14, 6);
            tabs.DrawItem += DrawTab;

            dash = new DashTab();
            dash.QuickAction = QuickOptimize;
            dash.UndoAction = UndoAll;
            dash.ReportAction = MakeReport;
            dash.PreviewAction = Preview;

            AddTab("Painel", dash);
            AddTab("Meu ganho", new GainTab());
            AddTab("Limpeza", new CleanTab());
            AddTab("Otimização", new TweaksTab());
            AddTab("NVIDIA 3D", new NvidiaTab());
            AddTab("Inicialização", new StartupTab());
            AddTab("Jogos", new GamesTab());
            AddTab("Prioridades", new PriorityTab());
            AddTab("Manutenção", new MaintTab());

            // Log
            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = new Font("Consolas", 9f);
            txtLog.BackColor = Color.FromArgb(10, 12, 16);
            txtLog.Tag = "keep";
            txtLog.ForeColor = Color.FromArgb(120, 230, 140);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(tabs, 0, 1);
            root.Controls.Add(txtLog, 0, 2);
            this.Controls.Add(root);

            // Minimizar manda o PC Turbo para a bandeja (se nao tiver tarefa rodando)
            this.Resize += delegate
            {
                if (this.WindowState != FormWindowState.Minimized || goingToTray || Ui.Busy) return;
                goingToTray = true;
                try { System.Diagnostics.Process.Start(Application.ExecutablePath, "/fundo"); }
                catch (Exception ex)
                {
                    goingToTray = false;
                    Log.Write("Não consegui ir para a bandeja: " + ex.Message);
                    return;
                }
                this.BeginInvoke(new Action(delegate { Application.Exit(); }));
            };

            Theme.Apply(this);
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

            this.Shown += delegate
            {
                if (!Report.GetFlag("bem_vindo"))
                {
                    Report.SetFlag("bem_vindo");
                    MessageBox.Show(
                        "Bem-vindo ao PC Turbo!\n\n" +
                        "É só clicar em OTIMIZAR TUDO (botão verde). Ele cria um ponto de restauração, limpa o lixo, aplica ajustes seguros e depois mostra o resultado na aba Meu ganho.\n\n" +
                        "O que ele NUNCA mexe: Riot e Vanguard, Xbox e Game Pass, seus jogos, launchers (Epic, Steam e outros), seus arquivos e seus programas. Depois de otimizar, ele só confere se tudo continua igual.\n\n" +
                        "Quer ver antes o que vai ser feito? No Painel, clique em \"Ver o que vai mudar\". E dá para desfazer os ajustes a qualquer hora.",
                        "PC Turbo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                System.Threading.ThreadPool.QueueUserWorkItem(delegate(object o)
                {
                    string latest = Updater.LatestVersion();
                    if (!Updater.IsNewer(latest)) return;
                    try
                    {
                        this.BeginInvoke(new Action(delegate
                        {
                            btnUpd.Text = "Atualizar (v" + latest + ")";
                            btnUpd.Width = 190;
                            btnUpd.Left = btnUpd.Parent.ClientSize.Width - 330 - 16 - btnUpd.Width - 12;
                            Log.Write("Tem versão nova do PC Turbo (" + latest + ").");
                            DialogResult dr = MessageBox.Show(
                                "Tem uma versão nova do PC Turbo (" + latest + "). A sua é a " + Updater.Current + ".\n\nQuer atualizar agora? Leva alguns segundos e o programa abre sozinho de novo.",
                                "PC Turbo - atualização", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                            if (dr == DialogResult.Yes) DoUpdate();
                        }));
                    }
                    catch { }
                });
            };

            Log.Write("PC Turbo iniciado. Tudo o que o programa faz aparece aqui e também no arquivo PCTurbo_log.txt na Área de Trabalho.");
        }

        private Font tabFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        private Font tabFontBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        private void DrawTab(object sender, DrawItemEventArgs e)
        {
            bool sel = (e.Index == tabs.SelectedIndex);
            Rectangle r = tabs.GetTabRect(e.Index);
            Color back = sel ? Theme.Card : Theme.Strip;
            using (SolidBrush bg = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(bg, r);
            }
            if (e.Index == tabs.TabCount - 1)
            {
                using (SolidBrush strip = new SolidBrush(Theme.Strip))
                {
                    e.Graphics.FillRectangle(strip, r.Right, 0, tabs.Width - r.Right, r.Bottom + 2);
                }
            }
            if (sel)
            {
                using (SolidBrush acc = new SolidBrush(Theme.Accent))
                {
                    e.Graphics.FillRectangle(acc, r.X, r.Bottom - 3, r.Width, 3);
                }
            }
            Color fore = sel ? Color.White : Theme.Muted;
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, sel ? tabFontBold : tabFont, r, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void AddTab(string title, Control content)
        {
            TabPage page = new TabPage(title);
            page.UseVisualStyleBackColor = true;
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            tabs.TabPages.Add(page);
        }

        private void Preview()
        {
            if (Ui.Busy)
            {
                MessageBox.Show("Já tem uma tarefa rodando. Espere terminar.", "PC Turbo");
                return;
            }
            Ui.Background("Montando a prévia (nada é aplicado)", delegate
            {
                string text = PreviewText();
                try { BeginInvoke(new Action(delegate { PreviewForm.ShowText(this, text); })); }
                catch { }
            });
        }

        private static string PreviewText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool laptop = Machine.IsLaptop();
            sb.AppendLine("O QUE O OTIMIZAR TUDO VAI FAZER");
            sb.AppendLine("(isto é só uma prévia: nada foi aplicado nem apagado)");
            sb.AppendLine();
            sb.AppendLine("1. Criar um ponto de restauração do Windows");
            sb.AppendLine();
            sb.AppendLine("2. Limpar este lixo:");
            long total = 0;
            foreach (CleanTarget t in Cleaner.Targets())
            {
                if (!t.Default) continue;
                long sz = 0;
                try { sz = t.Size(); }
                catch { }
                total += sz;
                sb.AppendLine("   • " + t.Name + " - " + Fmt.Bytes(sz));
            }
            sb.AppendLine("   Total: " + Fmt.Bytes(total));
            sb.AppendLine();
            sb.AppendLine("3. Aplicar estes ajustes:");
            foreach (Tweak tw in Tweaks.All())
            {
                if (!tw.Default) continue;
                sb.AppendLine("   • " + tw.Name + (laptop && tw.DesktopOnly ? "   (pulado: notebook)" : ""));
            }
            sb.AppendLine();
            sb.AppendLine("4. TRIM do SSD, limpar o cache de DNS, ligar a limpeza semanal e a limpeza em segundo plano (bandeja)");
            sb.AppendLine("5. Configs de FPS da placa NVIDIA (se tiver NVIDIA)");
            sb.AppendLine("6. Diagnóstico do PC e conferência dos programas abaixo (só leitura)");
            sb.AppendLine();
            sb.AppendLine("Programas que serão conferidos depois (nada é alterado neles):");
            System.Collections.Generic.List<GuardItem> g = Guard.Snapshot();
            if (g.Count == 0) sb.AppendLine("   • nenhum encontrado");
            foreach (GuardItem gi in g) sb.AppendLine("   • " + gi.Name + (gi.Version.Length > 0 ? " (versão " + gi.Version + ")" : ""));
            sb.AppendLine();
            sb.AppendLine("O que NUNCA é mexido: Riot e Vanguard, Xbox e Game Pass, seus jogos e launchers, seus arquivos e seus programas.");
            return sb.ToString();
        }

        private void MakeReport()
        {
            Ui.Background("Gerando relatório de ganho", delegate
            {
                string text = Report.Generate();
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        MessageBox.Show(text + "\r\nSalvei também o arquivo PCTurbo_relatorio.txt na Área de Trabalho.", "PC Turbo - relatório de ganho");
                        try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + Report.ReportFile() + "\""); }
                        catch { }
                    }));
                }
                catch { }
            });
        }

        private void UndoAll()
        {
            if (Ui.Busy)
            {
                MessageBox.Show("Já tem uma tarefa rodando. Espere terminar.", "PC Turbo");
                return;
            }
            DialogResult r = MessageBox.Show(
                "Isso desfaz os ajustes de Windows que o OTIMIZAR TUDO aplica (plano de energia, Game Bar, timer, serviços, efeitos visuais e todos os outros da aba Otimização que podem ser desfeitos). Arquivos já limpos não voltam.\n\nDesfazer agora?",
                "Desfazer ajustes", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            Ui.Background("Desfazer ajustes", delegate
            {
                int ok = 0, fail = 0;
                foreach (Tweak tw in Tweaks.All())
                {
                    if (tw.Revert == null) continue;
                    try
                    {
                        tw.Revert();
                        ok++;
                        Log.Write("Desfeito: " + tw.Name);
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        Log.Write("Falhou ao desfazer: " + tw.Name + " - " + ex.Message);
                    }
                }
                Log.Write("Ajustes desfeitos: " + ok + (fail > 0 ? " (" + fail + " falharam)" : "") + ". Reinicie o PC para valer tudo.");
            });
        }

        private void DoUpdate()
        {
            if (Ui.Busy)
            {
                MessageBox.Show("Já tem uma tarefa rodando. Espere terminar.", "PC Turbo");
                return;
            }
            Ui.Background("Verificando atualização", delegate
            {
                string latest = Updater.LatestVersion();
                if (latest == null)
                {
                    Log.Write("Não consegui checar a versão (sem internet?).");
                    this.BeginInvoke(new Action(delegate { MessageBox.Show("Não consegui checar se há versão nova. Veja a internet e tente de novo.", "PC Turbo"); }));
                    return;
                }
                if (!Updater.IsNewer(latest))
                {
                    Log.Write("Você já está na versão mais nova (" + Updater.Current + ").");
                    this.BeginInvoke(new Action(delegate { MessageBox.Show("Você já está na versão mais nova (" + Updater.Current + ").", "PC Turbo"); }));
                    return;
                }
                Log.Write("Versão nova: " + latest + " (a sua é " + Updater.Current + ")");
                string err = Updater.DownloadAndReplace();
                if (err != null)
                {
                    Log.Write("Falhou a atualização: " + err);
                    this.BeginInvoke(new Action(delegate { MessageBox.Show("Não consegui atualizar:\n" + err + "\n\nSeu PC Turbo continua como estava.", "PC Turbo"); }));
                    return;
                }
                Log.Write("Arquivos novos baixados. Recompilando e reabrindo...");
                this.BeginInvoke(new Action(delegate
                {
                    MessageBox.Show("Atualizado para a versão " + latest + ".\nO PC Turbo vai fechar, recompilar e abrir sozinho (leva alguns segundos).", "PC Turbo");
                    try { Updater.RestartAndRecompile(); }
                    catch (Exception ex) { MessageBox.Show("Atualizei os arquivos, mas não consegui reabrir: " + ex.Message + "\nAbra pelo INICIAR.bat.", "PC Turbo"); }
                }));
            });
        }

        private void QuickOptimize()
        {
            if (Ui.Busy)
            {
                MessageBox.Show("Já tem uma tarefa rodando. Espere terminar.", "PC Turbo");
                return;
            }
            bool full = (dash != null && dash.ChkFull != null && dash.ChkFull.Checked);
            Log.Write("OTIMIZAR TUDO iniciado" + (full ? " (com manutenção completa)." : "."));

            Ui.Background("Limpeza e otimização completas", delegate
            {
                Report.StartBaseline();
                System.Collections.Generic.List<GuardItem> guardBefore = Guard.Snapshot();
                bool laptop = Machine.IsLaptop();
                if (laptop) Log.Write("Notebook detectado: vou pular os ajustes de energia que gastam bateria.");
                Maint.RestorePoint("PC Turbo - antes da otimizacao");

                System.Collections.Generic.List<string> cleanedList = new System.Collections.Generic.List<string>();
                System.Collections.Generic.List<string> fixesList = new System.Collections.Generic.List<string>();
                System.Collections.Generic.List<string> warnList = new System.Collections.Generic.List<string>();

                // quanto lixo tinha antes de limpar
                long found = 0;
                foreach (CleanTarget t in Cleaner.Targets())
                {
                    if (!t.Default) continue;
                    try { found += t.Size(); }
                    catch { }
                }
                Log.Write("Lixo encontrado: " + Fmt.Bytes(found));

                long total = 0;
                foreach (CleanTarget t in Cleaner.Targets())
                {
                    if (!t.Default) continue;
                    long freed = 0;
                    try { freed = t.Clean(); }
                    catch (Exception ex) { Log.Write("Falhou: " + t.Name + " - " + ex.Message); }
                    total += freed;
                    if (freed > 0) cleanedList.Add(t.Name + " - " + Fmt.Bytes(freed));
                    Log.Write("Limpo: " + t.Name + " (" + Fmt.Bytes(freed) + ")");
                }
                Log.Write("Espaço liberado: " + Fmt.Bytes(total));

                int okCount = 0, failCount = 0;
                int tweaksDone = 0;
                foreach (Tweak tw in Tweaks.All())
                {
                    if (!tw.Default) continue;
                    if (laptop && tw.DesktopOnly)
                    {
                        Log.Write("Notebook: pulei \"" + tw.Name + "\" para não gastar bateria.");
                        continue;
                    }
                    try
                    {
                        tw.Apply();
                        okCount++;
                        fixesList.Add(tw.Name);
                        Log.Write("Aplicado: " + tw.Name);
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        Log.Write("Falhou: " + tw.Name + " - " + ex.Message);
                    }
                }

                tweaksDone = okCount;
                Report.AddCleaned(total, true, tweaksDone);

                try { Maint.Trim(); } catch (Exception ex) { Log.Write("TRIM falhou: " + ex.Message); }
                try { Maint.FlushDns(); } catch (Exception ex) { Log.Write("DNS falhou: " + ex.Message); }

                try { Maint.ScheduleClean(true); } catch (Exception ex) { Log.Write("Limpeza semanal falhou: " + ex.Message); }
                try { Maint.ScheduleBackground(true); } catch (Exception ex) { Log.Write("Limpeza em segundo plano falhou: " + ex.Message); }

                string nv = NvidiaProfile.ApplyBest();
                if (nv == null) Log.Write("Aplicado: configs de FPS da NVIDIA (perfil global)");
                else Log.Write("NVIDIA ignorado: " + nv);

                DiagResult dg = Diag.Run();
                Log.Write("Taxa da tela: " + dg.Refresh);
                Log.Write("Memória RAM: " + dg.Ram);
                Log.Write("Resizable BAR: " + dg.Bar);
                Log.Write("Secure Boot / TPM: " + dg.Boot);

                if (dg.RefreshKnown && !dg.RefreshOk) warnList.Add("Taxa da tela: " + dg.Refresh);
                if (dg.RamKnown && !dg.RamOk) warnList.Add("Memória RAM: " + dg.Ram);
                if (dg.BarKnown && !dg.BarOk) warnList.Add("Resizable BAR: " + dg.Bar);
                if (dg.PlanKnown && !dg.PlanOk) warnList.Add("Plano de energia: " + dg.Plan);
                if (dg.DiskKnown && !dg.DiskOk) warnList.Add("Saúde do disco: " + dg.Disk);
                if (dg.BootKnown && !dg.BootOk) warnList.Add("Secure Boot / TPM: " + dg.Boot);
                // confere se os programas continuam como estavam (so leitura)
                System.Collections.Generic.List<GuardItem> guardAfter = Guard.Snapshot();
                System.Collections.Generic.List<string> guardWarn = Guard.Compare(guardBefore, guardAfter);
                foreach (string gw in guardWarn)
                {
                    warnList.Add(gw);
                    Log.Write("ATENÇÃO: " + gw);
                }
                Log.Write("Programas conferidos: " + guardAfter.Count + " (Epic, Steam, Riot, Discord e outros). " + (guardWarn.Count == 0 ? "Tudo igual ao de antes." : guardWarn.Count + " aviso(s)."));
                Report.SaveRun(found, total, cleanedList, fixesList, warnList);

                if (full)
                {
                    Log.Write("Manutenção completa: isso demora, pode deixar o PC trabalhando.");
                    try { Maint.Sfc(); } catch (Exception ex) { Log.Write("SFC falhou: " + ex.Message); }
                    try { Maint.Dism(); } catch (Exception ex) { Log.Write("DISM falhou: " + ex.Message); }
                }

                string report = "Pronto!\n\nEspaço liberado: " + Fmt.Bytes(total) +
                    "\nAjustes aplicados: " + okCount + (failCount > 0 ? " (" + failCount + " falharam, veja o quadro de baixo)" : "") +
                    "\nSSD (TRIM) e cache de DNS: feitos" +
                    "\nLimpeza automática semanal e em segundo plano: ligadas" +
                    (full ? "\nManutenção completa (SFC e DISM): feita" : "") +
                    "\nNVIDIA: " + (nv == null ? "configs de FPS aplicadas" : "não aplicado") +
                    "\nProgramas conferidos (Epic, Steam, Riot, Discord...): " + (guardWarn.Count == 0 ? "tudo igual ao de antes" : guardWarn.Count + " aviso(s), veja o quadro de baixo") +
                    "\n\nTela: " + dg.Refresh + "\nRAM: " + dg.Ram + "\nResizable BAR: " + dg.Bar + "\nSecure Boot / TPM: " + dg.Boot +
                    "\n\nPara tudo valer, o PC precisa reiniciar. Depois de reiniciar, clique em \"Gerar relatório de ganho\" no Painel para ver o antes e depois." +
                    "\n\nReiniciar agora?";
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
        // Usado pela limpeza automatica semanal: so os itens que vem marcados, sem janela
        private static void SilentClean()
        {
            long total = 0;
            Log.Write("Limpeza automática semanal iniciada.");
            foreach (CleanTarget t in Cleaner.Targets())
            {
                if (!t.Default) continue;
                try { total += t.Clean(); }
                catch (Exception ex) { Log.Write("Falhou: " + t.Name + " - " + ex.Message); }
            }
            Log.Write("Limpeza automática concluída. Espaço liberado: " + Fmt.Bytes(total));
            Report.AddCleaned(total, false, 0);
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0].ToLowerInvariant() == "/selftest")
            {
                Environment.Exit(SelfTest.Run());
                return;
            }
            if (args != null && args.Length > 0 && args[0].ToLowerInvariant() == "/fundo")
            {
                bool created;
                using (System.Threading.Mutex one = new System.Threading.Mutex(true, "PCTurbo_Fundo_Unico", out created))
                {
                    if (!created) return;
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new TrayApp());
                }
                return;
            }
            if (args != null && args.Length > 0 && args[0].ToLowerInvariant() == "/limpar")
            {
                SilentClean();
                return;
            }

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

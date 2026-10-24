using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PCTurbo
{
    // Lista editavel (jogos reconhecidos ou programas protegidos). Salva na hora.
    public class ListEditor : FlowLayoutPanel
    {
        private ListBox lst;
        private ComboBox cmb;
        private string file;
        private string[] defaults;

        public ListEditor(string file, string[] defaults, bool canReset)
        {
            this.file = file;
            this.defaults = defaults;
            this.FlowDirection = FlowDirection.TopDown;
            this.WrapContents = false;
            this.AutoSize = true;
            this.Margin = new Padding(0, 0, 0, 6);

            lst = new ListBox();
            lst.Width = 470;
            lst.Height = 120;
            lst.BorderStyle = BorderStyle.FixedSingle;
            lst.BackColor = Theme.Field;
            lst.ForeColor = Theme.Text;
            lst.Tag = "keep";

            cmb = new ComboBox();
            cmb.Width = 250;
            cmb.DropDown += delegate { FillRunning(); };

            Button add = Ui.Btn("Adicionar", 110, delegate { Add(); });
            Button rem = Ui.Btn("Remover", 100, delegate { Remove(); });
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.FlowDirection = FlowDirection.LeftToRight;
            row.WrapContents = false;
            row.AutoSize = true;
            row.Margin = new Padding(0, 4, 0, 0);
            cmb.Margin = new Padding(0, 8, 8, 0);
            row.Controls.Add(cmb);
            row.Controls.Add(add);
            row.Controls.Add(rem);
            if (canReset)
            {
                Button reset = Ui.Btn("Lista padrão", 110, delegate
                {
                    List<string> d = Settings.ReadList("__nao_existe__.txt", defaults);
                    Settings.WriteList(file, d);
                    Reload();
                });
                row.Controls.Add(reset);
            }

            this.Controls.Add(lst);
            this.Controls.Add(row);
            Reload();
        }

        private void Reload()
        {
            lst.Items.Clear();
            foreach (string n in Settings.ReadList(file, defaults)) lst.Items.Add(n);
        }

        private List<string> Current()
        {
            List<string> l = new List<string>();
            foreach (object o in lst.Items) l.Add(o.ToString());
            return l;
        }

        private void Add()
        {
            string n = Settings.Clean(cmb.Text);
            if (n.Length == 0) return;
            List<string> l = Current();
            if (!Settings.Has(l, n)) l.Add(n);
            Settings.WriteList(file, l);
            cmb.Text = "";
            Reload();
        }

        private void Remove()
        {
            if (lst.SelectedIndex < 0) return;
            List<string> l = Current();
            l.RemoveAt(lst.SelectedIndex);
            Settings.WriteList(file, l);
            Reload();
        }

        // Programas abertos com janela (para escolher sem digitar)
        private void FillRunning()
        {
            try
            {
                string cur = cmb.Text;
                cmb.Items.Clear();
                List<string> names = new List<string>();
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && p.ProcessName.Length > 0 && !names.Contains(p.ProcessName)) names.Add(p.ProcessName);
                    }
                    catch { }
                    try { p.Dispose(); }
                    catch { }
                }
                names.Sort();
                foreach (string n in names) cmb.Items.Add(n);
                cmb.Text = cur;
            }
            catch { }
        }
    }

    public class GamesTab : UserControl
    {
        public GamesTab()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(16);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;

            flow.Controls.Add(Section("Modo Jogo automático"));
            Label note = new Label();
            note.Width = 900;
            note.Height = 78;
            note.Text = "Quando um dos jogos da lista abre, o PC Turbo (pelo ícone na bandeja) fica mais atento: programas em segundo plano que usam mais de 5% do processador passam para prioridade baixa, e a limpeza automática espera o jogo fechar. " +
                "Quando o jogo fecha, tudo volta ao normal. Nunca mexe no jogo, no anti-cheat, no Windows, em drivers nem nos programas protegidos.";
            flow.Controls.Add(note);

            CheckBox chk = new CheckBox();
            chk.Text = "Ligar o Modo Jogo automático (precisa da limpeza em segundo plano ligada: aba Manutenção)";
            chk.AutoSize = true;
            chk.Margin = new Padding(6, 4, 0, 8);
            chk.Checked = Settings.GetBool("jogo_auto", true);
            chk.CheckedChanged += delegate { Settings.SetBool("jogo_auto", chk.Checked); };
            flow.Controls.Add(chk);

            flow.Controls.Add(Section("Jogos reconhecidos (nome do programa do jogo)"));
            Label n2 = new Label();
            n2.Width = 900;
            n2.Height = 24;
            n2.ForeColor = Theme.Muted;
            n2.Text = "Escolha na lista com o jogo aberto ou digite o nome (sem .exe).";
            flow.Controls.Add(n2);
            flow.Controls.Add(new ListEditor("jogos.txt", Settings.DefaultGames, true));

            flow.Controls.Add(Section("Programas que o PC Turbo nunca mexe (a sua lista)"));
            Label n3 = new Label();
            n3.Width = 900;
            n3.Height = 50;
            n3.ForeColor = Theme.Muted;
            n3.Text = "Já vêm protegidos de fábrica: Windows, drivers, Riot/Vanguard, Xbox, Epic, Steam, Discord, OBS e outros. Aqui você adiciona os seus. Eles não têm a prioridade baixada pelo equilíbrio de CPU nem entram em regras da aba Prioridades.";
            flow.Controls.Add(n3);
            flow.Controls.Add(new ListEditor("protegidos.txt", null, false));

            this.Controls.Add(flow);
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
    }
}

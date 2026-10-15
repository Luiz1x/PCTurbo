using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace PCTurbo
{
    // Lista os programas abertos com janela e deixa VOCE escolher o que fechar antes de jogar
    public class GameModeForm : Form
    {
        private ListView lv;

        private static readonly Regex Protected = new Regex(
            "^(explorer|dwm|csrss|winlogon|PCTurbo|ApplicationFrameHost|TextInputHost|ShellExperienceHost|StartMenuExperienceHost|SearchHost|SearchApp|SystemSettings|RuntimeBroker|taskmgr|vgc|vgtray|Riot.*|VALORANT.*|League.*|Xbox.*|GameBar.*|GamingServices.*|nv.*|NVIDIA.*|msedgewebview2|ctfmon|sihost|lsass|services)$",
            RegexOptions.IgnoreCase);

        public GameModeForm()
        {
            this.Text = "Modo Jogo - fechar programas de fundo";
            this.ClientSize = new Size(640, 460);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9.5f);

            lv = Ui.NewList();
            lv.Columns.Add("Programa", 260);
            lv.Columns.Add("Memória", 100);
            lv.Columns.Add("Janela", 240);

            Button bClose = Ui.Btn("Fechar marcados", 150, delegate { CloseChecked(); });
            Button bReload = Ui.Btn("Atualizar lista", 130, delegate { Reload(); });

            string desc = "Marque o que quer fechar antes de jogar (nada vem marcado). Salve seu trabalho antes: o programa é fechado como se você clicasse no X, e só é forçado se não responder. Riot, Xbox, NVIDIA e o Windows não aparecem aqui.";
            Control layout = Ui.Layout(desc, lv, Ui.Buttons(bClose, bReload));
            layout.Dock = DockStyle.Fill;
            this.Controls.Add(layout);
            Theme.Apply(this);
            Reload();
        }

        private void Reload()
        {
            lv.Items.Clear();
            int me = Process.GetCurrentProcess().Id;
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == me) continue;
                    if (p.MainWindowHandle == IntPtr.Zero) continue;
                    if (Protected.IsMatch(p.ProcessName)) continue;
                    if (seen.ContainsKey(p.ProcessName)) continue;
                    seen[p.ProcessName] = true;
                    ListViewItem it = new ListViewItem(p.ProcessName);
                    it.SubItems.Add(Fmt.Bytes(p.WorkingSet64));
                    it.SubItems.Add(p.MainWindowTitle);
                    it.Tag = p.ProcessName;
                    lv.Items.Add(it);
                }
                catch { }
            }
        }

        private void CloseChecked()
        {
            List<string> names = new List<string>();
            foreach (ListViewItem it in lv.Items)
            {
                if (it.Checked) names.Add((string)it.Tag);
            }
            if (names.Count == 0)
            {
                MessageBox.Show("Nada marcado.", "Modo Jogo");
                return;
            }

            foreach (string n in names)
            {
                foreach (Process p in Process.GetProcessesByName(n))
                {
                    try { p.CloseMainWindow(); } catch { }
                }
            }
            Thread.Sleep(2500);
            foreach (string n in names)
            {
                foreach (Process p in Process.GetProcessesByName(n))
                {
                    try
                    {
                        if (!p.HasExited) p.Kill();
                    }
                    catch { }
                }
                Log.Write("Modo Jogo: fechado " + n);
            }
            Reload();
        }
    }
}

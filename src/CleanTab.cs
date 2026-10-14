using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PCTurbo
{
    public class CleanTab : UserControl
    {
        private ListView lv;
        private List<CleanTarget> targets;

        public CleanTab()
        {
            this.Dock = DockStyle.Fill;
            targets = Cleaner.Targets();

            lv = Ui.NewList();
            lv.Columns.Add("Item", 300);
            lv.Columns.Add("Tamanho", 100);
            lv.Columns.Add("O que é", 600);

            foreach (CleanTarget t in targets)
            {
                ListViewItem it = new ListViewItem(t.Name);
                it.SubItems.Add("-");
                it.SubItems.Add(t.Desc);
                it.Checked = t.Default;
                it.Tag = t;
                lv.Items.Add(it);
            }

            Button bScan = Ui.Btn("Analisar tamanhos", 160, delegate { Scan(); });
            Button bClean = Ui.Btn("Limpar marcados", 160, delegate { CleanChecked(); });
            Button bAll = Ui.Btn("Marcar todos", 120, delegate
            {
                foreach (ListViewItem it in lv.Items) it.Checked = true;
            });
            Button bNone = Ui.Btn("Desmarcar todos", 130, delegate
            {
                foreach (ListViewItem it in lv.Items) it.Checked = false;
            });

            string desc = "Marque o que quer limpar. Clique em Analisar para ver quanto espaço cada item ocupa. Arquivos em uso são ignorados. Lixeira, cache de shaders e navegadores vêm desmarcados.";
            this.Controls.Add(Ui.Layout(desc, lv, Ui.Buttons(bScan, bClean, bAll, bNone)));
        }

        private void SetSizeText(CleanTarget t, string text)
        {
            foreach (ListViewItem it in lv.Items)
            {
                if (object.ReferenceEquals(it.Tag, t))
                {
                    it.SubItems[1].Text = text;
                    break;
                }
            }
        }

        private void Scan()
        {
            Ui.Background("Analisando tamanhos", delegate
            {
                long total = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    CleanTarget t = targets[i];
                    long size = 0;
                    try { size = t.Size(); } catch { }
                    total += size;
                    string text = Fmt.Bytes(size);
                    BeginInvoke(new Action(delegate { SetSizeText(t, text); }));
                }
                Log.Write("Total que dá para limpar: " + Fmt.Bytes(total));
            });
        }

        private void CleanChecked()
        {
            List<CleanTarget> sel = new List<CleanTarget>();
            bool recycle = false;
            foreach (ListViewItem it in lv.Items)
            {
                if (!it.Checked) continue;
                CleanTarget t = (CleanTarget)it.Tag;
                sel.Add(t);
                if (t.Name == "Lixeira") recycle = true;
            }
            if (sel.Count == 0)
            {
                MessageBox.Show("Nenhum item marcado.", "Limpeza");
                return;
            }

            string msg = "Vai limpar " + sel.Count + " item(ns).";
            if (recycle) msg += "\n\nA Lixeira está marcada: o que estiver nela será apagado para sempre.";
            msg += "\n\nContinuar?";
            DialogResult r = MessageBox.Show(msg, "Confirmar limpeza", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            Ui.Background("Limpeza", delegate
            {
                long total = 0;
                foreach (CleanTarget t in sel)
                {
                    long freed = 0;
                    try { freed = t.Clean(); }
                    catch (Exception ex) { Log.Write("Falhou: " + t.Name + " - " + ex.Message); }
                    total += freed;
                    Log.Write("Limpo: " + t.Name + " (" + Fmt.Bytes(freed) + ")");
                    CleanTarget done = t;
                    BeginInvoke(new Action(delegate { SetSizeText(done, "limpo"); }));
                }
                Log.Write("Espaço liberado no total: " + Fmt.Bytes(total));
            });
        }
    }
}

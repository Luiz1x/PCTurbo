using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PCTurbo
{
    public class TweaksTab : UserControl
    {
        private ListView lv;

        public TweaksTab()
        {
            this.Dock = DockStyle.Fill;

            lv = Ui.NewList();
            lv.Columns.Add("Ajuste", 340);
            lv.Columns.Add("O que faz", 640);

            foreach (Tweak t in Tweaks.All())
            {
                ListViewItem it = new ListViewItem(t.Name);
                it.SubItems.Add(t.Desc);
                it.Checked = t.Default;
                it.Tag = t;
                lv.Items.Add(it);
            }

            Button bApply = Ui.Btn("Aplicar marcados", 150, delegate { Run(true); });
            Button bRevert = Ui.Btn("Reverter marcados", 160, delegate { Run(false); });
            Button bAll = Ui.Btn("Marcar os recomendados", 190, delegate
            {
                foreach (ListViewItem it in lv.Items) it.Checked = ((Tweak)it.Tag).Default;
            });
            Button bNone = Ui.Btn("Desmarcar todos", 130, delegate
            {
                foreach (ListViewItem it in lv.Items) it.Checked = false;
            });
            Button bRp = Ui.Btn("Criar ponto de restauração", 200, delegate
            {
                Ui.Background("Ponto de restauração", delegate { Maint.RestorePoint("PC Turbo - manual"); });
            });

            string desc = "Ajustes de desempenho do Windows. Cada um pode ser revertido. Marque, aplique e reinicie o PC. Recomendo criar um ponto de restauração antes.";
            this.Controls.Add(Ui.Layout(desc, lv, Ui.Buttons(bApply, bRevert, bAll, bNone, bRp)));
        }

        private void Run(bool apply)
        {
            List<Tweak> sel = new List<Tweak>();
            foreach (ListViewItem it in lv.Items)
            {
                if (it.Checked) sel.Add((Tweak)it.Tag);
            }
            if (sel.Count == 0)
            {
                MessageBox.Show("Nenhum ajuste marcado.", "Otimização");
                return;
            }

            string verb = apply ? "aplicar" : "reverter";
            DialogResult r = MessageBox.Show("Vai " + verb + " " + sel.Count + " ajuste(s). Continuar?",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            Ui.Background(apply ? "Aplicando ajustes" : "Revertendo ajustes", delegate
            {
                foreach (Tweak t in sel)
                {
                    try
                    {
                        if (apply)
                        {
                            t.Apply();
                            Log.Write("Aplicado: " + t.Name);
                        }
                        else if (t.Revert == null)
                        {
                            Log.Write("Sem reversão automática: " + t.Name);
                        }
                        else
                        {
                            t.Revert();
                            Log.Write("Revertido: " + t.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Falhou: " + t.Name + " - " + ex.Message);
                    }
                }
                Log.Write("Reinicie o PC para todos os ajustes valerem.");
            });
        }
    }
}

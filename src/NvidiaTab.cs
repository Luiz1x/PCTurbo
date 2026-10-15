using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace PCTurbo
{
    public class NvSetting
    {
        public string Name;
        public string Rec;
        public string Why;
        public uint Id;
        public uint Value;
    }

    public static class NvidiaProfile
    {
        public const uint FrameLimitId = 0x10835002;

        public static List<NvSetting> Fps()
        {
            List<NvSetting> l = new List<NvSetting>();
            l.Add(Make("Modo de gerenciamento de energia", "Preferir desempenho máximo", "A placa não baixa o clock no meio do jogo.", 0x1057EB71, 1));
            l.Add(Make("Filtragem de textura - qualidade", "Alto desempenho", "Mais FPS, diferença visual quase nula.", 0x00CE2691, 0x14));
            l.Add(Make("Modo de baixa latência", "Ligado", "Reduz a fila de quadros da CPU (menos input lag).", 0x007BA09E, 1));
            l.Add(Make("Taxa de atualização preferida", "Maior disponível", "Garante a taxa máxima do monitor nos jogos.", 0x0064B541, 1));
            return l;
        }

        private static NvSetting Make(string n, string r, string w, uint id, uint v)
        {
            NvSetting s = new NvSetting();
            s.Name = n; s.Rec = r; s.Why = w; s.Id = id; s.Value = v;
            return s;
        }

        public static string Describe(uint id, long v)
        {
            if (v == -1) return "Padrão do driver";
            if (v == -2) return "Não consegui ler";
            if (id == 0x1057EB71) return v == 1 ? "Preferir desempenho máximo" : (v == 0 ? "Adaptativo / ótimo" : "Outro (" + v + ")");
            if (id == 0x00CE2691) return v == 0x14 ? "Alto desempenho" : "Outro (0x" + v.ToString("X") + ")";
            if (id == 0x007BA09E) return v == 1 ? "Ligado" : "Outro (" + v + ")";
            if (id == 0x0064B541) return v == 1 ? "Maior disponível" : "Outro (" + v + ")";
            if (id == FrameLimitId) return v + " FPS";
            return v.ToString();
        }

        // Usado tambem pelo botao "Limpar e otimizar tudo"
        public static string ApplyBest()
        {
            List<NvSetting> l = Fps();
            uint[] ids = new uint[l.Count];
            uint[] vals = new uint[l.Count];
            for (int i = 0; i < l.Count; i++) { ids[i] = l[i].Id; vals[i] = l[i].Value; }
            return NvidiaDrs.Apply(ids, vals);
        }
    }

    public class NvidiaTab : UserControl
    {
        private ListView lv;
        private CheckBox chkCap;
        private NumericUpDown numCap;
        private List<NvSetting> items;

        private const string Manual =
            "OUTRAS CONFIGURAÇÕES (faça no Painel de Controle NVIDIA > Gerenciar configurações 3D, não dá pra automatizar com segurança):\r\n" +
            " - Sincronização vertical: Usar a configuração do aplicativo (se tiver G-SYNC: Ligado no painel e limite de FPS 3 abaixo da taxa da tela).\r\n" +
            " - Otimização de threading: Automático.   Cache de shaders: Driver padrão ou 10 GB.   DSR e nitidez de imagem: Desativados.\r\n" +
            " - Suavização (FXAA/modo/gama) e Filtragem anisotrópica: Controlado pelo aplicativo (configure dentro do jogo).\r\n" +
            " - Jogos com NVIDIA Reflex (ex.: Valorant) ignoram o modo de baixa latência do painel e usam o Reflex do próprio jogo: ative Reflex = Ligado + Boost lá.\r\n" +
            " - Depois de aplicar, feche e reabra o Painel de Controle NVIDIA para ver os valores novos.";

        public NvidiaTab()
        {
            this.Dock = DockStyle.Fill;
            items = NvidiaProfile.Fps();

            lv = Ui.NewList();
            lv.Columns.Add("Configuração (Painel NVIDIA)", 270);
            lv.Columns.Add("Recomendado p/ FPS", 210);
            lv.Columns.Add("Atual no driver", 210);
            lv.Columns.Add("Por quê", 330);
            foreach (NvSetting s in items)
            {
                ListViewItem it = new ListViewItem(s.Name);
                it.SubItems.Add(s.Rec);
                it.SubItems.Add("...");
                it.SubItems.Add(s.Why);
                it.Checked = true;
                it.Tag = s;
                lv.Items.Add(it);
            }

            chkCap = new CheckBox();
            chkCap.Text = "Limitar FPS máximo em:";
            chkCap.AutoSize = true;
            chkCap.Margin = new Padding(12, 10, 0, 0);
            numCap = new NumericUpDown();
            numCap.Minimum = 30;
            numCap.Maximum = 1000;
            numCap.Value = 237;
            numCap.Width = 70;
            numCap.Margin = new Padding(4, 8, 0, 0);
            Label capHint = new Label();
            capHint.Text = "(opcional; dica: taxa da tela - 3)";
            capHint.AutoSize = true;
            capHint.Margin = new Padding(4, 12, 0, 0);

            TextBox txt = new TextBox();
            txt.Multiline = true;
            txt.ReadOnly = true;
            txt.ScrollBars = ScrollBars.Vertical;
            txt.Text = Manual;
            txt.Dock = DockStyle.Fill;

            TableLayoutPanel mid = new TableLayoutPanel();
            mid.Dock = DockStyle.Fill;
            mid.ColumnCount = 1;
            mid.RowCount = 2;
            mid.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            mid.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            mid.Controls.Add(lv, 0, 0);
            mid.Controls.Add(txt, 0, 1);

            Button bBest = Ui.Btn("Aplicar melhores configs de FPS", 240, delegate { ApplyClick(); });
            Button bRestore = Ui.Btn("Restaurar padrão", 140, delegate { RestoreClick(); });
            Button bOpen = Ui.Btn("Abrir Painel NVIDIA", 160, delegate { OpenPanel(); });

            string desc = "Ajusta o perfil global do driver (Gerenciar configurações 3D > Configurações globais). Tudo reversível pelo botão Restaurar padrão.";
            this.Controls.Add(Ui.Layout(desc, mid, Ui.Buttons(bBest, bRestore, bOpen, chkCap, numCap, capHint)));

            RefreshValues();
        }

        private uint[] AllIds()
        {
            List<uint> ids = new List<uint>();
            foreach (NvSetting s in items) ids.Add(s.Id);
            ids.Add(NvidiaProfile.FrameLimitId);
            return ids.ToArray();
        }

        private void RefreshValues()
        {
            uint[] ids = AllIds();
            long[] res = new long[ids.Length];
            string err = NvidiaDrs.Read(ids, res);
            for (int i = 0; i < lv.Items.Count; i++)
            {
                lv.Items[i].SubItems[2].Text = err == null ? NvidiaProfile.Describe(ids[i], res[i]) : "indisponível";
            }
            if (err != null) Log.Write("NVIDIA: " + err);
        }

        private void ApplyClick()
        {
            List<uint> ids = new List<uint>();
            List<uint> vals = new List<uint>();
            foreach (ListViewItem it in lv.Items)
            {
                if (!it.Checked) continue;
                NvSetting s = (NvSetting)it.Tag;
                ids.Add(s.Id);
                vals.Add(s.Value);
            }
            if (chkCap.Checked)
            {
                ids.Add(NvidiaProfile.FrameLimitId);
                vals.Add((uint)numCap.Value);
            }
            if (ids.Count == 0)
            {
                MessageBox.Show("Nada marcado.", "NVIDIA");
                return;
            }
            uint[] a = ids.ToArray();
            uint[] b = vals.ToArray();
            Ui.Background("Aplicando configs NVIDIA", delegate
            {
                string err = NvidiaDrs.Apply(a, b);
                if (err == null) Log.Write("Configs NVIDIA aplicadas (" + a.Length + "). Reabra o Painel de Controle NVIDIA para ver.");
                else Log.Write("NVIDIA: " + err);
                Refresh2();
            });
        }

        private void RestoreClick()
        {
            DialogResult r = MessageBox.Show("Voltar estas configurações ao padrão do driver?", "NVIDIA",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            uint[] ids = AllIds();
            Ui.Background("Restaurando padrão NVIDIA", delegate
            {
                string err = NvidiaDrs.Restore(ids);
                if (err == null) Log.Write("Configs NVIDIA voltaram ao padrão.");
                else Log.Write("NVIDIA: " + err);
                Refresh2();
            });
        }

        private void Refresh2()
        {
            try { BeginInvoke(new Action(RefreshValues)); } catch { }
        }

        private void OpenPanel()
        {
            try
            {
                Process.Start("shell:AppsFolder\\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel");
            }
            catch
            {
                try { Process.Start("control.exe", "/name NVIDIA.ControlPanel"); }
                catch { MessageBox.Show("Abra pelo botão direito na Área de Trabalho > Painel de Controle NVIDIA.", "NVIDIA"); }
            }
        }

        private void CheckBar()
        {
            Ui.Background("Verificando Resizable BAR", delegate
            {
                long totalMiB = 0;
                int rc = -1;
                try
                {
                    rc = Proc.Run("nvidia-smi.exe", "-q -d MEMORY", delegate(string line)
                    {
                        if (totalMiB != 0) return;
                        // procura a secao BAR1; o primeiro "Total" depois dela e o tamanho do BAR
                        if (line.IndexOf("BAR1", StringComparison.OrdinalIgnoreCase) >= 0) bar1 = true;
                        else if (bar1 && line.StartsWith("Total"))
                        {
                            string[] p = line.Split(':');
                            if (p.Length > 1)
                            {
                                string n = p[1].Replace("MiB", "").Trim();
                                long v;
                                if (long.TryParse(n, out v)) totalMiB = v;
                            }
                        }
                    });
                }
                catch (Exception ex) { Log.Write("nvidia-smi: " + ex.Message); }
                bar1 = false;
                if (totalMiB == 0) Log.Write("Resizable BAR: não consegui ler (nvidia-smi indisponível).");
                else if (totalMiB > 256) Log.Write("Resizable BAR: ATIVO (BAR1 = " + totalMiB + " MiB).");
                else Log.Write("Resizable BAR: parece DESLIGADO (BAR1 = " + totalMiB + " MiB). Se a placa e a BIOS suportarem, ative 'Above 4G Decoding' e 'Re-Size BAR' na BIOS.");
            });
        }

        private bool bar1;
    }
}

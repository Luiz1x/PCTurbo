using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PCTurbo
{
    // Regras de prioridade por programa (como no Process Lasso). Ficam salvas em AppData
    // e a bandeja do PC Turbo reaplica sozinha toda vez que o programa abre.
    public static class PriorityRules
    {
        private static Dictionary<string, ProcessPriorityClass> cache = new Dictionary<string, ProcessPriorityClass>();
        private static DateTime cacheTime = DateTime.MinValue;

        public static readonly string[] Labels = { "Baixa", "Abaixo do normal", "Normal", "Acima do normal", "Alta" };
        private static readonly ProcessPriorityClass[] Classes =
        {
            ProcessPriorityClass.Idle, ProcessPriorityClass.BelowNormal, ProcessPriorityClass.Normal,
            ProcessPriorityClass.AboveNormal, ProcessPriorityClass.High
        };

        public static ProcessPriorityClass FromIndex(int i)
        {
            if (i < 0 || i >= Classes.Length) return ProcessPriorityClass.Normal;
            return Classes[i];
        }

        public static string Label(ProcessPriorityClass c)
        {
            for (int i = 0; i < Classes.Length; i++)
            {
                if (Classes[i] == c) return Labels[i];
            }
            return c.ToString();
        }

        private static string FilePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCTurbo");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "prioridades.txt");
        }

        public static Dictionary<string, ProcessPriorityClass> Load()
        {
            try
            {
                string f = FilePath();
                if (!File.Exists(f))
                {
                    cache = new Dictionary<string, ProcessPriorityClass>();
                    cacheTime = DateTime.MinValue;
                    return cache;
                }
                DateTime w = File.GetLastWriteTime(f);
                if (w != cacheTime)
                {
                    Dictionary<string, ProcessPriorityClass> d = new Dictionary<string, ProcessPriorityClass>();
                    foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string name = line.Substring(0, eq).Trim().ToLowerInvariant();
                        try
                        {
                            ProcessPriorityClass pc = (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), line.Substring(eq + 1).Trim());
                            if (name.Length > 0) d[name] = pc;
                        }
                        catch { }
                    }
                    cache = d;
                    cacheTime = w;
                }
            }
            catch { }
            return cache;
        }

        private static void Save(Dictionary<string, ProcessPriorityClass> d)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, ProcessPriorityClass> kv in d)
            {
                sb.AppendLine(kv.Key + "=" + kv.Value.ToString());
            }
            File.WriteAllText(FilePath(), sb.ToString(), Encoding.UTF8);
            cacheTime = DateTime.MinValue;
        }

        public static void Set(string name, ProcessPriorityClass pc)
        {
            Dictionary<string, ProcessPriorityClass> d = new Dictionary<string, ProcessPriorityClass>(Load());
            d[name.ToLowerInvariant()] = pc;
            Save(d);
        }

        public static void Remove(string name)
        {
            Dictionary<string, ProcessPriorityClass> d = new Dictionary<string, ProcessPriorityClass>(Load());
            d.Remove(name.ToLowerInvariant());
            Save(d);
        }

        // Aplica as regras nos programas que estiverem abertos agora
        public static void ApplyAll()
        {
            foreach (KeyValuePair<string, ProcessPriorityClass> kv in Load())
            {
                Process[] list;
                try { list = Process.GetProcessesByName(kv.Key); }
                catch { continue; }
                foreach (Process p in list)
                {
                    try
                    {
                        if (p.PriorityClass != kv.Value)
                        {
                            p.PriorityClass = kv.Value;
                            Log.Write("Prioridade de " + p.ProcessName + " definida para " + Label(kv.Value) + " (regra salva).");
                        }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
        }
    }

    public class PriorityTab : UserControl
    {
        private ListView lv;
        private ComboBox cbo;

        public PriorityTab()
        {
            this.Dock = DockStyle.Fill;

            lv = Ui.NewList();
            lv.CheckBoxes = false;
            lv.Columns.Add("Programa", 240);
            lv.Columns.Add("Memória", 100);
            lv.Columns.Add("Prioridade agora", 150);
            lv.Columns.Add("Regra salva (vale sempre)", 200);

            cbo = new ComboBox();
            cbo.DropDownStyle = ComboBoxStyle.DropDownList;
            cbo.Width = 180;
            cbo.Margin = new Padding(4, 8, 4, 4);
            foreach (string l in PriorityRules.Labels) cbo.Items.Add(l);
            cbo.SelectedIndex = 2;

            Label lbl = new Label();
            lbl.Text = "Prioridade:";
            lbl.AutoSize = true;
            lbl.Margin = new Padding(12, 12, 0, 0);

            Button bReload = Ui.Btn("Atualizar lista", 130, delegate { Reload(); });
            Button bSet = Ui.Btn("Aplicar e salvar regra", 190, delegate { SetRule(); });
            Button bDel = Ui.Btn("Remover regra", 140, delegate { RemoveRule(); });

            string desc = "Escolha um programa, a prioridade e clique em Aplicar. A regra fica salva e a bandeja do PC Turbo reaplica toda vez que o programa abrir (ligue a limpeza em segundo plano na aba Manutenção). Dica: Alta só em 1 ou 2 programas, como seu jogo. Windows, Riot/Vanguard, anti-cheat, drivers e launchers são protegidos.";
            this.Controls.Add(Ui.Layout(desc, lv, Ui.Buttons(bReload, lbl, cbo, bSet, bDel)));

            Reload();
        }

        private void Reload()
        {
            Dictionary<string, long> mem = new Dictionary<string, long>();
            Dictionary<string, string> now = new Dictionary<string, string>();
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.SessionId == 0) continue;
                    string n = p.ProcessName.ToLowerInvariant();
                    long m = 0;
                    try { m = p.WorkingSet64; } catch { }
                    if (!mem.ContainsKey(n)) { mem[n] = 0; now[n] = "sem acesso"; }
                    mem[n] += m;
                    try { now[n] = PriorityRules.Label(p.PriorityClass); } catch { }
                }
                catch { }
                finally { p.Dispose(); }
            }

            Dictionary<string, ProcessPriorityClass> rules = PriorityRules.Load();
            List<string> names = new List<string>(mem.Keys);
            foreach (string r in rules.Keys)
            {
                if (!mem.ContainsKey(r)) names.Add(r);
            }
            names.Sort(delegate(string a, string b)
            {
                long ma = mem.ContainsKey(a) ? mem[a] : -1;
                long mb = mem.ContainsKey(b) ? mem[b] : -1;
                return mb.CompareTo(ma);
            });

            lv.Items.Clear();
            foreach (string n in names)
            {
                bool running = mem.ContainsKey(n);
                ListViewItem it = new ListViewItem(n);
                it.SubItems.Add(running ? Fmt.Bytes(mem[n]) : "-");
                it.SubItems.Add(running ? now[n] : "(fechado)");
                ProcessPriorityClass rc;
                string ruleText = "";
                if (Balancer.IsProtected(n)) ruleText = "protegido";
                else if (rules.TryGetValue(n, out rc)) ruleText = PriorityRules.Label(rc);
                it.SubItems.Add(ruleText);
                it.Tag = n;
                lv.Items.Add(it);
            }
        }

        private string Selected()
        {
            if (lv.SelectedItems.Count == 0)
            {
                MessageBox.Show("Clique em um programa da lista primeiro.", "Prioridades");
                return null;
            }
            return (string)lv.SelectedItems[0].Tag;
        }

        private void SetRule()
        {
            string name = Selected();
            if (name == null) return;
            if (Balancer.IsProtected(name))
            {
                MessageBox.Show("Esse programa é protegido (Windows, Riot/Vanguard, anti-cheat, drivers ou launcher). Mudar a prioridade dele pode travar o PC ou ser barrado pelo anti-cheat, então eu não deixo.", "Prioridades");
                return;
            }
            ProcessPriorityClass pc = PriorityRules.FromIndex(cbo.SelectedIndex);
            try
            {
                PriorityRules.Set(name, pc);
                PriorityRules.ApplyAll();
                Log.Write("Regra salva: " + name + " = " + PriorityRules.Label(pc) + ". A bandeja reaplica sempre que o programa abrir.");
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui salvar a regra: " + ex.Message);
            }
            Reload();
        }

        private void RemoveRule()
        {
            string name = Selected();
            if (name == null) return;
            try
            {
                PriorityRules.Remove(name);
                Process[] list = Process.GetProcessesByName(name);
                foreach (Process p in list)
                {
                    try { p.PriorityClass = ProcessPriorityClass.Normal; }
                    catch { }
                    finally { p.Dispose(); }
                }
                Log.Write("Regra removida: " + name + " (voltou para Normal).");
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui remover a regra: " + ex.Message);
            }
            Reload();
        }
    }
}

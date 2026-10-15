using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCTurbo
{
    public class ProgInfo
    {
        public string Name = "";
        public string Publisher = "";
        public string Installed = "?";
        public string LastUse = "sem registro";
        public string Situacao = "";
        public string Quiet = "";
        public string Uninstall = "";
        public long SizeMB;
        public int Order;
    }

    public static class ProgramScanner
    {
        private static Dictionary<string, DateTime> ReadPrefetch()
        {
            Dictionary<string, DateTime> d = new Dictionary<string, DateTime>();
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                foreach (string f in Directory.GetFiles(dir, "*.pf"))
                {
                    string n = Path.GetFileName(f);
                    int idx = n.LastIndexOf('-');
                    if (idx <= 0) continue;
                    string exe = n.Substring(0, idx).ToUpperInvariant();
                    DateTime t = File.GetLastWriteTime(f);
                    DateTime cur;
                    if (!d.TryGetValue(exe, out cur) || t > cur) d[exe] = t;
                }
            }
            catch { }
            return d;
        }

        private static void CollectExe(string dir, int depth, List<string> acc)
        {
            try
            {
                foreach (string f in Directory.GetFiles(dir, "*.exe"))
                    acc.Add(Path.GetFileName(f).ToUpperInvariant());
                if (depth <= 0) return;
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    try
                    {
                        if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch { continue; }
                    CollectExe(sub, depth - 1, acc);
                }
            }
            catch { }
        }

        public static List<ProgInfo> Scan(int limitDays)
        {
            DateTime now = DateTime.Now;
            Dictionary<string, DateTime> last = ReadPrefetch();
            if (last.Count == 0) Log.Write("Aviso: o Windows não tem histórico de uso (Prefetch vazio). Tudo vai aparecer como 'sem registro': confira cada programa à mão.");

            Regex ignore = new Regex(@"^(UNINS|UNINST|UNINSTALL|SETUP|INSTALL|CRASH|VC_REDIST|VCREDIST)|UPDAT|HELPER", RegexOptions.IgnoreCase);
            Regex sys = new Regex(@"Visual C\+\+|\.NET|Desktop Runtime|Redistributable|WebView2|NVIDIA|Intel\(R\)|Intel |Realtek|DirectX|GIGABYTE|AMD |Microsoft Edge|Windows SDK|Driver|Chipset|GameInput|Update Health", RegexOptions.IgnoreCase);
            Regex prot = new Regex(@"Vanguard|Riot|Valorant|League of Legends|EasyAntiCheat|Easy Anti-Cheat|BattlEye|Gaming Services|Xbox", RegexOptions.IgnoreCase);
            Regex hotfix = new Regex(@"^(Update for|Security Update|Hotfix)|KB\d{6,}", RegexOptions.IgnoreCase);
            Regex iconExe = new Regex(@"([^\\""',]+\.exe)", RegexOptions.IgnoreCase);

            Dictionary<string, ProgInfo> found = new Dictionary<string, ProgInfo>(StringComparer.OrdinalIgnoreCase);

            string[] paths =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            RegistryKey[] roots = { Registry.LocalMachine, Registry.LocalMachine, Registry.CurrentUser };

            for (int i = 0; i < 3; i++)
            {
                using (RegistryKey baseKey = roots[i].OpenSubKey(paths[i]))
                {
                    if (baseKey == null) continue;
                    foreach (string subName in baseKey.GetSubKeyNames())
                    {
                        using (RegistryKey k = baseKey.OpenSubKey(subName))
                        {
                            if (k == null) continue;

                            string name = R.Str(k, "DisplayName").Trim();
                            if (name.Length == 0) continue;
                            if (found.ContainsKey(name)) continue;

                            string quiet = R.Str(k, "QuietUninstallString");
                            string unin = R.Str(k, "UninstallString");
                            if (quiet.Trim().Length == 0 && unin.Trim().Length == 0) continue;
                            if (R.Int(k, "SystemComponent") == 1) continue;
                            if (R.Int(k, "NoRemove") == 1) continue;
                            if (R.Str(k, "ParentKeyName").Length > 0) continue;
                            if (hotfix.IsMatch(name)) continue;
                            if (sys.IsMatch(name) || prot.IsMatch(name)) continue;

                            string loc = R.Str(k, "InstallLocation").Trim().Trim('"').TrimEnd('\\');

                            // Data de instalacao
                            DateTime installed = DateTime.MinValue;
                            bool hasInstalled = false;
                            string idt = R.Str(k, "InstallDate");
                            if (idt.Length == 8)
                            {
                                DateTime dt;
                                if (DateTime.TryParseExact(idt, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                                {
                                    installed = dt;
                                    hasInstalled = true;
                                }
                            }
                            if (!hasInstalled && loc.Length > 0 && Directory.Exists(loc))
                            {
                                try
                                {
                                    installed = Directory.GetCreationTime(loc);
                                    hasInstalled = true;
                                }
                                catch { }
                            }

                            // Executaveis do programa
                            List<string> exes = new List<string>();
                            string icon = R.Str(k, "DisplayIcon");
                            if (icon.Length > 0)
                            {
                                Match m = iconExe.Match(icon);
                                if (m.Success) exes.Add(m.Groups[1].Value.ToUpperInvariant());
                            }
                            if (loc.Length > 0 && loc.Split('\\').Length >= 3 && Directory.Exists(loc))
                                CollectExe(loc, 2, exes);

                            // Ultimo uso estimado
                            DateTime lastUse = DateTime.MinValue;
                            bool hasUse = false;
                            foreach (string exe in exes)
                            {
                                if (ignore.IsMatch(exe)) continue;
                                DateTime t;
                                if (last.TryGetValue(exe, out t))
                                {
                                    if (!hasUse || t > lastUse)
                                    {
                                        lastUse = t;
                                        hasUse = true;
                                    }
                                }
                            }

                            int days = 0;
                            if (hasUse) days = (int)(now - lastUse).TotalDays;
                            int daysInstalled = 99999;
                            if (hasInstalled) daysInstalled = (int)(now - installed).TotalDays;

                            ProgInfo p = new ProgInfo();
                            p.Name = name;
                            p.Publisher = R.Str(k, "Publisher");
                            p.Quiet = quiet;
                            p.Uninstall = unin;
                            int kb = R.Int(k, "EstimatedSize");
                            p.SizeMB = kb / 1024;
                            if (hasInstalled) p.Installed = installed.ToString("dd/MM/yyyy");
                            if (hasUse) p.LastUse = lastUse.ToString("dd/MM/yyyy");

                            if (hasUse && days >= limitDays)
                            {
                                p.Situacao = "PARADO há " + days + " dias";
                                p.Order = 1;
                            }
                            else if (hasUse)
                            {
                                p.Situacao = "Usado há " + days + " dias";
                                p.Order = 3;
                            }
                            else if (daysInstalled < limitDays)
                            {
                                p.Situacao = "Instalado recentemente";
                                p.Order = 3;
                            }
                            else
                            {
                                p.Situacao = "SEM REGISTRO DE USO";
                                p.Order = 2;
                            }

                            found[name] = p;
                        }
                    }
                }
            }

            List<ProgInfo> list = new List<ProgInfo>(found.Values);
            list.Sort(delegate(ProgInfo a, ProgInfo b)
            {
                int c = a.Order.CompareTo(b.Order);
                if (c != 0) return c;
                c = b.SizeMB.CompareTo(a.SizeMB);
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        public static string UninstallCommand(ProgInfo p)
        {
            string q = p.Quiet ?? "";
            string u = p.Uninstall ?? "";
            if (q.Trim().Length > 0) return q;
            if (u.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Match m = Regex.Match(u, @"\{[0-9A-Fa-f\-]{36}\}");
                if (m.Success) return "msiexec.exe /x " + m.Value + " /qn /norestart";
                return u;
            }
            if (Regex.IsMatch(u, @"unins\d*\.exe", RegexOptions.IgnoreCase))
                return u + " /VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
            return u;
        }
    }

    // ------------------------------------------------------------------
    // Aba
    // ------------------------------------------------------------------
    public class ProgramsTab : UserControl
    {
        private ListView lv;
        private NumericUpDown numDays;

        public ProgramsTab()
        {
            this.Dock = DockStyle.Fill;

            lv = Ui.NewList();
            lv.Columns.Add("Programa", 300);
            lv.Columns.Add("Tamanho", 80);
            lv.Columns.Add("Instalado", 85);
            lv.Columns.Add("Último uso (aprox.)", 125);
            lv.Columns.Add("Situação", 170);
            lv.Columns.Add("Editora", 180);

            numDays = new NumericUpDown();
            numDays.Minimum = 30;
            numDays.Maximum = 730;
            numDays.Value = 120;
            numDays.Width = 60;
            numDays.Margin = new Padding(4, 9, 4, 4);

            Label l1 = new Label();
            l1.Text = "Considerar parado após";
            l1.AutoSize = true;
            l1.Margin = new Padding(14, 12, 2, 4);
            Label l2 = new Label();
            l2.Text = "dias";
            l2.AutoSize = true;
            l2.Margin = new Padding(2, 12, 4, 4);

            Button bScan = Ui.Btn("Analisar programas", 150, delegate { Scan(); });
            Button bMark = Ui.Btn("Marcar parados e sem registro", 210, delegate { MarkStale(); });
            Button bClear = Ui.Btn("Desmarcar todos", 130, delegate
            {
                foreach (ListViewItem it in lv.Items) it.Checked = false;
            });
            Button bUn = Ui.Btn("Desinstalar marcados", 170, delegate { UninstallChecked(); });

            string desc = "Clique em Analisar. Laranja = parado há muito tempo, amarelo = sem registro de uso. A data é uma ESTIMATIVA do histórico do Windows: confira antes de remover. Drivers, anti-cheat, Riot e Xbox não aparecem.";
            this.Controls.Add(Ui.Layout(desc, lv, Ui.Buttons(bScan, l1, numDays, l2, bMark, bClear, bUn)));
        }

        private void MarkStale()
        {
            foreach (ListViewItem it in lv.Items)
            {
                ProgInfo p = (ProgInfo)it.Tag;
                if (p.Order <= 2) it.Checked = true;
            }
        }

        private void Scan()
        {
            int days = (int)numDays.Value;
            Ui.Background("Analisando programas instalados", delegate
            {
                List<ProgInfo> list = ProgramScanner.Scan(days);
                BeginInvoke(new Action(delegate { Fill(list); }));
            });
        }

        private void Fill(List<ProgInfo> list)
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            int stale = 0;
            foreach (ProgInfo p in list)
            {
                ListViewItem it = new ListViewItem(p.Name);
                it.SubItems.Add(p.SizeMB > 0 ? p.SizeMB + " MB" : "");
                it.SubItems.Add(p.Installed);
                it.SubItems.Add(p.LastUse);
                it.SubItems.Add(p.Situacao);
                it.SubItems.Add(p.Publisher);
                it.Tag = p;
                if (p.Order == 1)
                {
                    it.BackColor = Color.FromArgb(255, 224, 204);
                    it.ForeColor = Color.Black;
                    stale++;
                }
                else if (p.Order == 2)
                {
                    it.BackColor = Color.FromArgb(255, 245, 200);
                    it.ForeColor = Color.Black;
                    stale++;
                }
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            Log.Write(list.Count + " programas listados, " + stale + " candidatos a remover.");
        }

        private void UninstallChecked()
        {
            List<ProgInfo> sel = new List<ProgInfo>();
            foreach (ListViewItem it in lv.Items)
            {
                if (it.Checked) sel.Add((ProgInfo)it.Tag);
            }
            if (sel.Count == 0)
            {
                MessageBox.Show("Nenhum programa marcado.", "Desinstalar programas");
                return;
            }

            StringBuilderLite names = new StringBuilderLite();
            for (int i = 0; i < sel.Count && i < 20; i++) names.Add(" - " + sel[i].Name);
            if (sel.Count > 20) names.Add(" ... e mais " + (sel.Count - 20));

            string msg = "Vai desinstalar " + sel.Count + " programa(s):\n\n" + names.ToString() +
                "\n\nIsso também apaga as configurações deles. Continuar?";
            DialogResult r = MessageBox.Show(msg, "Confirmar desinstalação", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;

            Ui.Background("Desinstalando programas", delegate
            {
                Maint.RestorePoint("PC Turbo - antes de desinstalar");
                int n = 0;
                foreach (ProgInfo p in sel)
                {
                    n++;
                    Log.Write("[" + n + "/" + sel.Count + "] " + p.Name);
                    string cmd = ProgramScanner.UninstallCommand(p);
                    if (cmd.Trim().Length == 0)
                    {
                        Log.Write("   sem comando de desinstalação: remova pelas Configurações.");
                        continue;
                    }
                    try
                    {
                        int code = Proc.RunCmd(cmd);
                        if (code == 0 || code == 3010) Log.Write("   OK");
                        else Log.Write("   terminou com código " + code + " (pode ter pedido confirmação na janela do programa)");
                    }
                    catch (Exception ex)
                    {
                        Log.Write("   erro: " + ex.Message);
                    }
                }
                Log.Write("Clique em Analisar programas para atualizar a lista.");
            });
        }
    }

    // Pequeno auxiliar para juntar linhas (evita depender de System.Text aqui)
    public class StringBuilderLite
    {
        private System.Text.StringBuilder sb = new System.Text.StringBuilder();
        public void Add(string line)
        {
            if (sb.Length > 0) sb.Append("\n");
            sb.Append(line);
        }
        public override string ToString() { return sb.ToString(); }
    }
}

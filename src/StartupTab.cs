using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PCTurbo
{
    public class StartEntry
    {
        public string Name = "";
        public string Command = "";
        public string Origin = "";
        public RegistryKey ApprovedRoot;
        public string ApprovedPath = "";
        public string ApprovedName = "";
    }

    public class StartupTab : UserControl
    {
        private const string ApprovedRun = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedRun32 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        private const string ApprovedFolder = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        private ListView lv;

        public StartupTab()
        {
            this.Dock = DockStyle.Fill;

            lv = Ui.NewList();
            lv.Columns.Add("Programa", 230);
            lv.Columns.Add("Estado", 90);
            lv.Columns.Add("Origem", 200);
            lv.Columns.Add("Comando", 540);

            Button bRefresh = Ui.Btn("Atualizar lista", 140, delegate { Reload(); });
            Button bOff = Ui.Btn("Desativar marcados", 170, delegate { SetChecked(false); });
            Button bOn = Ui.Btn("Ativar marcados", 150, delegate { SetChecked(true); });
            Button bTm = Ui.Btn("Abrir Gerenciador de Tarefas", 220, delegate
            {
                try { System.Diagnostics.Process.Start("taskmgr.exe"); } catch { }
            });

            string desc = "Programas que abrem sozinhos com o Windows. Marque os que você não precisa e clique em Desativar. É o mesmo mecanismo do Gerenciador de Tarefas: dá para reativar a qualquer momento.";
            this.Controls.Add(Ui.Layout(desc, lv, Ui.Buttons(bRefresh, bOff, bOn, bTm)));

            Reload();
        }

        private static void AddRun(List<StartEntry> list, RegistryKey root, string runPath, string origin, string approvedPath)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(runPath))
                {
                    if (k == null) return;
                    foreach (string n in k.GetValueNames())
                    {
                        StartEntry e = new StartEntry();
                        e.Name = n;
                        e.Command = R.Str(k, n);
                        e.Origin = origin;
                        e.ApprovedRoot = root;
                        e.ApprovedPath = approvedPath;
                        e.ApprovedName = n;
                        list.Add(e);
                    }
                }
            }
            catch { }
        }

        private static void AddFolder(List<StartEntry> list, string folder, string origin, RegistryKey root)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (string f in Directory.GetFiles(folder))
                {
                    string fn = Path.GetFileName(f);
                    if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    StartEntry e = new StartEntry();
                    e.Name = Path.GetFileNameWithoutExtension(f);
                    e.Command = f;
                    e.Origin = origin;
                    e.ApprovedRoot = root;
                    e.ApprovedPath = ApprovedFolder;
                    e.ApprovedName = fn;
                    list.Add(e);
                }
            }
            catch { }
        }

        private static bool IsDisabled(StartEntry e)
        {
            try
            {
                using (RegistryKey k = e.ApprovedRoot.OpenSubKey(e.ApprovedPath))
                {
                    if (k == null) return false;
                    byte[] b = k.GetValue(e.ApprovedName) as byte[];
                    if (b == null || b.Length == 0) return false;
                    return (b[0] & 1) == 1;
                }
            }
            catch { return false; }
        }

        private static void SetEnabled(StartEntry e, bool enable)
        {
            byte[] data = new byte[12];
            if (enable)
            {
                data[0] = 2;
            }
            else
            {
                data[0] = 3;
                byte[] ft = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
                Array.Copy(ft, 0, data, 4, 8);
            }
            R.Set(e.ApprovedRoot, e.ApprovedPath, e.ApprovedName, data, RegistryValueKind.Binary);
        }

        private void Reload()
        {
            List<StartEntry> list = new List<StartEntry>();
            AddRun(list, Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Usuário (registro)", ApprovedRun);
            AddRun(list, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Todos os usuários (registro)", ApprovedRun);
            AddRun(list, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Todos os usuários (32 bits)", ApprovedRun32);
            AddFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Pasta Inicializar (usuário)", Registry.CurrentUser);
            AddFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Pasta Inicializar (todos)", Registry.LocalMachine);

            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (StartEntry e in list)
            {
                bool off = IsDisabled(e);
                ListViewItem it = new ListViewItem(e.Name);
                it.SubItems.Add(off ? "Desativado" : "Ativo");
                it.SubItems.Add(e.Origin);
                it.SubItems.Add(e.Command);
                it.Tag = e;
                if (off) it.ForeColor = Color.Gray;
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            Log.Write("Inicialização: " + list.Count + " itens encontrados.");
        }

        private void SetChecked(bool enable)
        {
            int count = 0;
            foreach (ListViewItem it in lv.Items)
            {
                if (!it.Checked) continue;
                StartEntry e = (StartEntry)it.Tag;
                try
                {
                    SetEnabled(e, enable);
                    count++;
                    Log.Write((enable ? "Ativado: " : "Desativado: ") + e.Name);
                }
                catch (Exception ex)
                {
                    Log.Write("Não consegui alterar " + e.Name + ": " + ex.Message);
                }
            }
            if (count == 0) MessageBox.Show("Nenhum item marcado.", "Inicialização");
            Reload();
        }
    }
}

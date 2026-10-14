using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace PCTurbo
{
    public static class Ui
    {
        // Controle que fica desativado enquanto uma tarefa longa roda
        public static Control BusyHost;

        public static Button Btn(string text, int width, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 34;
            b.Margin = new Padding(4);
            b.FlatStyle = FlatStyle.System;
            b.Click += onClick;
            return b;
        }

        // Botao grande e colorido (destaque)
        public static Button BigButton(string text, int width, int height, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = height;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(34, 177, 90);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.Click += onClick;
            return b;
        }

        public static bool Busy;

        public static ListView NewList()
        {
            ListView lv = new ListView();
            lv.View = View.Details;
            lv.CheckBoxes = true;
            lv.FullRowSelect = true;
            lv.GridLines = true;
            lv.HideSelection = false;
            lv.MultiSelect = false;
            lv.Dock = DockStyle.Fill;
            return lv;
        }

        public static FlowLayoutPanel Buttons(params Control[] items)
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.FlowDirection = FlowDirection.LeftToRight;
            f.WrapContents = true;
            f.Padding = new Padding(4);
            f.Controls.AddRange(items);
            return f;
        }

        // Descricao em cima, lista no meio, botoes embaixo
        public static TableLayoutPanel Layout(string description, Control middle, Control buttons)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 3;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

            Label l = new Label();
            l.Text = description;
            l.Dock = DockStyle.Fill;
            l.Padding = new Padding(6, 6, 6, 0);

            middle.Dock = DockStyle.Fill;
            buttons.Dock = DockStyle.Fill;

            t.Controls.Add(l, 0, 0);
            t.Controls.Add(middle, 0, 1);
            t.Controls.Add(buttons, 0, 2);
            return t;
        }

        // Roda uma tarefa longa em segundo plano, bloqueando as abas ate terminar
        public static void Background(string title, Action work)
        {
            if (Busy)
            {
                Log.Write("Já tem uma tarefa rodando. Espere terminar.");
                return;
            }
            Busy = true;
            Control host = BusyHost;
            if (host != null) host.Enabled = false;
            Log.Write(">> " + title);

            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    Log.Write("ERRO: " + ex.Message);
                }
                Log.Write("<< Concluído: " + title);
                Busy = false;
                if (host != null)
                {
                    try
                    {
                        host.BeginInvoke(new Action(delegate { host.Enabled = true; }));
                    }
                    catch { }
                }
            });
        }
    }
}

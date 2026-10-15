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
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Theme.Border;
            b.BackColor = Theme.Card;
            b.ForeColor = Theme.Text;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.Click += onClick;
            b.MouseEnter += delegate { b.BackColor = Theme.CardHover; };
            b.MouseLeave += delegate { b.BackColor = Theme.Card; };
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
            b.MouseEnter += delegate { b.BackColor = Color.FromArgb(28, 150, 76); };
            b.MouseLeave += delegate { b.BackColor = Color.FromArgb(34, 177, 90); };
            return b;
        }

        public static bool Busy;

        public static ListView NewList()
        {
            ListView lv = new ListView();
            lv.View = View.Details;
            lv.CheckBoxes = true;
            lv.FullRowSelect = true;
            lv.GridLines = false;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = Theme.Field;
            lv.ForeColor = Theme.Text;
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

    // Cores do visual escuro. Tudo fica aqui para ser facil de mudar.
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(14, 17, 22);
        public static readonly Color Strip = Color.FromArgb(10, 12, 16);
        public static readonly Color Card = Color.FromArgb(24, 29, 38);
        public static readonly Color CardHover = Color.FromArgb(36, 44, 57);
        public static readonly Color Field = Color.FromArgb(18, 23, 31);
        public static readonly Color Border = Color.FromArgb(46, 55, 70);
        public static readonly Color Text = Color.FromArgb(230, 234, 240);
        public static readonly Color Muted = Color.FromArgb(152, 162, 179);
        public static readonly Color Accent = Color.FromArgb(34, 177, 90);
        public static readonly Color AccentText = Color.FromArgb(61, 220, 132);
        public static readonly Color Warn = Color.FromArgb(255, 122, 69);

        // Pinta o controle e tudo que tem dentro. Controle com Tag "keep" mantem as proprias cores.
        public static void Apply(Control c)
        {
            bool keep = (c.Tag as string) == "keep";
            if (!keep)
            {
                if (c is ListView)
                {
                    c.BackColor = Field;
                    c.ForeColor = Text;
                }
                else if (c is TextBox || c is NumericUpDown)
                {
                    c.BackColor = Field;
                    c.ForeColor = Text;
                }
                else if (c is TabPage)
                {
                    TabPage tp = (TabPage)c;
                    tp.UseVisualStyleBackColor = false;
                    tp.BackColor = Bg;
                    tp.ForeColor = Text;
                }
                else if (c is Form || c is UserControl || c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel)
                {
                    c.BackColor = Bg;
                    c.ForeColor = Text;
                }
            }
            foreach (Control child in c.Controls) Apply(child);
        }
    }

    // Barra de progresso lisa (a padrao do Windows nao aceita cores)
    public class FlatBar : ProgressBar
    {
        public FlatBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle r = ClientRectangle;
            using (SolidBrush track = new SolidBrush(Color.FromArgb(34, 42, 54)))
            {
                e.Graphics.FillRectangle(track, r);
            }
            int range = Maximum - Minimum;
            if (range <= 0) return;
            int w = (int)((long)r.Width * (Value - Minimum) / range);
            if (w <= 0) return;
            double pct = 100.0 * (Value - Minimum) / range;
            Color fill = pct >= 85 ? Theme.Warn : Theme.Accent;
            using (SolidBrush b = new SolidBrush(fill))
            {
                e.Graphics.FillRectangle(b, 0, 0, w, r.Height);
            }
        }
    }
}

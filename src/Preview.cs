using System;
using System.Drawing;
using System.Windows.Forms;

namespace PCTurbo
{
    // Janela simples que mostra a previa do OTIMIZAR TUDO
    public class PreviewForm : Form
    {
        public static void ShowText(IWin32Window owner, string text)
        {
            using (PreviewForm f = new PreviewForm(text))
            {
                f.ShowDialog(owner);
            }
        }

        private PreviewForm(string text)
        {
            this.Text = "PC Turbo - o que vai mudar";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(760, 600);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.Font = new Font("Segoe UI", 10f);
            box.Dock = DockStyle.Fill;
            box.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            box.SelectionStart = 0;
            box.SelectionLength = 0;

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 54;
            Button close = Ui.Btn("Fechar", 120, delegate { this.Close(); });
            close.Left = 620;
            close.Top = 10;
            bottom.Controls.Add(close);

            this.Controls.Add(box);
            this.Controls.Add(bottom);
            this.AcceptButton = close;
            Theme.Apply(this);
        }
    }
}

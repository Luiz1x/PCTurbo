using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace PCTurbo
{
    public class CardTile
    {
        public string Label = "";
        public string Value = "";
        public string Sub = "";
        public int Better;   // 1 melhorou, -1 piorou, 0 neutro
    }

    public class CardData
    {
        public string Hardware = "";
        public string Footer = "";
        public List<CardTile> Tiles = new List<CardTile>();
    }

    // Cartao de resultado em imagem (PNG) para mandar para os amigos.
    // Mostra os numeros como estao: o que melhorou em verde e o que piorou em laranja.
    public static class Card
    {
        public const int Width = 1200;
        public const int Height = 675;

        public static string FilePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PCTurbo_resultado.png");
        }

        private static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Desenha a imagem e salva em 'path'
        public static void Draw(CardData data, string path)
        {
            using (Bitmap bmp = new Bitmap(Width, Height))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), Color.FromArgb(10, 13, 18), Color.FromArgb(20, 30, 28), 60f))
                {
                    g.FillRectangle(bg, 0, 0, Width, Height);
                }

                // cabecalho
                using (SolidBrush green = new SolidBrush(Theme.Accent))
                {
                    g.FillEllipse(green, 48, 40, 52, 52);
                }
                Point[] bolt = { new Point(80, 46), new Point(62, 70), new Point(74, 70), new Point(70, 88), new Point(90, 62), new Point(78, 62) };
                g.FillPolygon(Brushes.White, bolt);
                using (Font f = new Font("Segoe UI", 30f, FontStyle.Bold))
                using (SolidBrush w = new SolidBrush(Color.White))
                {
                    g.DrawString("PC Turbo", f, w, 116, 38);
                }
                using (Font f = new Font("Segoe UI", 14f))
                using (SolidBrush m = new SolidBrush(Theme.Muted))
                {
                    g.DrawString(data.Hardware, f, m, 52, 108);
                }
                using (Font f = new Font("Segoe UI", 14f, FontStyle.Bold))
                using (SolidBrush a = new SolidBrush(Theme.AccentText))
                {
                    StringFormat right = new StringFormat();
                    right.Alignment = StringAlignment.Far;
                    g.DrawString("Meu resultado", f, a, new RectangleF(700, 50, 452, 30), right);
                }

                // 6 blocos (2 linhas x 3 colunas)
                int cols = 3;
                float tw = 352, th = 190, gap = 24, x0 = 48, y0 = 164;
                for (int i = 0; i < data.Tiles.Count && i < 6; i++)
                {
                    CardTile t = data.Tiles[i];
                    float x = x0 + (i % cols) * (tw + gap);
                    float y = y0 + (i / cols) * (th + gap);
                    RectangleF rect = new RectangleF(x, y, tw, th);
                    using (GraphicsPath gp = Round(rect, 16))
                    using (SolidBrush card = new SolidBrush(Color.FromArgb(28, 34, 44)))
                    using (Pen border = new Pen(Color.FromArgb(50, 60, 74), 1.5f))
                    {
                        g.FillPath(card, gp);
                        g.DrawPath(border, gp);
                    }

                    Color vc = t.Better > 0 ? Theme.AccentText : (t.Better < 0 ? Theme.Warn : Color.White);
                    using (Font f = new Font("Segoe UI", 13f))
                    using (SolidBrush m = new SolidBrush(Theme.Muted))
                    {
                        g.DrawString(t.Label, f, m, x + 22, y + 18);
                    }
                    using (Font f = new Font("Segoe UI", 34f, FontStyle.Bold))
                    using (SolidBrush v = new SolidBrush(vc))
                    {
                        g.DrawString(t.Value, f, v, new RectangleF(x + 18, y + 52, tw - 30, 70));
                    }
                    using (Font f = new Font("Segoe UI", 12f))
                    using (SolidBrush m = new SolidBrush(Theme.Muted))
                    {
                        g.DrawString(t.Sub, f, m, new RectangleF(x + 22, y + 128, tw - 36, 52));
                    }
                }

                // rodape
                using (Font f = new Font("Segoe UI", 11f))
                using (SolidBrush m = new SolidBrush(Color.FromArgb(120, 130, 146)))
                {
                    g.DrawString(data.Footer, f, m, new RectangleF(48, 618, 1104, 40));
                }

                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static string Diff(double before, double after, string unit, bool lowerIsBetter, out int better)
        {
            return Report.Diff(before, after, unit, lowerIsBetter, out better);
        }

        // Monta os dados com o que o app guardou + uma medicao de agora (leva ~3 s)
        public static CardData Collect()
        {
            Snap now = Report.Take();
            Snap before = Report.LoadBefore();
            long cleaned;
            int cleanings, runs;
            string last;
            Report.Totals(out cleaned, out cleanings, out runs, out last);
            FpsResult fa = Report.LoadFps("antes");
            FpsResult fd = Report.LoadFps("depois");

            CardData d = new CardData();
            d.Hardware = Report.Cpu() + "  •  " + Report.Gpu() + "  •  " + Math.Round(now.RamTotalMB / 1024.0, 0) + " GB de RAM";
            d.Footer = "Gerado em " + DateTime.Now.ToString("dd/MM/yyyy") + " pelo PC Turbo " + Updater.Current + ". Números medidos neste PC: o resultado muda de PC para PC e de partida para partida.";

            CardTile t1 = new CardTile();
            t1.Label = "Lixo removido";
            t1.Value = Fmt.Bytes(cleaned);
            t1.Sub = cleanings + " limpezas (manual, semanal e em segundo plano)";
            t1.Better = cleaned > 0 ? 1 : 0;
            d.Tiles.Add(t1);

            CardTile t2 = new CardTile();
            t2.Label = "Programas rodando";
            t2.Value = now.Procs.ToString();
            CardTile t3 = new CardTile();
            t3.Label = "RAM em uso";
            t3.Value = now.RamUsedMB + " MB";
            CardTile t4 = new CardTile();
            t4.Label = "CPU parada";
            t4.Value = now.CpuAvg >= 0 ? Math.Round(now.CpuAvg, 1).ToString("0.0") + "%" : "?";
            CardTile t5 = new CardTile();
            t5.Label = "Livre no C:";
            t5.Value = Math.Round(now.DiskFreeMB / 1024.0, 1).ToString("0.0") + " GB";

            if (before != null)
            {
                int b;
                string x = Diff(before.Procs, now.Procs, "", true, out b);
                t2.Sub = "antes: " + before.Procs + "  (" + x + ")";
                t2.Better = b;
                x = Diff(before.RamUsedMB, now.RamUsedMB, " MB", true, out b);
                t3.Sub = "antes: " + before.RamUsedMB + " MB  (" + x + ")";
                t3.Better = b;
                if (before.CpuAvg >= 0 && now.CpuAvg >= 0)
                {
                    x = Diff(before.CpuAvg, now.CpuAvg, "%", true, out b);
                    t4.Sub = "antes: " + Math.Round(before.CpuAvg, 1).ToString("0.0") + "%  (" + x + ")";
                    t4.Better = b;
                }
                x = Diff(Math.Round(before.DiskFreeMB / 1024.0, 1), Math.Round(now.DiskFreeMB / 1024.0, 1), " GB", false, out b);
                t5.Sub = "antes: " + Math.Round(before.DiskFreeMB / 1024.0, 1).ToString("0.0") + " GB  (" + x + ")";
                t5.Better = b;
            }
            else
            {
                t2.Sub = "ainda sem o antes (clique em OTIMIZAR TUDO)";
                t3.Sub = t2.Sub;
                t4.Sub = t2.Sub;
                t5.Sub = t2.Sub;
            }
            d.Tiles.Add(t2);
            d.Tiles.Add(t3);
            d.Tiles.Add(t4);
            d.Tiles.Add(t5);

            CardTile t6 = new CardTile();
            if (fa != null && fd != null && string.Equals(fa.Proc, fd.Proc, StringComparison.OrdinalIgnoreCase))
            {
                int b;
                string x = Diff(fa.Avg, fd.Avg, " FPS", false, out b);
                t6.Label = "FPS médio (" + fd.Proc.Replace(".exe", "") + ")";
                t6.Value = Math.Round(fa.Avg, 0) + " → " + Math.Round(fd.Avg, 0);
                int b2;
                string y = Diff(fa.Low1, fd.Low1, " FPS", false, out b2);
                t6.Sub = "1% low: " + Math.Round(fa.Low1, 0) + " → " + Math.Round(fd.Low1, 0) + "  (" + y + ")";
                t6.Better = b;
            }
            else
            {
                t6.Label = "FPS no jogo";
                t6.Value = "não medido";
                t6.Sub = "meça antes e depois na aba Meu ganho";
            }
            d.Tiles.Add(t6);
            return d;
        }

        // Gera a imagem na Area de Trabalho e devolve o caminho
        public static string Make()
        {
            CardData d = Collect();
            string p = FilePath();
            Draw(d, p);
            Log.Write("Cartão de resultado salvo em: " + p);
            return p;
        }
    }
}

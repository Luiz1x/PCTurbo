using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PCTurbo
{
    // Autoteste: o GitHub roda "PCTurbo.exe /selftest" a cada envio.
    // Confere as partes que nao dependem do PC de ninguem (leitura de dados, contas, regras de protecao).
    public static class SelfTest
    {
        private static List<string> fails = new List<string>();
        private static int total;

        private static void Check(string what, bool ok)
        {
            total++;
            if (!ok) fails.Add(what);
        }

        private static bool Near(double a, double b, double tol)
        {
            return Math.Abs(a - b) <= tol;
        }

        private static GuardItem G(string name, string version)
        {
            GuardItem g = new GuardItem();
            g.Name = name;
            g.Version = version;
            g.Path = "C:\\x\\" + name + ".exe";
            return g;
        }

        public static int Run()
        {
            try
            {
                Atualizador();
                Fps();
                Contas();
                Vigia();
                Ajustes();
                Protecao();
                Config();
                Imagem();
            }
            catch (Exception ex)
            {
                fails.Add("o autoteste quebrou: " + ex.Message);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Autoteste do PC Turbo " + Updater.Current + ": " + (total - fails.Count) + " de " + total + " conferencias ok.");
            foreach (string f in fails) sb.AppendLine("FALHOU: " + f);
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"), sb.ToString(), Encoding.UTF8); }
            catch { }
            return fails.Count == 0 ? 0 : 1;
        }

        private static void Atualizador()
        {
            string json = "{\"tag_name\":\"v1.2.3\",\"assets\":[{\"browser_download_url\":\"https://x/y/PCTurbo-fonte.zip\"},{\"browser_download_url\":\"https://x/y/PCTurbo-fonte.zip.sha256\"},{\"browser_download_url\":\"https://x/y/PCTurbo-completo.zip\"}]}";
            string v, a, h;
            bool ok = Updater.ParseRelease(json, out v, out a, out h);
            Check("Release completa e lida", ok && v == "1.2.3" && a != null && a.EndsWith("PCTurbo-fonte.zip") && h != null && h.EndsWith(".zip.sha256"));
            Check("Release sem hash e recusada", !Updater.ParseRelease("{\"tag_name\":\"v1.2.3\",\"assets\":[{\"browser_download_url\":\"https://x/PCTurbo-fonte.zip\"}]}", out v, out a, out h));
            Check("Resposta sem versao e recusada", !Updater.ParseRelease("{\"message\":\"Not Found\"}", out v, out a, out h));
            Check("Versao do app vale como numero", Updater.IsNewer("99.0.0") && !Updater.IsNewer("0.0.1"));
        }

        private static void Fps()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "pcturbo_selftest_" + DateTime.Now.Ticks + ".csv");
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Application,ProcessID,MsBetweenPresents");
                for (int i = 0; i < 990; i++) sb.AppendLine("game.exe,1,4.0");
                for (int i = 0; i < 10; i++) sb.AppendLine("game.exe,1,20.0");
                File.WriteAllText(tmp, sb.ToString());
                string err;
                FpsResult r = FpsTest.ParseCsv(tmp, "game.exe", 60, out err);
                Check("FPS: arquivo lido", r != null);
                if (r != null)
                {
                    Check("FPS medio (esperado 240.4)", Near(r.Avg, 240.38, 0.5));
                    Check("FPS 1% low (esperado 50)", Near(r.Low1, 50.0, 0.5));
                    Check("FPS 0,1% low (esperado 50)", Near(r.Low01, 50.0, 0.5));
                    Check("FPS: 1000 quadros", r.Frames == 1000);
                }

                StringBuilder sb2 = new StringBuilder();
                sb2.AppendLine("Application,FrameTime");
                for (int i = 0; i < 300; i++) sb2.AppendLine("game.exe,8.0");
                File.WriteAllText(tmp, sb2.ToString());
                FpsResult r2 = FpsTest.ParseCsv(tmp, "game.exe", 60, out err);
                Check("FPS: coluna FrameTime tambem funciona (125)", r2 != null && Near(r2.Avg, 125.0, 0.5));

                File.WriteAllText(tmp, "Application,MsBetweenPresents\ngame.exe,4.0\n");
                FpsResult r3 = FpsTest.ParseCsv(tmp, "game.exe", 60, out err);
                Check("FPS: poucos quadros e recusado", r3 == null && err != null);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        private static void Contas()
        {
            int b;
            string t = Report.Diff(100, 80, "", true, out b);
            Check("Diferenca: 100 para 80 melhorou", b == 1 && t.StartsWith("-20"));
            Report.Diff(100, 120, "", true, out b);
            Check("Diferenca: 100 para 120 piorou", b == -1);
            Report.Diff(50, 60, " FPS", false, out b);
            Check("Diferenca: FPS maior melhorou", b == 1);
            Report.Diff(5, 5, "", true, out b);
            Check("Diferenca: igual", b == 0);
        }

        private static void Vigia()
        {
            List<GuardItem> antes = new List<GuardItem>();
            antes.Add(G("Epic", "20.3.5.0"));
            antes.Add(G("Steam", "9.9.9.9"));

            List<GuardItem> igual = new List<GuardItem>();
            igual.Add(G("Epic", "20.3.5.0"));
            igual.Add(G("Steam", "9.9.9.9"));
            Check("Vigia: tudo igual sem aviso", Guard.Compare(antes, igual).Count == 0);

            List<GuardItem> velho = new List<GuardItem>();
            velho.Add(G("Epic", "20.3.3.0"));
            velho.Add(G("Steam", "9.9.9.9"));
            Check("Vigia: versao mais antiga avisa", Guard.Compare(antes, velho).Count == 1);

            List<GuardItem> novo = new List<GuardItem>();
            novo.Add(G("Epic", "21.0.0.0"));
            novo.Add(G("Steam", "9.9.9.9"));
            Check("Vigia: versao mais nova nao avisa", Guard.Compare(antes, novo).Count == 0);

            List<GuardItem> sumiu = new List<GuardItem>();
            sumiu.Add(G("Steam", "9.9.9.9"));
            Check("Vigia: programa sumido avisa", Guard.Compare(antes, sumiu).Count == 1);

            Check("Vigia: foto do PC nao quebra", Guard.Snapshot() != null);
            Check("Notebook: leitura nao quebra", Machine.IsLaptop() || true);
        }

        private static void Ajustes()
        {
            List<Tweak> all = Tweaks.All();
            Check("Ajustes: lista com pelo menos 15", all.Count >= 15);
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            bool dup = false;
            bool removeApps = false;
            foreach (Tweak t in all)
            {
                if (seen.ContainsKey(t.Name)) dup = true;
                seen[t.Name] = true;
                if (t.Name.ToLowerInvariant().Contains("remover")) removeApps = true;
            }
            Check("Ajustes: nomes sem repetir", !dup);
            Check("Ajustes: nenhum remove programas", !removeApps);

            bool plan = false;
            foreach (Tweak t in all)
            {
                if (t.Name.StartsWith("Plano de energia") && t.DesktopOnly) plan = true;
            }
            Check("Ajustes: plano de energia so em PC de mesa", plan);

            bool hasBin = false;
            foreach (CleanTarget c in Cleaner.Targets())
            {
                if (c.Name == "Lixeira") hasBin = true;
            }
            Check("Limpeza: lixeira na lista", hasBin);
        }

        private static void Imagem()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "pcturbo_selftest_" + DateTime.Now.Ticks + ".png");
            try
            {
                CardData d = new CardData();
                d.Hardware = "CPU de teste  •  GPU de teste  •  16 GB de RAM";
                d.Footer = "teste";
                for (int i = 0; i < 6; i++)
                {
                    CardTile t = new CardTile();
                    t.Label = "Bloco " + i;
                    t.Value = (i * 111) + " MB";
                    t.Sub = "antes: 1 (-1)";
                    t.Better = (i % 3) - 1;
                    d.Tiles.Add(t);
                }
                Card.Draw(d, tmp);
                Check("Cartao: imagem criada", File.Exists(tmp) && new FileInfo(tmp).Length > 5000);
                using (System.Drawing.Image img = System.Drawing.Image.FromFile(tmp))
                {
                    Check("Cartao: tamanho 1200x675", img.Width == Card.Width && img.Height == Card.Height);
                }
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
            Check("Hardware: nomes nao quebram", Report.Cpu() != null && Report.Gpu() != null);
        }

        private static void Config()
        {
            Check("Config: tira .exe e espacos", Settings.Clean("  Game.EXE ") == "Game");
            List<string> l = new List<string>();
            l.Add("Valorant");
            Check("Config: compara sem ligar para maiusculas", Settings.Has(l, "VALORANT") && !Settings.Has(l, "outro"));

            string f = "selftest_" + DateTime.Now.Ticks + ".txt";
            string[] def = { "A", "B.exe", "a" };
            List<string> r1 = Settings.ReadList(f, def);
            Check("Config: padrao sem repetir (A, B)", r1.Count == 2 && r1[1] == "B");
            Settings.WriteList(f, new List<string>());
            Check("Config: lista vazia salva nao volta ao padrao", Settings.ReadList(f, def).Count == 0);
            try { File.Delete(Path.Combine(Settings.Dir(), f)); } catch { }
            Check("Config: jogos padrao tem Valorant", Settings.Has(new List<string>(Settings.DefaultGames), "VALORANT-Win64-Shipping"));
        }

        private static void Protecao()
        {
            Check("Protegido: Valorant", Balancer.IsProtected("VALORANT-Win64-Shipping"));
            Check("Protegido: Vanguard", Balancer.IsProtected("vgc"));
            Check("Protegido: Epic", Balancer.IsProtected("EpicGamesLauncher"));
            Check("Protegido: Xbox", Balancer.IsProtected("XboxPcApp"));
            Check("Protegido: o proprio app", Balancer.IsProtected("PCTurbo"));
            Check("Nao protegido: navegador qualquer", !Balancer.IsProtected("chrome"));
        }
    }
}

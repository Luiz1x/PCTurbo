using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PCTurbo
{
    public static class Maint
    {
        private const string CleanTaskName = "PC Turbo Limpeza";

        private const string BgTaskName = "PC Turbo Segundo Plano";

        public static void ScheduleBackground(bool on)
        {
            if (on)
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                int rc = Proc.Run("schtasks.exe",
                    "/Create /TN \"" + BgTaskName + "\" /SC ONLOGON /RL HIGHEST /F /TR \"\\\"" + exe + "\\\" /fundo\"",
                    Log.Write);
                if (rc == 0)
                {
                    Log.Write("Limpeza em segundo plano ligada: abre sozinha quando você entra no Windows e fica na bandeja.");
                    try { System.Diagnostics.Process.Start(exe, "/fundo"); } catch { }
                }
                else Log.Write("Não consegui ligar a limpeza em segundo plano (código " + rc + ").");
            }
            else
            {
                int rc = Proc.Run("schtasks.exe", "/Delete /TN \"" + BgTaskName + "\" /F", Log.Write);
                if (rc == 0) Log.Write("Limpeza em segundo plano desligada. Se o ícone ainda estiver na bandeja, clique nele com o botão direito e escolha Sair.");
                else Log.Write("A limpeza em segundo plano não estava ligada.");
            }
        }

        public static void ScheduleClean(bool on)
        {
            if (on)
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                int rc = Proc.Run("schtasks.exe",
                    "/Create /TN \"" + CleanTaskName + "\" /SC WEEKLY /D SUN /ST 12:00 /RL HIGHEST /F /TR \"\\\"" + exe + "\\\" /limpar\"",
                    Log.Write);
                if (rc == 0) Log.Write("Limpeza automática ligada: todo domingo às 12h, se o PC estiver ligado. Só limpa lixo temporário.");
                else Log.Write("Não consegui ligar a limpeza automática (código " + rc + ").");
            }
            else
            {
                int rc = Proc.Run("schtasks.exe", "/Delete /TN \"" + CleanTaskName + "\" /F", Log.Write);
                if (rc == 0) Log.Write("Limpeza automática desligada.");
                else Log.Write("A limpeza automática não estava ligada.");
            }
        }

        private static void LogFiltered(string t)
        {
            // Ignora barras de progresso (so simbolos) e contadores "1 MB / 5 MB"
            if (!Regex.IsMatch(t, "[A-Za-z0-9]")) return;
            if (Regex.IsMatch(t, @"\d+\s*%")) return;
            if (Regex.IsMatch(t, @"\d+(\.\d+)?\s*(KB|MB|GB)\s*/")) return;
            Log.Write(t);
        }

        public static void RestorePoint(string description)
        {
            Log.Write("Criando ponto de restauração (o Windows limita a 1 por dia)...");
            try
            {
                Proc.PowerShell("Enable-ComputerRestore -Drive 'C:\\'; Checkpoint-Computer -Description '" +
                    description + "' -RestorePointType MODIFY_SETTINGS", LogFiltered);
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui criar o ponto de restauração: " + ex.Message);
            }
        }

        // Launchers de jogos que o winget NUNCA deve atualizar (eles se atualizam sozinhos;
        // o instalador do winget pode ser mais antigo e quebrar o launcher).
        private static readonly string[] WingetBlock =
        {
            "EpicGames.EpicGamesLauncher", "Valve.Steam", "Discord.Discord", "Blizzard.BattleNet",
            "Ubisoft.Connect", "ElectronicArts.EADesktop", "RockstarGames.Launcher", "GOG.Galaxy",
            "RiotGames.Valorant.BR", "RiotGames.LeagueOfLegends.BR", "RiotGames.Valorant.NA", "RiotGames.LeagueOfLegends.NA"
        };

        public static void Winget()
        {
            Log.Write("Atualizando programas com o winget (pode demorar). Launchers de jogos (Epic, Steam, Riot, Discord e outros) ficam de fora.");
            try
            {
                foreach (string id in WingetBlock)
                {
                    try
                    {
                        Proc.Run("cmd.exe", "/c winget pin add --id " + id + " --blocking --accept-source-agreements", null, Encoding.UTF8);
                    }
                    catch { }
                }
                int rc = Proc.Run("cmd.exe",
                    "/c winget upgrade --all --silent --accept-source-agreements --accept-package-agreements --disable-interactivity",
                    LogFiltered, Encoding.UTF8);
                Log.Write("winget terminou (código " + rc + ").");
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui rodar o winget: " + ex.Message);
            }
        }

        public static void Dism()
        {
            Log.Write("Limpando componentes antigos do Windows (alguns minutos)...");
            try
            {
                int rc = Proc.Run("Dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", LogFiltered);
                Log.Write("DISM terminou (código " + rc + ").");
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui rodar o DISM: " + ex.Message);
            }
        }

        public static void Sfc()
        {
            Log.Write("Verificando arquivos do sistema (pode levar 10 a 20 minutos)...");
            try
            {
                int rc = Proc.Run("sfc.exe", "/scannow", LogFiltered, Encoding.Unicode);
                Log.Write("SFC terminou (código " + rc + ").");
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui rodar o SFC: " + ex.Message);
            }
        }

        public static void Trim()
        {
            Log.Write("Otimizando o SSD (TRIM)...");
            try
            {
                Proc.PowerShell("Optimize-Volume -DriveLetter C -ReTrim -Verbose 4>&1 | Out-String -Stream | Where-Object { $_.Trim().Length -gt 0 }", LogFiltered);
                Log.Write("TRIM concluído.");
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui otimizar o SSD: " + ex.Message);
            }
        }

        public static void FlushDns()
        {
            try
            {
                Proc.Run("ipconfig.exe", "/flushdns", LogFiltered);
            }
            catch (Exception ex)
            {
                Log.Write("Erro: " + ex.Message);
            }
        }
    }

    public class MaintTab : UserControl
    {
        public MaintTab()
        {
            this.Dock = DockStyle.Fill;

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.Padding = new Padding(12);

            AddAction(flow, "Atualizar programas",
                "Usa o winget para atualizar programas (navegador, .NET etc.). Launchers de jogos como Epic, Steam, Riot e Discord NUNCA são atualizados por aqui, porque se atualizam sozinhos.",
                delegate { Ui.Background("Atualizar programas (winget)", delegate { Maint.Winget(); }); });

            AddAction(flow, "Limpar componentes do Windows (DISM)",
                "Remove versões antigas de componentes do Windows e libera espaço. Demora alguns minutos.",
                delegate { Ui.Background("Limpeza de componentes (DISM)", delegate { Maint.Dism(); }); });

            AddAction(flow, "Verificar arquivos do sistema (SFC)",
                "Procura e repara arquivos do Windows corrompidos. Pode levar de 10 a 20 minutos.",
                delegate { Ui.Background("Verificação do sistema (SFC)", delegate { Maint.Sfc(); }); });

            AddAction(flow, "Ligar limpeza em segundo plano",
                "Fica na bandeja (perto do relógio). Esvazia o lixo temporário e a Lixeira sozinha quando passam de um limite (3 GB de temporários, 2 GB na Lixeira) ou quando o disco C: está quase cheio. Também equilibra o processador: se um programa em segundo plano pesar demais, baixa a prioridade dele e devolve depois. Nunca limpa com jogo ou tela cheia aberta. Não apaga programas nem seus arquivos.",
                delegate { Ui.Background("Ligar limpeza em segundo plano", delegate { Maint.ScheduleBackground(true); }); });

            AddAction(flow, "Desligar limpeza em segundo plano",
                "Para de abrir junto com o Windows. Se o ícone estiver na bandeja, clique com o botão direito nele e escolha Sair.",
                delegate { Ui.Background("Desligar limpeza em segundo plano", delegate { Maint.ScheduleBackground(false); }); });

            AddAction(flow, "Ligar limpeza automática semanal",
                "Todo domingo às 12h (se o PC estiver ligado) faz a limpeza e esvazia a Lixeira, sem abrir janela. Não apaga programas nem seus arquivos.",
                delegate { Ui.Background("Ligar limpeza automática", delegate { Maint.ScheduleClean(true); }); });

            AddAction(flow, "Desligar limpeza automática",
                "Remove a tarefa agendada de limpeza semanal.",
                delegate { Ui.Background("Desligar limpeza automática", delegate { Maint.ScheduleClean(false); }); });

            AddAction(flow, "Reiniciar o PC agora",
                "Reinicia em 15 segundos. Salve o que estiver aberto antes.",
                delegate { Reboot(); });

            this.Controls.Add(flow);
        }

        private void AddAction(FlowLayoutPanel host, string title, string desc, EventHandler click)
        {
            Panel row = new Panel();
            row.Width = 960;
            row.Height = 50;

            Button b = Ui.Btn(title, 300, click);
            b.Location = new Point(0, 6);

            Label l = new Label();
            l.Text = desc;
            l.Location = new Point(316, 6);
            l.Size = new Size(630, 38);
            l.TextAlign = ContentAlignment.MiddleLeft;

            row.Controls.Add(b);
            row.Controls.Add(l);
            host.Controls.Add(row);
        }

        private void Open(string target)
        {
            try { System.Diagnostics.Process.Start(target); }
            catch (Exception ex) { Log.Write("Não consegui abrir: " + ex.Message); }
        }

        private void Reboot()
        {
            DialogResult r = MessageBox.Show("Reiniciar o PC agora? Salve tudo o que estiver aberto.",
                "Reiniciar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            try
            {
                Proc.Run("shutdown.exe", "/r /t 15 /c \"PC Turbo: reiniciando para aplicar as mudanças\"", null);
                Log.Write("Reiniciando em 15 segundos. Para cancelar: abra o CMD e digite shutdown /a");
            }
            catch (Exception ex)
            {
                Log.Write("Não consegui reiniciar: " + ex.Message);
            }
        }
    }
}

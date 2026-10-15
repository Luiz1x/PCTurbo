using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace PCTurbo
{
    // Atualizacao dentro do app: baixa a versao nova do GitHub, troca os arquivos e recompila.
    public static class Updater
    {
        public const string Current = "1.4.0";
        private const string RawBase = "https://raw.githubusercontent.com/Luiz1x/PCTurbo/main/";
        private const string ZipUrl = "https://github.com/Luiz1x/PCTurbo/archive/refs/heads/main.zip";

        public static string AppDir()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static WebClient NewClient()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
            WebClient wc = new WebClient();
            wc.Headers.Add("User-Agent", "PCTurbo");
            wc.Encoding = Encoding.UTF8;
            return wc;
        }

        // Devolve a versao mais nova publicada, ou null se nao deu pra checar.
        public static string LatestVersion()
        {
            try
            {
                using (WebClient wc = NewClient())
                {
                    string v = wc.DownloadString(RawBase + "version.txt?t=" + DateTime.Now.Ticks).Trim();
                    System.Version tmp;
                    if (!System.Version.TryParse(v, out tmp)) return null;
                    return v;
                }
            }
            catch { return null; }
        }

        public static bool IsNewer(string latest)
        {
            if (latest == null) return false;
            try { return new System.Version(latest) > new System.Version(Current); }
            catch { return false; }
        }

        // Baixa e troca os arquivos. Devolve null se deu certo, ou a mensagem de erro.
        public static string DownloadAndReplace()
        {
            string dir = AppDir();
            string tmp = Path.Combine(Path.GetTempPath(), "PCTurbo_upd_" + DateTime.Now.Ticks);
            string zip = tmp + ".zip";
            try
            {
                Log.Write("Baixando a versão nova...");
                using (WebClient wc = NewClient())
                {
                    wc.DownloadFile(ZipUrl + "?t=" + DateTime.Now.Ticks, zip);
                }
                if (!File.Exists(zip) || new FileInfo(zip).Length < 1000) return "O download veio vazio.";

                Directory.CreateDirectory(tmp);
                string script =
                    "$ErrorActionPreference='Stop';" +
                    "Expand-Archive -LiteralPath '" + zip.Replace("'", "''") + "' -DestinationPath '" + tmp.Replace("'", "''") + "' -Force;" +
                    "$root=(Get-ChildItem -LiteralPath '" + tmp.Replace("'", "''") + "' -Directory | Select-Object -First 1).FullName;" +
                    "if(-not (Test-Path (Join-Path $root 'src\\Util.cs'))){ throw 'Pacote sem a pasta src' };" +
                    "$dst='" + dir.TrimEnd('\\').Replace("'", "''") + "';" +
                    "if(Test-Path (Join-Path $dst 'PCTurbo.exe')){ Copy-Item (Join-Path $dst 'PCTurbo.exe') (Join-Path $dst 'PCTurbo.exe.bak') -Force };" +
                    "if(Test-Path (Join-Path $dst 'src')){ Get-ChildItem (Join-Path $dst 'src') -Filter *.cs | Remove-Item -Force };" +
                    "New-Item -ItemType Directory -Force -Path (Join-Path $dst 'src') | Out-Null;" +
                    "Copy-Item (Join-Path $root 'src\\*.cs') (Join-Path $dst 'src') -Force;" +
                    "foreach($f in 'COMPILAR.bat','INICIAR.bat','CORRIGIR_AVISO_CONTROLE.bat','LEIA-ME.txt','app.manifest','icone.ico','version.txt'){ " +
                    "if(Test-Path (Join-Path $root $f)){ Copy-Item (Join-Path $root $f) (Join-Path $dst $f) -Force } };" +
                    "Get-ChildItem (Join-Path $dst 'src') -Filter *.cs | ForEach-Object { $_.LastWriteTime = Get-Date };" +
                    "Write-Output 'ok'";
                string err = null;
                bool ok = false;
                Proc.PowerShell(script, delegate(string line)
                {
                    if (line.Trim() == "ok") ok = true;
                    else if (line.Trim().Length > 0) err = line.Trim();
                });
                if (!ok) return err ?? "Não consegui copiar os arquivos novos.";
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                try { if (File.Exists(zip)) File.Delete(zip); } catch { }
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            }
        }

        // Fecha o app, recompila e abre de novo (feito por um .bat auxiliar).
        public static void RestartAndRecompile()
        {
            string dir = AppDir().TrimEnd('\\');
            string bat = Path.Combine(Path.GetTempPath(), "pcturbo_atualizar.bat");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("title Atualizando o PC Turbo");
            sb.AppendLine("ping 127.0.0.1 -n 3 >nul");
            sb.AppendLine("cd /d \"" + dir + "\"");
            sb.AppendLine("call COMPILAR.bat auto");
            sb.AppendLine("if not exist PCTurbo.exe if exist PCTurbo.exe.bak copy /y PCTurbo.exe.bak PCTurbo.exe >nul");
            sb.AppendLine("start \"\" \"" + dir + "\\PCTurbo.exe\"");
            File.WriteAllText(bat, sb.ToString(), Encoding.Default);

            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
            psi.UseShellExecute = true;
            psi.WorkingDirectory = dir;
            Process.Start(psi);
            Application.Exit();
        }
    }
}

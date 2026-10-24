using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PCTurbo
{
    // Atualizacao dentro do app: baixa a versao nova do GitHub, troca os arquivos e recompila.
    public static class Updater
    {
        public const string Current = "2.1.0";
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

        // Pacote da Release que o GitHub compilou e conferiu (null = nao achou Release)
        private static string assetUrl;
        private static string shaUrl;

        // Devolve a versao mais nova publicada, ou null se nao deu pra checar.
        // Primeiro olha a ultima Release: ela so existe se o GitHub conseguiu compilar o codigo.
        // So se o repositorio ainda nao tiver nenhuma Release usa o jeito antigo (arquivo version.txt).
        public static string LatestVersion()
        {
            assetUrl = null;
            shaUrl = null;
            bool notFound;
            string r = ReadRelease(out notFound);
            if (r != null) return r;
            if (!notFound) return null;

            string v = ReadVersion("https://api.github.com/repos/Luiz1x/PCTurbo/contents/version.txt?ref=main&t=" + DateTime.Now.Ticks, true);
            if (v == null) v = ReadVersion(RawBase + "version.txt?t=" + DateTime.Now.Ticks, false);
            return v;
        }

        private static string ReadRelease(out bool notFound)
        {
            notFound = false;
            try
            {
                using (WebClient wc = NewClient())
                {
                    wc.Headers.Add("Accept", "application/vnd.github+json");
                    wc.Headers.Add("Cache-Control", "no-cache");
                    string json = wc.DownloadString("https://api.github.com/repos/Luiz1x/PCTurbo/releases/latest?t=" + DateTime.Now.Ticks);
                    string v, a, h;
                    if (!ParseRelease(json, out v, out a, out h)) return null;
                    assetUrl = a;
                    shaUrl = h;
                    return v;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse resp = ex.Response as HttpWebResponse;
                if (resp != null && resp.StatusCode == HttpStatusCode.NotFound) notFound = true;
                return null;
            }
            catch { return null; }
        }

        // Le a resposta da API do GitHub (separado para poder ser testado no autoteste)
        internal static bool ParseRelease(string json, out string ver, out string asset, out string sha)
        {
            ver = null;
            asset = null;
            sha = null;
            Match m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9.]+)\"");
            if (!m.Success) return false;
            System.Version tmp;
            if (!System.Version.TryParse(m.Groups[1].Value, out tmp)) return false;
            Match a = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*PCTurbo-fonte\\.zip)\"");
            Match h = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*PCTurbo-fonte\\.zip\\.sha256)\"");
            if (!a.Success || !h.Success) return false;
            ver = m.Groups[1].Value;
            asset = a.Groups[1].Value;
            sha = h.Groups[1].Value;
            return true;
        }

        private static string Sha256(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] b = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string ReadVersion(string url, bool api)
        {
            try
            {
                using (WebClient wc = NewClient())
                {
                    if (api) wc.Headers.Add("Accept", "application/vnd.github.v3.raw");
                    wc.Headers.Add("Cache-Control", "no-cache");
                    string v = wc.DownloadString(url).Trim();
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
                if (assetUrl == null) LatestVersion();
                Log.Write("Baixando a versão nova...");
                using (WebClient wc = NewClient())
                {
                    wc.DownloadFile(assetUrl != null ? assetUrl : ZipUrl + "?t=" + DateTime.Now.Ticks, zip);
                }
                if (!File.Exists(zip) || new FileInfo(zip).Length < 1000) return "O download veio vazio.";

                if (assetUrl != null && shaUrl != null)
                {
                    string want;
                    using (WebClient wc2 = NewClient())
                    {
                        want = wc2.DownloadString(shaUrl).Trim().ToLowerInvariant();
                    }
                    string got = Sha256(zip);
                    if (want.Length != 64 || got != want)
                    {
                        return "O arquivo baixado não bate com o SHA-256 publicado (corrompido ou alterado). Não instalei nada.";
                    }
                    Log.Write("Pacote conferido: o SHA-256 bate e a versão foi compilada pelo GitHub.");
                }
                else
                {
                    Log.Write("Aviso: ainda não existe Release, então baixei direto do repositório (sem conferência).");
                }

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
                    "foreach($f in 'COMPILAR.bat','INICIAR.bat','LEIA-ME.txt','app.manifest','icone.ico','version.txt'){ " +
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
            sb.AppendLine("schtasks /Query /TN \"PC Turbo Segundo Plano\" >nul 2>&1");
            sb.AppendLine("if not errorlevel 1 start \"\" \"" + dir + "\\PCTurbo.exe\" /fundo");
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

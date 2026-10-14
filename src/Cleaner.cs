using System;
using System.Collections.Generic;
using System.IO;

namespace PCTurbo
{
    public class CleanTarget
    {
        public string Name;
        public string Desc;
        public bool Default;
        public Func<long> Size;
        public Func<long> Clean;
    }

    public static class Cleaner
    {
        private static CleanTarget Folders(string name, string desc, bool def, params string[] paths)
        {
            CleanTarget t = new CleanTarget();
            t.Name = name;
            t.Desc = desc;
            t.Default = def;
            t.Size = delegate
            {
                long s = 0;
                foreach (string p in paths) s += Dirs.Size(p);
                return s;
            };
            t.Clean = delegate
            {
                long s = 0;
                foreach (string p in paths) s += Dirs.DeleteContents(p);
                return s;
            };
            return t;
        }

        public static List<CleanTarget> Targets()
        {
            List<CleanTarget> list = new List<CleanTarget>();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string temp = Path.GetTempPath();

            list.Add(Folders("Temporários do usuário",
                "Arquivos temporários que os programas deixam para trás.",
                true, temp));

            list.Add(Folders("Temporários do Windows",
                "Pasta Temp do próprio Windows.",
                true, Path.Combine(win, "Temp")));

            // Cache do Windows Update (precisa parar os servicos antes)
            CleanTarget wu = new CleanTarget();
            wu.Name = "Cache de downloads do Windows Update";
            wu.Desc = "Atualizações já baixadas. O Windows baixa de novo só se precisar.";
            wu.Default = true;
            string wuPath = Path.Combine(win, @"SoftwareDistribution\Download");
            wu.Size = delegate { return Dirs.Size(wuPath); };
            wu.Clean = delegate
            {
                Proc.Run("net.exe", "stop wuauserv", null);
                Proc.Run("net.exe", "stop bits", null);
                long freed = Dirs.DeleteContents(wuPath);
                Proc.Run("net.exe", "start bits", null);
                Proc.Run("net.exe", "start wuauserv", null);
                return freed;
            };
            list.Add(wu);

            list.Add(Folders("Relatórios de erro do Windows",
                "Relatórios de travamento guardados pelo Windows.",
                true,
                Path.Combine(common, @"Microsoft\Windows\WER\ReportArchive"),
                Path.Combine(common, @"Microsoft\Windows\WER\ReportQueue")));

            list.Add(Folders("Despejos de falha (dumps)",
                "Arquivos gerados quando um programa ou o Windows trava.",
                true,
                Path.Combine(local, "CrashDumps"),
                Path.Combine(win, "Minidump"),
                Path.Combine(win, "MEMORY.DMP")));

            list.Add(Folders("Cache de otimização de entrega",
                "Cache que o Windows usa para compartilhar atualizações.",
                true,
                Path.Combine(win, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache")));

            list.Add(Folders("Cache de shaders do DirectX",
                "Jogos recriam isso na primeira abertura (pode dar micro-travadas no começo).",
                false,
                Path.Combine(local, "D3DSCache")));

            list.Add(Folders("Cache de shaders da NVIDIA",
                "Resolve travadas depois de atualizar o driver. Os jogos recriam na primeira abertura.",
                false,
                Path.Combine(local, @"NVIDIA\DXCache"), Path.Combine(local, @"NVIDIA\GLCache")));

            list.Add(Folders("Instaladores antigos de driver NVIDIA (C:\\NVIDIA)",
                "Pasta que o instalador da NVIDIA deixa para trás depois de instalar o driver.",
                true,
                Path.Combine(Path.GetPathRoot(win), "NVIDIA")));

            list.Add(Folders("Cache de miniaturas e arquivos temporários de internet",
                "O Windows recria as miniaturas sozinho quando precisar.",
                true,
                Path.Combine(local, @"Microsoft\Windows\INetCache"),
                Path.Combine(local, @"Microsoft\Windows\Explorer")));

            list.Add(Folders("Cache do Discord",
                "Feche o Discord antes. Não apaga conversas nem login.",
                false,
                Path.Combine(roaming, @"discord\Cache"),
                Path.Combine(roaming, @"discord\Code Cache"),
                Path.Combine(roaming, @"discord\GPUCache")));

            list.Add(Folders("Cache de páginas da Steam (htmlcache)",
                "Feche a Steam antes. Não apaga jogos nem login.",
                false,
                Path.Combine(local, @"Steam\htmlcache")));

            list.Add(Folders("Cache do Google Chrome",
                "Feche o Chrome antes. Não apaga senhas nem histórico.",
                false,
                Path.Combine(local, @"Google\Chrome\User Data\Default\Cache"),
                Path.Combine(local, @"Google\Chrome\User Data\Default\Code Cache"),
                Path.Combine(local, @"Google\Chrome\User Data\Default\GPUCache")));

            list.Add(Folders("Cache do Microsoft Edge",
                "Feche o Edge antes. Não apaga senhas nem histórico.",
                false,
                Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache"),
                Path.Combine(local, @"Microsoft\Edge\User Data\Default\Code Cache"),
                Path.Combine(local, @"Microsoft\Edge\User Data\Default\GPUCache")));

            // Lixeira
            CleanTarget rb = new CleanTarget();
            rb.Name = "Lixeira";
            rb.Desc = "Esvazia a Lixeira de todos os discos. Não dá para desfazer.";
            rb.Default = false;
            rb.Size = delegate
            {
                long t = 0;
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.DriveType != DriveType.Fixed) continue;
                        t += Dirs.Size(Path.Combine(d.RootDirectory.FullName, "$Recycle.Bin"));
                    }
                    catch { }
                }
                return t;
            };
            rb.Clean = delegate
            {
                long before = rb.Size();
                try { Native.SHEmptyRecycleBin(IntPtr.Zero, null, 7); } catch { }
                return before;
            };
            list.Add(rb);

            return list;
        }
    }
}

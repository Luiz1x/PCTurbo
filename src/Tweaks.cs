using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace PCTurbo
{
    public class Tweak
    {
        public string Name;
        public string Desc;
        public bool Default;
        public Action Apply;
        public Action Revert;   // null = nao ha o que reverter
    }

    public static class Tweaks
    {
        private const string PlanGuid = "99999999-9999-9999-9999-999999999999";

        private static readonly string[] Svcs =
        {
            "Fax", "MapsBroker", "RemoteRegistry", "RetailDemo",
            "DiagTrack", "dmwappushservice", "WMPNetworkSvc"
        };

        private static readonly string[] TaskNames =
        {
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"
        };

        private static readonly string[] CdmNames =
        {
            "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled",
            "SubscribedContent-338389Enabled", "SubscribedContent-353694Enabled",
            "SubscribedContent-353696Enabled", "SubscribedContent-310093Enabled",
            "SystemPaneSuggestionsEnabled", "SoftLandingEnabled",
            "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled", "SilentInstalledAppsEnabled"
        };

        private static readonly string[] BloatApps =
        {
            "Microsoft.SkypeApp", "Microsoft.YourPhone", "Clipchamp.Clipchamp",
            "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.MixedReality.Portal",
            "Microsoft.GetHelp", "Microsoft.Getstarted", "Microsoft.Microsoft3DViewer",
            "Microsoft.MicrosoftOfficeHub", "Microsoft.MicrosoftSolitaireCollection",
            "Microsoft.People", "Microsoft.PowerAutomateDesktop", "Microsoft.Todos",
            "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps",
            "Microsoft.549981C3F5F10",
            "Microsoft.Windows.DevHome", "MicrosoftTeams", "MSTeams",
            "king.com.CandyCrushSaga", "king.com.CandyCrushSodaSaga"
        };

        private static Tweak T(string name, string desc, bool def, Action apply, Action revert)
        {
            Tweak t = new Tweak();
            t.Name = name;
            t.Desc = desc;
            t.Default = def;
            t.Apply = apply;
            t.Revert = revert;
            return t;
        }

        // Aplica uma chave sem derrubar o ajuste inteiro se o Windows negar acesso
        private static void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception ex) { Log.Write("   aviso: " + what + " nao aplicado (" + ex.Message + ")"); }
        }

        private static void SetGpuPref(string key, string val)
        {
            const string path = @"Software\Microsoft\DirectX\UserGpuPreferences";
            string cur = "";
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(path))
            {
                if (k != null) cur = R.Str(k, "DirectXUserGlobalSettings");
            }
            List<string> parts = new List<string>();
            foreach (string p in cur.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!p.StartsWith(key + "=")) parts.Add(p);
            }
            if (val != null) parts.Add(key + "=" + val);
            string res = string.Join(";", parts.ToArray());
            if (res.Length > 0) res += ";";
            if (res.Length == 0) R.Del(Registry.CurrentUser, path, "DirectXUserGlobalSettings");
            else R.SetString(Registry.CurrentUser, path, "DirectXUserGlobalSettings", res);
        }

        public static List<Tweak> All()
        {
            RegistryKey cu = Registry.CurrentUser;
            RegistryKey lm = Registry.LocalMachine;
            List<Tweak> list = new List<Tweak>();

            // 1. Plano de energia
            list.Add(T("Plano de energia: Desempenho Máximo",
                "O processador não reduz a velocidade para economizar energia. Ideal para PC de mesa.",
                true,
                delegate
                {
                    Proc.Run("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 " + PlanGuid, null);
                    int rc = Proc.Run("powercfg.exe", "-setactive " + PlanGuid, null);
                    if (rc != 0) Proc.Run("powercfg.exe", "-setactive SCHEME_MIN", null);
                },
                delegate { Proc.Run("powercfg.exe", "-setactive SCHEME_BALANCED", null); }));

            // 2. USB e PCIe
            list.Add(T("USB e PCIe sem economia de energia",
                "Evita mouse/teclado \"dormindo\" e a placa de vídeo trocando de estado de energia.",
                true,
                delegate
                {
                    Proc.Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0", null);
                    Proc.Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0", null);
                    Proc.Run("powercfg.exe", "/setactive SCHEME_CURRENT", null);
                },
                delegate
                {
                    Proc.Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1", null);
                    Proc.Run("powercfg.exe", "/setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 1", null);
                    Proc.Run("powercfg.exe", "/setactive SCHEME_CURRENT", null);
                }));

            // 3. Modo de Jogo
            list.Add(T("Modo de Jogo ligado",
                "O Windows dá prioridade ao jogo e evita atualizações no meio da partida.",
                true,
                delegate
                {
                    R.SetDword(cu, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                    R.SetDword(cu, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
                },
                null));

            // 4. Game Bar / gravacao
            list.Add(T("Desligar Game Bar e gravação em segundo plano",
                "Tira o overlay do Xbox (Win+G) e a gravação automática. O app Xbox e o Game Pass continuam funcionando.",
                true,
                delegate
                {
                    R.SetDword(cu, @"System\GameConfigStore", "GameDVR_Enabled", 0);
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                    R.SetDword(lm, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
                    R.SetDword(cu, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0);
                    R.SetDword(cu, @"Software\Microsoft\GameBar", "ShowStartupPanel", 0);
                },
                delegate
                {
                    R.SetDword(cu, @"System\GameConfigStore", "GameDVR_Enabled", 1);
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1);
                    R.Del(lm, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR");
                    R.SetDword(cu, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 1);
                    R.SetDword(cu, @"Software\Microsoft\GameBar", "ShowStartupPanel", 1);
                }));

            // 5. HAGS
            list.Add(T("Agendamento de GPU acelerado por hardware",
                "A placa de vídeo gerencia a própria memória. Vale depois de reiniciar.",
                true,
                delegate { R.SetDword(lm, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2); },
                delegate { R.SetDword(lm, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1); }));

            // 5b. Otimizacoes para jogos em janela
            list.Add(T("Otimizações para jogos em janela",
                "Jogos em tela cheia sem bordas/janela usam o modo de apresentação mais rápido (menos latência).",
                true,
                delegate { SetGpuPref("SwapEffectUpgradeEnable", "1"); },
                delegate { SetGpuPref("SwapEffectUpgradeEnable", null); }));

            // 5c. Integridade de memoria (VBS/HVCI) - opcional
            list.Add(T("Desligar Integridade de memória (VBS)",
                "Dá alguns % de FPS em jogos, mas tira uma camada de proteção do Windows contra drivers maliciosos. Opcional: só marque se aceitar o risco. Vale após reiniciar.",
                false,
                delegate { R.SetDword(lm, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0); },
                delegate { R.SetDword(lm, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 1); }));

            // 5d. VRR em janela
            list.Add(T("Taxa de atualização variável (G-SYNC) em jogos em janela",
                "Liga o VRR do Windows para jogos DX11 em janela/sem bordas. Não faz mal se o monitor não suportar.",
                true,
                delegate { SetGpuPref("VRROptimizeEnable", "1"); },
                delegate { SetGpuPref("VRROptimizeEnable", null); }));

            // 5e. Edge
            list.Add(T("Edge sem inicialização antecipada e sem segundo plano",
                "O Edge para de ficar carregado na memória o tempo todo. Ele continua funcionando normalmente quando você abrir.",
                true,
                delegate
                {
                    R.SetDword(lm, @"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0);
                    R.SetDword(lm, @"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0);
                },
                delegate
                {
                    R.Del(lm, @"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled");
                    R.Del(lm, @"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled");
                }));

            // 5f. Widgets, Copilot, Recall, busca web
            list.Add(T("Desligar Widgets, Copilot, Recall e busca na web do Iniciar",
                "Menos processos de fundo e menos RAM. A pesquisa do Windows continua achando seus arquivos e apps.",
                true,
                delegate
                {
                    Safe("Widgets", delegate { R.SetDword(lm, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0); });
                    Safe("Copilot", delegate { R.SetDword(cu, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1); });
                    Safe("Busca na web", delegate { R.SetDword(cu, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1); });
                    Safe("Recall", delegate { R.SetDword(lm, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1); });
                    Safe("Botao de Widgets", delegate { R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0); });
                },
                delegate
                {
                    Safe("Widgets", delegate { R.Del(lm, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests"); });
                    Safe("Copilot", delegate { R.Del(cu, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot"); });
                    Safe("Busca na web", delegate { R.Del(cu, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions"); });
                    Safe("Recall", delegate { R.Del(lm, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis"); });
                    Safe("Botao de Widgets", delegate { R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 1); });
                }));

            // 5g. Teclas de aderencia
            list.Add(T("Sem aviso de Teclas de Aderência (Shift 5 vezes)",
                "Evita aquela janelinha que minimiza o jogo quando você aperta Shift várias vezes seguidas.",
                true,
                delegate
                {
                    R.SetString(cu, @"Control Panel\Accessibility\StickyKeys", "Flags", "506");
                    R.SetString(cu, @"Control Panel\Accessibility\ToggleKeys", "Flags", "58");
                    R.SetString(cu, @"Control Panel\Accessibility\Keyboard Response", "Flags", "122");
                },
                delegate
                {
                    R.SetString(cu, @"Control Panel\Accessibility\StickyKeys", "Flags", "510");
                    R.SetString(cu, @"Control Panel\Accessibility\ToggleKeys", "Flags", "62");
                    R.SetString(cu, @"Control Panel\Accessibility\Keyboard Response", "Flags", "126");
                }));

            // 5h. Atraso de inicializacao do Explorer
            list.Add(T("Programas de inicialização abrem sem atraso",
                "O Windows espera alguns segundos antes de abrir os programas do início. Isso tira a espera.",
                true,
                delegate
                {
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0);
                },
                delegate
                {
                    R.Del(cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec");
                }));

            // 5i. Apps em segundo plano (desmarcado: pode afetar notificacoes de apps da Store)
            list.Add(T("Impedir apps da Microsoft Store de rodar em segundo plano",
                "Economiza RAM e CPU. DESMARCADO por padrão: pode atrasar notificações de apps da Store (o Game Pass continua funcionando).",
                false,
                delegate
                {
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1);
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0);
                },
                delegate
                {
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0);
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 1);
                }));

            // 5j. Aviso "procurar um app na Microsoft Store" ao usar o controle
            list.Add(T("Evitar o aviso \"procurar app na Microsoft Store\" do controle",
                "Se o Game Bar não está instalado, o botão Xbox do controle e alguns jogos (GTA, Rocket League) mostram esse aviso. Isso deixa o Windows quieto, sem instalar nada.",
                true,
                delegate
                {
                    foreach (string proto in new string[] { "ms-gamingoverlay", "ms-gamebar" })
                    {
                        string path = @"SOFTWARE\Classes\" + proto;
                        R.SetString(lm, path, "", "URL:" + proto);
                        R.SetString(lm, path, "URL Protocol", "");
                    }
                },
                delegate
                {
                    foreach (string proto in new string[] { "ms-gamingoverlay", "ms-gamebar" })
                    {
                        try { lm.DeleteSubKeyTree(@"SOFTWARE\Classes\" + proto, false); } catch { }
                    }
                }));

            // 6. Mouse
            list.Add(T("Mouse sem aceleração",
                "Desliga \"Aprimorar precisão do ponteiro\". Padrão de quem joga FPS. Vale após reiniciar.",
                true,
                delegate
                {
                    R.SetString(cu, @"Control Panel\Mouse", "MouseSpeed", "0");
                    R.SetString(cu, @"Control Panel\Mouse", "MouseThreshold1", "0");
                    R.SetString(cu, @"Control Panel\Mouse", "MouseThreshold2", "0");
                },
                delegate
                {
                    R.SetString(cu, @"Control Panel\Mouse", "MouseSpeed", "1");
                    R.SetString(cu, @"Control Panel\Mouse", "MouseThreshold1", "6");
                    R.SetString(cu, @"Control Panel\Mouse", "MouseThreshold2", "10");
                }));

            // 7. Visual leve (desmarcado por padrao: tira a transparencia da barra)
            list.Add(T("Menos animações e sem transparência",
                "Deixa o Windows mais seco. DESMARCADO por padrão: tira a transparência da barra de tarefas.",
                false,
                delegate
                {
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3);
                    R.Set(cu, @"Control Panel\Desktop", "UserPreferencesMask",
                        new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                    R.SetString(cu, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0);
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
                },
                delegate
                {
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0);
                    R.Set(cu, @"Control Panel\Desktop", "UserPreferencesMask",
                        new byte[] { 0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                    R.SetString(cu, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "1");
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 1);
                    R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1);
                }));

            // 8. Propagandas
            list.Add(T("Desligar sugestões e propagandas do Windows",
                "Tira dicas, apps sugeridos e anúncios do Menu Iniciar e das Configurações.",
                true,
                delegate
                {
                    foreach (string n in CdmNames)
                        R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", n, 0);
                },
                delegate
                {
                    foreach (string n in CdmNames)
                        R.SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", n, 1);
                }));

            // 9. Telemetria
            list.Add(T("Telemetria mínima e atualizações sem compartilhar sua internet",
                "Reduz o envio de dados à Microsoft e impede o Windows de usar sua internet para outros PCs.",
                true,
                delegate
                {
                    R.SetDword(lm, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0);
                    R.SetDword(lm, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0);
                },
                delegate
                {
                    R.Del(lm, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");
                    R.Del(lm, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode");
                }));

            // 10. Servicos
            list.Add(T("Desativar serviços inúteis",
                "Fax, Mapas offline, Registro Remoto, Modo Demo, Telemetria e Windows Media Player em rede.",
                true,
                delegate
                {
                    foreach (string s in Svcs)
                    {
                        Proc.Run("sc.exe", "config " + s + " start= disabled", null);
                        Proc.Run("sc.exe", "stop " + s, null);
                    }
                },
                delegate
                {
                    foreach (string s in Svcs)
                    {
                        string mode = "demand";
                        if (s == "DiagTrack") mode = "auto";
                        if (s == "MapsBroker") mode = "delayed-auto";
                        Proc.Run("sc.exe", "config " + s + " start= " + mode, null);
                    }
                }));

            // 11. Tarefas agendadas
            list.Add(T("Desativar tarefas de telemetria agendadas",
                "Tarefas que coletam dados de uso e rodam em segundo plano.",
                true,
                delegate
                {
                    foreach (string t in TaskNames)
                        Proc.Run("schtasks.exe", "/Change /TN \"" + t + "\" /Disable", null);
                },
                delegate
                {
                    foreach (string t in TaskNames)
                        Proc.Run("schtasks.exe", "/Change /TN \"" + t + "\" /Enable", null);
                }));

            // 12. Hibernacao
            list.Add(T("Desligar a hibernação",
                "Libera vários GB no disco (o arquivo de hibernação é grande com 32 GB de RAM) e desliga o Início Rápido.",
                true,
                delegate { Proc.Run("powercfg.exe", "-h off", null); },
                delegate { Proc.Run("powercfg.exe", "-h on", null); }));

            // 13. Apps inuteis
            list.Add(T("Remover apps inúteis da Microsoft",
                "Skype, Phone Link, Solitaire, Notícias, Clima, Mapas, Teams e outros. DESMARCADO por padrão: só marque se quiser. O Xbox, o Game Bar e o Game Pass nunca são removidos.",
                false,
                delegate
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("$apps=@(");
                    for (int i = 0; i < BloatApps.Length; i++)
                    {
                        sb.Append("'" + BloatApps[i] + "'");
                        if (i < BloatApps.Length - 1) sb.Append(",");
                    }
                    sb.Append(");");
                    sb.Append("foreach($a in $apps){");
                    sb.Append("$pk=@(Get-AppxPackage -Name $a -AllUsers); $pv=@(Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $a });");
                    sb.Append("if($pk.Count -eq 0 -and $pv.Count -eq 0){ Write-Output ('Nao estava instalado: ' + $a); continue };");
                    sb.Append("foreach($p in $pk){ try { Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction Stop; Write-Output ('Removido: ' + $a) } catch { Write-Output ('Nao consegui remover: ' + $a) } };");
                    sb.Append("foreach($p in $pv){ try { Remove-AppxProvisionedPackage -Online -PackageName $p.PackageName -ErrorAction Stop | Out-Null } catch {} }");
                    sb.Append("}");
                    Proc.PowerShell(sb.ToString(), Log.Write);
                },
                null));

            return list;
        }
    }
}

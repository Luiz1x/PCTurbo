using System;
using System.Runtime.InteropServices;

namespace PCTurbo
{
#if NVAPI
    // Fala com o driver NVIDIA pela NVAPI (perfil global = "Configuracoes globais" do Painel de Controle)
    public static class NvidiaDrs
    {
        public static bool Supported { get { return true; } }

        private const int BufSize = 12320;      // tamanho de NVDRS_SETTING
        private const int OffId = 4100;
        private const int OffType = 4104;
        private const int OffLocation = 4108;
        private const int OffCurrent = 8220;
        private const int NotFound = -160;      // NVAPI_SETTING_NOT_FOUND

        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QueryInterface(uint id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FInit();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FCreate(out IntPtr session);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FSession(IntPtr session);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FGlobal(IntPtr session, out IntPtr profile);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FGet(IntPtr session, IntPtr profile, uint id, IntPtr setting);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FSet(IntPtr session, IntPtr profile, IntPtr setting);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FDelete(IntPtr session, IntPtr profile, uint id);

        private delegate string SessionWork(IntPtr session, IntPtr profile);

        private static T Fn<T>(uint id) where T : class
        {
            IntPtr p = QueryInterface(id);
            if (p == IntPtr.Zero) throw new Exception("função NVAPI 0x" + id.ToString("X") + " indisponível neste driver");
            return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
        }

        private static string WithSession(bool save, SessionWork work)
        {
            IntPtr session = IntPtr.Zero;
            FSession destroy = null;
            try
            {
                int rc = Fn<FInit>(0x0150E828)();
                if (rc != 0) return "NVAPI não iniciou (código " + rc + "). Placa NVIDIA ou driver não encontrados.";

                destroy = Fn<FSession>(0xDAD9CFF8);
                rc = Fn<FCreate>(0x0694D52E)(out session);
                if (rc != 0) return "Não consegui abrir sessão no driver (código " + rc + ").";

                rc = Fn<FSession>(0x375DBD6B)(session);
                if (rc != 0) return "Não consegui carregar as configurações do driver (código " + rc + ").";

                IntPtr profile;
                rc = Fn<FGlobal>(0x617BFF9F)(session, out profile);
                if (rc != 0) return "Não achei o perfil global (código " + rc + ").";

                string err = work(session, profile);
                if (err != null) return err;

                if (save)
                {
                    rc = Fn<FSession>(0xFCBC7E14)(session);
                    if (rc != 0) return "Não consegui salvar no driver (código " + rc + ").";
                }
                return null;
            }
            catch (DllNotFoundException)
            {
                return "nvapi64.dll não encontrada (driver NVIDIA não instalado?).";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                if (session != IntPtr.Zero && destroy != null)
                {
                    try { destroy(session); } catch { }
                }
            }
        }

        private static IntPtr NewBuf()
        {
            IntPtr b = Marshal.AllocHGlobal(BufSize);
            for (int i = 0; i < BufSize; i += 4) Marshal.WriteInt32(b, i, 0);
            Marshal.WriteInt32(b, 0, BufSize | (1 << 16));
            return b;
        }

        // result[i]: valor, -1 = nao definido (padrao do driver), -2 = erro
        public static string Read(uint[] ids, long[] result)
        {
            return WithSession(false, delegate(IntPtr s, IntPtr p)
            {
                FGet get = Fn<FGet>(0x73BF8338);
                for (int i = 0; i < ids.Length; i++)
                {
                    IntPtr buf = NewBuf();
                    try
                    {
                        int rc = get(s, p, ids[i], buf);
                        if (rc == 0) result[i] = (uint)Marshal.ReadInt32(buf, OffCurrent);
                        else if (rc == NotFound) result[i] = -1;
                        else result[i] = -2;
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
                return null;
            });
        }

        public static string Apply(uint[] ids, uint[] values)
        {
            return WithSession(true, delegate(IntPtr s, IntPtr p)
            {
                FSet set = Fn<FSet>(0x577DD202);
                string errs = null;
                for (int i = 0; i < ids.Length; i++)
                {
                    IntPtr buf = NewBuf();
                    try
                    {
                        Marshal.WriteInt32(buf, OffId, unchecked((int)ids[i]));
                        Marshal.WriteInt32(buf, OffType, 0);
                        Marshal.WriteInt32(buf, OffLocation, 0);
                        Marshal.WriteInt32(buf, OffCurrent, unchecked((int)values[i]));
                        int rc = set(s, p, buf);
                        if (rc != 0) errs = (errs == null ? "" : errs + "; ") + "0x" + ids[i].ToString("X8") + " (código " + rc + ")";
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
                return errs == null ? null : "Não aceitas pelo driver: " + errs;
            });
        }

        // Volta ao padrao do driver
        public static string Restore(uint[] ids)
        {
            return WithSession(true, delegate(IntPtr s, IntPtr p)
            {
                FDelete del = Fn<FDelete>(0xE4A26362);
                for (int i = 0; i < ids.Length; i++)
                {
                    int rc = del(s, p, ids[i]);
                    if (rc != 0 && rc != NotFound) return "Falha ao restaurar 0x" + ids[i].ToString("X8") + " (código " + rc + ")";
                }
                return null;
            });
        }
    }
#else
    public static class NvidiaDrs
    {
        public static bool Supported { get { return false; } }
        private const string Msg = "Esta versão foi compilada sem o módulo NVIDIA. Use o Painel de Controle NVIDIA manualmente.";
        public static string Read(uint[] ids, long[] result) { for (int i = 0; i < result.Length; i++) result[i] = -2; return Msg; }
        public static string Apply(uint[] ids, uint[] values) { return Msg; }
        public static string Restore(uint[] ids) { return Msg; }
    }
#endif
}

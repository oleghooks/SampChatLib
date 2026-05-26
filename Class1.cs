using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SampChatLib
{
    public class SampChat : IDisposable
    {
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;


        private const uint PROCESS_ACCESS =
            PROCESS_VM_READ |
            PROCESS_VM_WRITE |
            PROCESS_VM_OPERATION |
            PROCESS_CREATE_THREAD |
            PROCESS_QUERY_INFORMATION;
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr addr,
            byte[] buffer,
            int size,
            out int written);

        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr addr,
            uint size,
            uint type,
            uint protect);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            IntPtr a,
            uint b,
            IntPtr start,
            IntPtr param,
            uint flags,
            IntPtr id);

        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr h, uint ms);

        private IntPtr hProcess;
        private IntPtr sampBase;

        // ===== ATTACH =====
        public bool Attach()
        {
            var p = Process.GetProcesses()
                .FirstOrDefault(x => x.MainWindowTitle.Contains("AMAZING ONLINE"));

            if (p == null)
                return false;

            hProcess = OpenProcess(PROCESS_ACCESS, false, p.Id);
            if (hProcess == IntPtr.Zero)
                return false;

            sampBase = GetModule(p, "azmp.dll");

            return sampBase != IntPtr.Zero;
        }

        private IntPtr GetModule(Process p, string name)
        {
            foreach (ProcessModule m in p.Modules)
                if (m.ModuleName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return m.BaseAddress;

            return IntPtr.Zero;
        }
        private const uint PAGE_READWRITE = 0x04;
        // ===== CORE =====
        private IntPtr Alloc(byte[] data)
        {
            IntPtr addr = VirtualAllocEx(
                hProcess,
                IntPtr.Zero,
                (uint)data.Length,
                0x1000 | 0x2000,
                PAGE_READWRITE);

            WriteProcessMemory(hProcess, addr, data, data.Length, out _);
            return addr;
        }

        private IntPtr WriteString(string text)
        {
            return Alloc(Encoding.GetEncoding(1251).GetBytes(text + "\0"));
        }

        private void Run(IntPtr func, IntPtr arg)
        {
            byte[] shell = new byte[32];
            int i = 0;

            shell[i++] = 0x68;
            BitConverter.GetBytes((int)arg).CopyTo(shell, i);
            i += 4;

            shell[i++] = 0xB8;
            BitConverter.GetBytes((int)func).CopyTo(shell, i);
            i += 4;

            shell[i++] = 0xFF;
            shell[i++] = 0xD0;

            shell[i++] = 0xC3;

            IntPtr mem = Alloc(shell);

            var hThread = CreateRemoteThread(
                hProcess,
                IntPtr.Zero,
                0,
                mem,
                IntPtr.Zero,
                0,
                IntPtr.Zero);

            WaitForSingleObject(hThread, 0xFFFFFFFF);
            CloseHandle(hThread);
        }

        // ===== PUBLIC API =====
        public void SendChat(string text)
        {
            bool cmd = text.StartsWith("/");

            IntPtr func = cmd
                ? sampBase + 0x69190
                : sampBase + 0x5820;

            IntPtr str = WriteString(text);

            Run(func, str);
        }

        public void Dispose()
        {
            if (hProcess != IntPtr.Zero)
                CloseHandle(hProcess);
        }
    }
}
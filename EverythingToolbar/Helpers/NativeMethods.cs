using System;
using System.Runtime.InteropServices;
using NLog;

namespace EverythingToolbar.Helpers
{
    public class NativeMethods
    {
        private const string User32Dll = "user32.dll";
        private static readonly ILogger Logger = ToolbarLogger.GetLogger<NativeMethods>();

        public static IntPtr FindTaskbarHandle()
        {
            return FindWindow("Shell_TrayWnd", null);
        }

        public static void FocusTaskbarWindow()
        {
            var taskbarHandle = FindTaskbarHandle();
            if (taskbarHandle != IntPtr.Zero)
            {
                ForciblySetForegroundWindow(taskbarHandle);
            }
        }

        public static void ForciblySetForegroundWindow(IntPtr handle)
        {
            var success = SetForegroundWindow(handle);
            if (success)
            {
                SetActiveWindow(handle);
                return;
            }

            Logger.Debug("SetForegroundWindow failed, trying to force window to front...");

            var foregroundWindow = GetForegroundWindow();
            var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
            var targetThreadId = GetWindowThreadProcessId(handle, out _);

            if (foregroundThreadId != targetThreadId)
                AttachThreadInput(foregroundThreadId, targetThreadId, true);

            try
            {
                SetForegroundWindow(handle);
                SetActiveWindow(handle);
            }
            finally
            {
                if (foregroundThreadId != targetThreadId)
                    AttachThreadInput(foregroundThreadId, targetThreadId, false);
            }
        }

        [DllImport(User32Dll)]
        public static extern uint FlashWindow(IntPtr hWnd, bool bInvert);

        [DllImport(User32Dll)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport(User32Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport(User32Dll)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport(User32Dll)]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport(User32Dll, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(
            IntPtr parentHandle,
            IntPtr childAfter,
            string className,
            string? windowTitle
        );

        [DllImport(User32Dll)]
        public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport(User32Dll)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, ref Copydatastruct lParam);

        [DllImport(User32Dll)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport(User32Dll)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport(User32Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags
        );

        [DllImport("dwmapi.dll")]
        public static extern int DwmFlush();

        [StructLayout(LayoutKind.Sequential)]
        public struct Copydatastruct
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport(User32Dll, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            [MarshalAs(UnmanagedType.LPStr)] string lpClassName,
            [MarshalAs(UnmanagedType.LPStr)] string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam
        );

        [DllImport(User32Dll, EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport(User32Dll)]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}

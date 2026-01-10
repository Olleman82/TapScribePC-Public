using System.Runtime.InteropServices;

namespace WsprPc.Services;

public enum VirtualKey
{
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B
}

public sealed class GlobalKeyHoldService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const short KeyDownMask = unchecked((short)0x8000);
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;

    private readonly int _vkCode;
    private bool _isDown;
    private readonly object _stateLock = new();
    private IntPtr _hookHandle;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;

    public event Action? KeyDown;
    public event Action? KeyUp;
    public event Action<string>? Diagnostic;

    private void Report(string message) => Diagnostic?.Invoke(message);

    public GlobalKeyHoldService(VirtualKey key)
    {
        _vkCode = (int)key;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
            return;

        _hookProc = HookCallback;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            WhKeyboardLl,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);
        Report($"Hook start vk={_vkCode} handle={_hookHandle}");
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
            return;

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _hookProc = null;
        _isDown = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();
            var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);

            if (info.vkCode == _vkCode)
            {
                Action? toInvoke = null;
                lock (_stateLock)
                {
                    if ((message == WmKeydown || message == WmSyskeydown) && !_isDown)
                    {
                        _isDown = true;
                        toInvoke = KeyDown;
                        Report($"Hook vk={_vkCode} msg={message} (keydown)");
                    }
                    else if ((message == WmKeyup || message == WmSyskeyup) && _isDown)
                    {
                        _isDown = false;
                        toInvoke = KeyUp;
                        Report($"Hook vk={_vkCode} msg={message} (keyup)");
                    }
                }
                toInvoke?.Invoke();
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }

    private static class NativeMethods
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct KbdLlHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}

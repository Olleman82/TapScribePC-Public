using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;

namespace WsprPc.Services;

public sealed class PasteInjector
{
    public bool PasteText(string text, IntPtr targetWindow)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!TrySetClipboard(text))
            return false;

        if (targetWindow != IntPtr.Zero)
        {
            FocusWindow(targetWindow);
            // Increased delay to 100ms to allow target app (Word, Browser) to react to focus
            Thread.Sleep(100);
        }

        if (TrySendCtrlV())
            return true;

        try
        {
            SendKeys.SendWait("^v");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetClipboard(string text)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                WpfClipboard.SetText(text);
                return true;
            }
            catch
            {
                Thread.Sleep(50);
            }
        }
        return false;
    }

    private static bool TrySendCtrlV()
    {
        const ushort VkControl = 0x11;
        const ushort VkV = 0x56;

        INPUT[] inputs =
        [
            INPUT.Keyboard(VkControl, 0),
            INPUT.Keyboard(VkV, 0),
            INPUT.Keyboard(VkV, KeyEventF.KeyUp),
            INPUT.Keyboard(VkControl, KeyEventF.KeyUp)
        ];

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return sent == inputs.Length;
    }

    private static void FocusWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero || !IsWindow(targetWindow)) return;

        // Restore if minimized
        if (IsIconic(targetWindow))
        {
            ShowWindow(targetWindow, 9); // SW_RESTORE
        }

        IntPtr foreground = GetForegroundWindow();
        if (foreground == targetWindow) return;

        uint fgThread = GetWindowThreadProcessId(foreground, out _);
        uint targetThread = GetWindowThreadProcessId(targetWindow, out _);
        uint currentThread = GetCurrentThreadId();

        // The "Alt-Key trick" - sending a dummy Alt key press can often bypass focus-stealing prevention
        // because Windows allows the window receiving the last input to set the foreground window.
        const ushort VkMenu = 0x12; // Alt key
        INPUT[] altInput = 
        [
            INPUT.Keyboard(VkMenu, 0),
            INPUT.Keyboard(VkMenu, KeyEventF.KeyUp)
        ];
        SendInput((uint)altInput.Length, altInput, Marshal.SizeOf<INPUT>());

        AttachThreadInput(currentThread, targetThread, true);
        if (foreground != IntPtr.Zero && fgThread != targetThread)
            AttachThreadInput(fgThread, targetThread, true);

        SetForegroundWindow(targetWindow);
        BringWindowToTop(targetWindow);
        SetFocus(targetWindow);

        if (foreground != IntPtr.Zero && fgThread != targetThread)
            AttachThreadInput(fgThread, targetThread, false);
        AttachThreadInput(currentThread, targetThread, false);
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;

        public static INPUT Keyboard(ushort vk, KeyEventF flags)
        {
            return new INPUT
            {
                type = 1,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public KeyEventF dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [Flags]
    private enum KeyEventF : uint
    {
        ExtendedKey = 0x0001,
        KeyUp = 0x0002,
        Unicode = 0x0004,
        Scancode = 0x0008
    }
}


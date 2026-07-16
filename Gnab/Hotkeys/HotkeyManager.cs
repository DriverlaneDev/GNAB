using System.Runtime.InteropServices;
using Gnab.Settings;

namespace Gnab.Hotkeys;

public sealed class HotkeyManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WmHotkey = 0x0312;

    private sealed class HotkeyWindow : NativeWindow
    {
        public event Action<int>? HotkeyPressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                HotkeyPressed?.Invoke(m.WParam.ToInt32());
            }

            base.WndProc(ref m);
        }
    }

    private readonly HotkeyWindow _window = new();
    private readonly Dictionary<int, Action> _actions = new();

    public HotkeyManager()
    {
        _window.HotkeyPressed += id =>
        {
            if (_actions.TryGetValue(id, out var action))
            {
                action();
            }
        };
    }

    /// <summary>
    /// Registers a hotkey under a caller-supplied stable id. Reusing the same id on a later call
    /// replaces any previous registration for that id (this is how RegisterHotKey behaves for a
    /// given hWnd+id pair), so re-registration is safe even if a prior UnregisterHotKey silently failed.
    /// </summary>
    public bool Register(int id, HotkeyCombo combo, Action action)
    {
        if (!combo.IsAssigned)
        {
            UnregisterHotKey(_window.Handle, id);
            _actions.Remove(id);
            return false;
        }

        if (RegisterHotKey(_window.Handle, id, (uint)combo.Modifiers, (uint)combo.Key))
        {
            _actions[id] = action;
            return true;
        }

        _actions.Remove(id);
        return false;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(_window.Handle, id);
        }

        _actions.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.DestroyHandle();
    }
}

using EverythingToolbar.Helpers;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace EverythingToolbar.Settings
{
    public partial class Shortcuts
    {
        // ── Global shortcut state ──────────────────────────────────────────
        private Key _globalKey;
        private Key _globalOriginalKey;
        private ModifierKeys _globalModifiers;
        private ModifierKeys _globalOriginalModifiers;
        private ModifierKeys _tempMods;

        // ── Local shortcut state ───────────────────────────────────────────
        private System.Windows.Controls.Control? _activeLocalBox;
        private bool _nonPrintableOnly;
        private readonly Dictionary<string, ShortcutBinding> _originalLocal = new();
        private FilterRangeBinding _originalFilterRange;

        private static event EventHandler<WinKeyEventArgs>? WinKeyEventHandler;
        private static LowLevelKeyboardProc? _llKeyboardHookCallback;
        private static IntPtr _llKeyboardHookId = IntPtr.Zero;
        private const int WhKeyboardLl = 13;
        private const int WmKeydown = 0x0100;
        private const int WmSyskeydown = 0x0104;

        public Shortcuts()
        {
            InitializeComponent();
        }

        // ── Page lifecycle ─────────────────────────────────────────────────

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            StartMenuIntegration.Instance.Disable();
            HotkeyManager.Current.IsEnabled = false;

            // Global shortcut (Still uses Name in XAML)
            _globalModifiers = (ModifierKeys)ToolbarSettings.User.ShortcutModifiers;
            _globalKey = (Key)ToolbarSettings.User.ShortcutKey;
            _globalOriginalKey = _globalKey;
            _globalOriginalModifiers = _globalModifiers;
            UpdateTextBox(GlobalShortcutTextBox, _globalKey, _globalModifiers);

            // Local shortcuts — find them by Tag and load values
            LoadLocalShortcut("Open", ToolbarSettings.User.LocalShortcutOpen);
            LoadLocalShortcut("OpenPath", ToolbarSettings.User.LocalShortcutOpenPath);
            LoadLocalShortcut("RunAsAdmin", ToolbarSettings.User.LocalShortcutRunAsAdmin);
            LoadLocalShortcut("OpenInEverything", ToolbarSettings.User.LocalShortcutOpenInEverything);
            LoadLocalShortcut("Properties", ToolbarSettings.User.LocalShortcutProperties);
            LoadLocalShortcut("CopyPath", ToolbarSettings.User.LocalShortcutCopyPath);
            LoadLocalShortcut("Preview", ToolbarSettings.User.LocalShortcutPreview);
            LoadLocalShortcut("NavigateUp", ToolbarSettings.User.LocalShortcutNavigateUp);
            LoadLocalShortcut("NavigateDown", ToolbarSettings.User.LocalShortcutNavigateDown);
            LoadLocalShortcut("CycleNext", ToolbarSettings.User.LocalShortcutCycleNext);
            LoadLocalShortcut("CyclePrev", ToolbarSettings.User.LocalShortcutCyclePrev);

            // Filter range
            _originalFilterRange = ToolbarSettings.User.LocalShortcutFilterRange;
            LoadFilterRange(_originalFilterRange);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            HotkeyManager.Current.IsEnabled = true;
            ReleaseKeyboard();
            StartMenuIntegration.Instance.Initialize();

            if (_globalKey != _globalOriginalKey || _globalModifiers != _globalOriginalModifiers)
                ApplyGlobalShortcut();
        }

        // ── Local shortcut helpers ─────────────────────────────────────────

        private void LoadLocalShortcut(string tag, ShortcutBinding binding)
        {
            _originalLocal[tag] = binding;
            var box = FindByTag(this, tag) as System.Windows.Controls.Control;
            if (box != null) SetBoxText(box, binding.Key, binding.Modifiers);
        }

        private FrameworkElement? FindByTag(DependencyObject parent, string tag)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Tag?.ToString() == tag) return fe;

                var result = FindByTag(child, tag);
                if (result != null) return result;
            }
            return null;
        }

        private void SetBoxText(System.Windows.Controls.Control box, Key key, ModifierKeys mods)
        {
            var text = BuildShortcutText(key, mods);
            if (box is Wpf.Ui.Controls.TextBox tb) tb.Text = text;
        }

        private string GetBoxKey(System.Windows.Controls.Control box)
        {
            return box.Tag as string ?? "";
        }

        private void SaveLocalShortcut(System.Windows.Controls.Control box, Key key, ModifierKeys mods)
        {
            var binding = new ShortcutBinding(key, mods);
            switch (GetBoxKey(box))
            {
                case "Open": ToolbarSettings.User.LocalShortcutOpen = binding; break;
                case "OpenPath": ToolbarSettings.User.LocalShortcutOpenPath = binding; break;
                case "RunAsAdmin": ToolbarSettings.User.LocalShortcutRunAsAdmin = binding; break;
                case "OpenInEverything": ToolbarSettings.User.LocalShortcutOpenInEverything = binding; break;
                case "Properties": ToolbarSettings.User.LocalShortcutProperties = binding; break;
                case "CopyPath": ToolbarSettings.User.LocalShortcutCopyPath = binding; break;
                case "Preview": ToolbarSettings.User.LocalShortcutPreview = binding; break;
                case "NavigateUp": ToolbarSettings.User.LocalShortcutNavigateUp = binding; break;
                case "NavigateDown": ToolbarSettings.User.LocalShortcutNavigateDown = binding; break;
                case "CycleNext": ToolbarSettings.User.LocalShortcutCycleNext = binding; break;
                case "CyclePrev": ToolbarSettings.User.LocalShortcutCyclePrev = binding; break;
            }
        }

        // ── Filter range ───────────────────────────────────────────────────

        private void LoadFilterRange(FilterRangeBinding binding)
        {
            // Find and set the Start Key Box
            var startKeyBox = FindByTag(this, "FilterRangeStart") as System.Windows.Controls.Control;
            if (startKeyBox != null) SetBoxText(startKeyBox, binding.StartKey, ModifierKeys.None);

            // Find and set the Count Box
            var countBox = FindByTag(this, "FilterRangeCount") as Wpf.Ui.Controls.NumberBox;
            if (countBox != null) countBox.Value = binding.Count;

            // Find and set the Modifier ComboBox
            var modifierCombo = FindByTag(this, "FilterRangeModifier") as System.Windows.Controls.ComboBox;
            if (modifierCombo != null)
            {
                modifierCombo.SelectedIndex = binding.Modifiers switch
                {
                    ModifierKeys.Control => 1,
                    ModifierKeys.Shift => 2,
                    ModifierKeys.Alt => 3,
                    _ => 0
                };
            }

            UpdateFilterRangePreview();
        }

        private void SaveFilterRange()
        {
            var current = ToolbarSettings.User.LocalShortcutFilterRange;
            ToolbarSettings.User.LocalShortcutFilterRange = new FilterRangeBinding(current.StartKey, current.Modifiers, current.Count);
        }

        private void OnFilterRangeModifierChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Use the 'sender' object to get the ComboBox that triggered the event
            var combo = sender as System.Windows.Controls.ComboBox;
            if (combo == null) return;

            var mods = combo.SelectedIndex switch
            {
                1 => ModifierKeys.Control,
                2 => ModifierKeys.Shift,
                3 => ModifierKeys.Alt,
                _ => ModifierKeys.None
            };

            var current = ToolbarSettings.User.LocalShortcutFilterRange;
            ToolbarSettings.User.LocalShortcutFilterRange = new FilterRangeBinding(current.StartKey, mods, current.Count);
            UpdateFilterRangePreview();
        }

        private void OnFilterRangeCountChanged(object sender, RoutedEventArgs e)
        {
            // Use the 'sender' object to get the NumberBox that triggered the event
            var numBox = sender as Wpf.Ui.Controls.NumberBox;
            if (numBox == null) return;

            var current = ToolbarSettings.User.LocalShortcutFilterRange;
            var count = (int)(numBox.Value ?? 1);

            ToolbarSettings.User.LocalShortcutFilterRange = new FilterRangeBinding(current.StartKey, current.Modifiers, count);
            UpdateFilterRangePreview();
        }

        private void UpdateFilterRangePreview()
        {
            // Find the TextBlock using our tag helper
            var previewText = FindByTag(this, "FilterRangePreview") as Wpf.Ui.Controls.TextBlock;
            if (previewText == null) return;

            var binding = ToolbarSettings.User.LocalShortcutFilterRange;
            var modText = binding.Modifiers switch
            {
                ModifierKeys.Control => "Ctrl+",
                ModifierKeys.Shift => "Shift+",
                ModifierKeys.Alt => "Alt+",
                _ => ""
            };

            var keys = new List<string>();
            for (int i = 0; i < binding.Count; i++)
            {
                var k = (Key)((int)binding.StartKey + i);
                keys.Add(modText + k.ToString());
            }
            previewText.Text = string.Join(", ", keys);
        }

        // ── Keyboard capture ───────────────────────────────────────────────

        private void OnGotKeyboardFocusLocal(object sender, KeyboardFocusChangedEventArgs e)
        {
            _activeLocalBox = sender as System.Windows.Controls.Control;
            _nonPrintableOnly = false;
            CaptureKeyboard(OnLocalKeyPressedReleased);
        }

        private void OnGotKeyboardFocusLocalNonPrintable(object sender, KeyboardFocusChangedEventArgs e)
        {
            _activeLocalBox = sender as System.Windows.Controls.Control;
            _nonPrintableOnly = true;
            CaptureKeyboard(OnLocalKeyPressedReleased);
        }

        private void OnLostKeyboardFocusLocal(object sender, KeyboardFocusChangedEventArgs e)
        {
            _activeLocalBox = null;
            ReleaseKeyboard();
        }

        private Key _localCurrentKey;
        private ModifierKeys _localCurrentMods;
        private ModifierKeys _localTempMods;

        private void OnLocalKeyPressedReleased(object? sender, WinKeyEventArgs e)
        {
            if (_activeLocalBox == null) return;

            switch (e.Key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    _localTempMods = e.IsDown ? _localTempMods | ModifierKeys.Control : _localTempMods & ~ModifierKeys.Control;
                    break;
                case Key.LeftShift:
                case Key.RightShift:
                    _localTempMods = e.IsDown ? _localTempMods | ModifierKeys.Shift : _localTempMods & ~ModifierKeys.Shift;
                    break;
                case Key.LeftAlt:
                case Key.RightAlt:
                    _localTempMods = e.IsDown ? _localTempMods | ModifierKeys.Alt : _localTempMods & ~ModifierKeys.Alt;
                    break;
                case Key.LWin:
                case Key.RWin:
                    _localTempMods = e.IsDown ? _localTempMods | ModifierKeys.Windows : _localTempMods & ~ModifierKeys.Windows;
                    break;
                default:
                    if (!e.IsDown) break;

                    if (e.Key == Key.Escape)
                    {
                        if (_originalLocal.TryGetValue(GetBoxKey(_activeLocalBox), out var original))
                        {
                            _localCurrentKey = original.Key;
                            _localCurrentMods = original.Modifiers;
                            SaveLocalShortcut(_activeLocalBox, original.Key, original.Modifiers);
                        }
                    }
                    else
                    {
                        if (_nonPrintableOnly && IsPrintable(e.Key)) return;

                        _localCurrentKey = e.Key;
                        _localCurrentMods = _localTempMods;
                        SaveLocalShortcut(_activeLocalBox, _localCurrentKey, _localCurrentMods);

                        if (GetBoxKey(_activeLocalBox) == "FilterRangeStart")
                        {
                            var current = ToolbarSettings.User.LocalShortcutFilterRange;
                            ToolbarSettings.User.LocalShortcutFilterRange = new FilterRangeBinding(_localCurrentKey, current.Modifiers, current.Count);
                            UpdateFilterRangePreview();
                        }
                    }
                    Dispatcher.Invoke(() => SetBoxText(_activeLocalBox, _localCurrentKey, _localCurrentMods));
                    break;
            }
        }

        private static bool IsPrintable(Key key)
        {
            return key >= Key.A && key <= Key.Z
                || key >= Key.D0 && key <= Key.D9
                || key >= Key.NumPad0 && key <= Key.NumPad9;
        }

        private void OnGotKeyboardFocusGlobal(object sender, KeyboardFocusChangedEventArgs e)
        {
            CaptureKeyboard(OnGlobalKeyPressedReleased);
        }

        private void OnLostKeyboardFocusGlobal(object sender, KeyboardFocusChangedEventArgs e)
        {
            ReleaseKeyboard();
        }

        private void OnGlobalKeyPressedReleased(object? sender, WinKeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.LeftCtrl:
                    _tempMods = e.IsDown ? _tempMods | ModifierKeys.Control : _tempMods & ~ModifierKeys.Control;
                    break;
                case Key.LWin:
                    _tempMods = e.IsDown ? _tempMods | ModifierKeys.Windows : _tempMods & ~ModifierKeys.Windows;
                    break;
                case Key.LeftAlt:
                    _tempMods = e.IsDown ? _tempMods | ModifierKeys.Alt : _tempMods & ~ModifierKeys.Alt;
                    break;
                case Key.LeftShift:
                    _tempMods = e.IsDown ? _tempMods | ModifierKeys.Shift : _tempMods & ~ModifierKeys.Shift;
                    break;
                default:
                    if (e.IsDown)
                    {
                        if (_tempMods == ModifierKeys.None && e.Key == Key.Escape)
                        {
                            _globalKey = Key.None;
                            _globalModifiers = ModifierKeys.None;
                        }
                        else
                        {
                            _globalKey = e.Key;
                            _globalModifiers = _tempMods;
                        }
                        Dispatcher.Invoke(() => UpdateTextBox(GlobalShortcutTextBox, _globalKey, _globalModifiers));
                    }
                    break;
            }
        }

        private void ApplyGlobalShortcut()
        {
            if (_globalModifiers == ModifierKeys.Windows)
            {
                ShortcutManager.UpdateSettings(_globalKey, _globalModifiers);
                foreach (var exe in System.Diagnostics.Process.GetProcesses())
                    if (exe.ProcessName == "explorer") exe.Kill();
            }
            ShortcutManager.TrySetShortcut(_globalKey, _globalModifiers);
        }

        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(_llKeyboardHookId, nCode, wParam, lParam);
            var vkCode = (Keys)Marshal.ReadInt32(lParam);
            var isDown = (int)wParam == WmKeydown || (int)wParam == WmSyskeydown;
            switch (vkCode)
            {
                case Keys.Control:
                case Keys.ControlKey:
                case Keys.LControlKey:
                case Keys.RControlKey:
                    WinKeyEventHandler?.Invoke(null, new WinKeyEventArgs(isDown, Key.LeftCtrl));
                    break;
                case Keys.Shift:
                case Keys.ShiftKey:
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                    WinKeyEventHandler?.Invoke(null, new WinKeyEventArgs(isDown, Key.LeftShift));
                    break;
                case Keys.Alt:
                    WinKeyEventHandler?.Invoke(null, new WinKeyEventArgs(isDown, Key.LeftAlt));
                    break;
                case Keys.LWin:
                case Keys.RWin:
                    WinKeyEventHandler?.Invoke(null, new WinKeyEventArgs(isDown, Key.LWin));
                    break;
                default:
                    WinKeyEventHandler?.Invoke(null, new WinKeyEventArgs(isDown, KeyInterop.KeyFromVirtualKey((int)vkCode)));
                    break;
            }
            return (IntPtr)1;
        }

        private static void CaptureKeyboard(EventHandler<WinKeyEventArgs> callback)
        {
            ReleaseKeyboard();
            WinKeyEventHandler += callback;
            _llKeyboardHookCallback = KeyboardHookCallback;
            _llKeyboardHookId = SetWindowsHookEx(WhKeyboardLl, _llKeyboardHookCallback, 0, 0);
        }

        private static void ReleaseKeyboard()
        {
            WinKeyEventHandler = null;
            UnhookWindowsHookEx(_llKeyboardHookId);
        }

        private static void UpdateTextBox(Wpf.Ui.Controls.TextBox box, Key key, ModifierKeys mods)
        {
            box.Text = BuildShortcutText(key, mods);
        }

        private static string BuildShortcutText(Key key, ModifierKeys mods)
        {
            var sb = new StringBuilder();
            if ((mods & ModifierKeys.Control) != 0) sb.Append("Ctrl+");
            if ((mods & ModifierKeys.Windows) != 0) sb.Append("Win+");
            if ((mods & ModifierKeys.Alt) != 0) sb.Append("Alt+");
            if ((mods & ModifierKeys.Shift) != 0) sb.Append("Shift+");
            if (key != Key.None) sb.Append(key.ToString());
            return sb.ToString();
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        public class WinKeyEventArgs(bool isDown, Key key) : EventArgs
        {
            public bool IsDown { get; set; } = isDown;
            public Key Key { get; set; } = key;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    }
}
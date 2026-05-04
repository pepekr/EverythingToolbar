namespace EverythingToolbar.Helpers;
using System.Windows.Input;


public class ShortcutBinding
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }

    public ShortcutBinding(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        Key = key;
        Modifiers = modifiers;
    }

    // Stored as "Key,Modifiers" string in settings.ini
    public static ShortcutBinding FromString(string value, ShortcutBinding fallback)
    {
        try
        {
            var parts = value.Split(',');
            if (parts.Length == 2)
                return new ShortcutBinding(
                    (Key)int.Parse(parts[0]),
                    (ModifierKeys)int.Parse(parts[1])
                );
        }
        catch { }
        return fallback;
    }

    public override string ToString() => $"{(int)Key},{(int)Modifiers}";

    public bool Matches(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key == Key && Keyboard.Modifiers == Modifiers;
    }
}

public class FilterRangeBinding
{
    public Key StartKey { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public int Count { get; set; }

    public FilterRangeBinding(Key startKey, ModifierKeys modifiers, int count)
    {
        StartKey = startKey;
        Modifiers = modifiers;
        Count = count;
    }

    public static FilterRangeBinding FromString(string value, FilterRangeBinding fallback)
    {
        try
        {
            var parts = value.Split(',');
            if (parts.Length == 3)
                return new FilterRangeBinding(
                    (Key)int.Parse(parts[0]),
                    (ModifierKeys)int.Parse(parts[1]),
                    int.Parse(parts[2])
                );
        }
        catch { }
        return fallback;
    }

    public override string ToString() => $"{(int)StartKey},{(int)Modifiers},{Count}";

    // Returns which filter index this key maps to, or -1 if no match
    public int MatchFilterIndex(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != Modifiers)
            return -1;

        int pressedInt = (int)e.Key;
        int startInt = (int)StartKey;

        var index = pressedInt - startInt;
        if (index >= 0 && index < Count)
            return index;

        return -1;
    }
}
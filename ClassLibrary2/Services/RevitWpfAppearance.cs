using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace AutoDocumentation.Services;

/// <summary>
/// Applies Revit's current UI theme (light/dark) to WPF windows. No in-plugin toggle.
/// </summary>
public static class RevitWpfAppearance
{
    private const string ThemeMarkerKey = "__AutoDocumentationThemeDict";

    public static bool IsDarkTheme() => UIThemeManager.CurrentTheme == UITheme.Dark;

    public static void Apply(Window window)
    {
        RemoveThemeDictionary(window);
        var dict = BuildPalette(IsDarkTheme());
        dict[ThemeMarkerKey] = true;
        window.Resources.MergedDictionaries.Insert(0, dict);
        ApplySystemColorBrushOverrides(window);
    }

    public static void AttachThemeChanged(UIApplication uiApp, Window window)
    {
        void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            if (window.Dispatcher.CheckAccess())
                ApplyIfOpen(window);
            else
                window.Dispatcher.Invoke(() => ApplyIfOpen(window));
        }

        uiApp.ThemeChanged += OnThemeChanged;
        window.Closed += (_, _) => uiApp.ThemeChanged -= OnThemeChanged;
    }

    private static void ApplyIfOpen(Window window)
    {
        if (!window.IsLoaded)
            return;
        Apply(window);
    }

    private static void RemoveThemeDictionary(Window window)
    {
        for (var i = window.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            var md = window.Resources.MergedDictionaries[i];
            if (md.Contains(ThemeMarkerKey))
                window.Resources.MergedDictionaries.RemoveAt(i);
        }
    }

    private static void ApplySystemColorBrushOverrides(Window window)
    {
        var dark = IsDarkTheme();
        var r = window.Resources;

        r[SystemColors.WindowBrushKey] = Brush(dark ? "#FF2C2C2E" : "#FFFFFFFF");
        r[SystemColors.WindowTextBrushKey] = Brush(dark ? "#FFF2F2F7" : "#FF1C1C1E");
        r[SystemColors.ControlBrushKey] = Brush(dark ? "#FF2C2C2E" : "#FFFFFFFF");
        r[SystemColors.ControlTextBrushKey] = Brush(dark ? "#FFF2F2F7" : "#FF1C1C1E");
        r[SystemColors.ControlLightBrushKey] = Brush(dark ? "#FF2C2C2E" : "#FFF2F2F7");
        r[SystemColors.ControlLightLightBrushKey] = Brush(dark ? "#FF2C2C2E" : "#FFFFFFFF");
        r[SystemColors.ControlDarkBrushKey] = Brush(dark ? "#FF48484A" : "#FFC6C6C8");
        r[SystemColors.HighlightBrushKey] = Brush(dark ? "#FF0A84FF" : "#FF0078D7");
        r[SystemColors.HighlightTextBrushKey] = Brushes.White;
        r[SystemColors.InactiveSelectionHighlightBrushKey] = Brush(dark ? "#FF244060" : "#FFE0E0E6");
        r[SystemColors.InactiveSelectionHighlightTextBrushKey] = Brush(dark ? "#FFF2F2F7" : "#FF1C1C1E");
    }

    private static SolidColorBrush Brush(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static void AddBrush(ResourceDictionary d, string key, string hex) =>
        d[key] = Brush(hex);

    private static ResourceDictionary BuildPalette(bool dark)
    {
        var d = new ResourceDictionary();

        if (dark)
        {
            AddBrush(d, "App_Background", "#FF1C1C1E");
            AddBrush(d, "App_Foreground", "#FFF2F2F7");
            AddBrush(d, "App_MutedForeground", "#FFAEAEB2");
            AddBrush(d, "App_SubtleForeground", "#FF8E8E93");
            AddBrush(d, "Surface_Default", "#FF2C2C2E");
            AddBrush(d, "Surface_Alt", "#FF252528");
            AddBrush(d, "Border_Default", "#FF48484A");
            AddBrush(d, "Border_Focus", "#FF0A84FF");
            AddBrush(d, "DataGrid_Background", "#FF2C2C2E");
            AddBrush(d, "DataGrid_Foreground", "#FFF2F2F7");
            AddBrush(d, "DataGrid_RowBackground", "#FF2C2C2E");
            AddBrush(d, "DataGrid_AltRowBackground", "#FF252528");
            AddBrush(d, "DataGrid_HeaderBackground", "#FF3A3A3C");
            AddBrush(d, "DataGrid_HeaderForeground", "#FFF2F2F7");
            AddBrush(d, "DataGrid_GridLine", "#FF48484A");
            AddBrush(d, "DataGrid_RowSelected", "#FF244060");
            AddBrush(d, "DataGrid_RowHover", "#FF343436");
            AddBrush(d, "DataGrid_CellSelected", "#FF244060");
            AddBrush(d, "DataGrid_TextBoxBackground", "#FF1C1C1E");
            AddBrush(d, "DataGrid_TextBoxForeground", "#FFF2F2F7");
            AddBrush(d, "DataGrid_TextBoxBorder", "#FF48484A");
            AddBrush(d, "DataGrid_TextSelection", "#FF0A84FF");

            AddBrush(d, "Btn_Secondary_Background", "#FF3A3A3C");
            AddBrush(d, "Btn_Secondary_Foreground", "#FFF2F2F7");
            AddBrush(d, "Btn_Secondary_Hover", "#FF48484A");
            AddBrush(d, "Btn_Secondary_Pressed", "#FF555558");
            AddBrush(d, "Btn_Secondary_DisabledBg", "#FF252528");
            AddBrush(d, "Btn_Secondary_DisabledFg", "#FF8E8E93");

            AddBrush(d, "Btn_Primary_Background", "#FF0A84FF");
            AddBrush(d, "Btn_Primary_Foreground", "#FFFFFFFF");
            AddBrush(d, "Btn_Primary_Hover", "#FF2A8CFF");
            AddBrush(d, "Btn_Primary_Pressed", "#FF0870E0");
            AddBrush(d, "Btn_Primary_DisabledBg", "#FF1C4A7A");
            AddBrush(d, "Btn_Primary_DisabledFg", "#FF8E8E93");

            AddBrush(d, "Btn_Danger_Background", "#FF4A2828");
            AddBrush(d, "Btn_Danger_Foreground", "#FFF2F2F7");
            AddBrush(d, "Btn_Danger_Hover", "#FF5A3232");
            AddBrush(d, "Btn_Danger_Pressed", "#FF6A3838");
            AddBrush(d, "Btn_Danger_DisabledBg", "#FF2A2424");
            AddBrush(d, "Btn_Danger_DisabledFg", "#FF8E8E93");

            AddBrush(d, "Combo_Surface", "#FF2C2C2E");
            AddBrush(d, "Combo_Border", "#FF48484A");
            AddBrush(d, "Combo_ArrowFill", "#FFF2F2F7");
        }
        else
        {
            AddBrush(d, "App_Background", "#FFFFFFFF");
            AddBrush(d, "App_Foreground", "#FF1C1C1E");
            AddBrush(d, "App_MutedForeground", "#FF636366");
            AddBrush(d, "App_SubtleForeground", "#FF8E8E93");
            AddBrush(d, "Surface_Default", "#FFFFFFFF");
            AddBrush(d, "Surface_Alt", "#FFF2F2F7");
            AddBrush(d, "Border_Default", "#FFC6C6C8");
            AddBrush(d, "Border_Focus", "#FF0078D7");
            AddBrush(d, "DataGrid_Background", "#FFFFFFFF");
            AddBrush(d, "DataGrid_Foreground", "#FF1C1C1E");
            AddBrush(d, "DataGrid_RowBackground", "#FFFFFFFF");
            AddBrush(d, "DataGrid_AltRowBackground", "#FFF7F7FA");
            AddBrush(d, "DataGrid_HeaderBackground", "#FFE9E9ED");
            AddBrush(d, "DataGrid_HeaderForeground", "#FF1C1C1E");
            AddBrush(d, "DataGrid_GridLine", "#FFE1E1E6");
            AddBrush(d, "DataGrid_RowSelected", "#FFD2E9FF");
            AddBrush(d, "DataGrid_RowHover", "#FFEFF3F8");
            AddBrush(d, "DataGrid_CellSelected", "#FFD2E9FF");
            AddBrush(d, "DataGrid_TextBoxBackground", "#FFFFFFFF");
            AddBrush(d, "DataGrid_TextBoxForeground", "#FF1C1C1E");
            AddBrush(d, "DataGrid_TextBoxBorder", "#FFC6C6C8");
            AddBrush(d, "DataGrid_TextSelection", "#FF0078D7");

            AddBrush(d, "Btn_Secondary_Background", "#FFE9E9ED");
            AddBrush(d, "Btn_Secondary_Foreground", "#FF1C1C1E");
            AddBrush(d, "Btn_Secondary_Hover", "#FFDCDCE0");
            AddBrush(d, "Btn_Secondary_Pressed", "#FFCFCFD4");
            AddBrush(d, "Btn_Secondary_DisabledBg", "#FFF2F2F7");
            AddBrush(d, "Btn_Secondary_DisabledFg", "#FF8E8E93");

            AddBrush(d, "Btn_Primary_Background", "#FF0078D7");
            AddBrush(d, "Btn_Primary_Foreground", "#FFFFFFFF");
            AddBrush(d, "Btn_Primary_Hover", "#FF1084E0");
            AddBrush(d, "Btn_Primary_Pressed", "#FF0B6BB8");
            AddBrush(d, "Btn_Primary_DisabledBg", "#FFB9D9F5");
            AddBrush(d, "Btn_Primary_DisabledFg", "#FF8E8E93");

            AddBrush(d, "Btn_Danger_Background", "#FFFFECEC");
            AddBrush(d, "Btn_Danger_Foreground", "#FF7A1E1E");
            AddBrush(d, "Btn_Danger_Hover", "#FFFFD6D6");
            AddBrush(d, "Btn_Danger_Pressed", "#FFFFC2C2");
            AddBrush(d, "Btn_Danger_DisabledBg", "#FFFFF2F2");
            AddBrush(d, "Btn_Danger_DisabledFg", "#FF8E8E93");

            AddBrush(d, "Combo_Surface", "#FFFFFFFF");
            AddBrush(d, "Combo_Border", "#FFC6C6C8");
            AddBrush(d, "Combo_ArrowFill", "#FF1C1C1E");
        }

        return d;
    }
}

using System;
using System.Collections.Generic;
using BabyStepsMenuLib;
using HarmonyLib;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rulebook;

// the RULEBOOK page in the pause menu, built with BabyStepsMenuLib (optional: only touched when it's installed).
// tabs: Falls, World, Arms, Legs, Size, Misc, Options (4 rows a page: more runs into the Close button). every slider says its normal value.
// with FallTracker also installed, both pages open from one MODS button (MenuHub)
static class RulebookMenu
{
    static readonly List<(Slider slider, Core.Dial dial)> sliders = new();
    static Toggle tagToggle, itemsToggle, hatToggle;
    static Button resetBtn;

    static bool built;   // our tabs are on the pause menu page (ours, or the shared one)

    internal const string PageName = "Rulebook";
    internal static void Announce() => MenuHub.Announce(PageName);

    internal static void Build(MelonLoader.MelonLogger.Instance log, HarmonyLib.Harmony harmony)
    {
        MenuHub.StyleWith(b => Style(b.GetComponentInChildren<TextMeshProUGUI>(true), Scale));
        MenuHub.Build(PageName, new()
        {
            ("Falls", tab => Dials(tab, "Falls")), ("World", tab => Dials(tab, "World")),
            ("Arms", tab => Dials(tab, "Arms")), ("Legs", tab => Dials(tab, "Legs")),
            ("Size", tab => Dials(tab, "Size")), ("Misc", Misc), ("Options", Options),
        }, log, harmony);
    }

    static void Dials(MenuInjectionLibrary.TabBuilder tab, string page)
    {
        built = true;
        foreach (var d in Core.Dials) if (d.Tab == page) Row(tab, d);
    }

    static void Misc(MenuInjectionLibrary.TabBuilder tab)
    {
        built = true;
        itemsToggle = tab.AddToggle("Never drop items", Core.KeepItems.Value, (UnityAction<bool>)(v => { Core.KeepItems.Value = v; Core.Changed(); }));
        if (itemsToggle != null) Style(itemsToggle.GetComponentInChildren<TextMeshProUGUI>(true), Scale);
        hatToggle = tab.AddToggle("Never lose the hat", Core.KeepHat.Value, (UnityAction<bool>)(v => { Core.KeepHat.Value = v; Core.Changed(); }));
        if (hatToggle != null) Style(hatToggle.GetComponentInChildren<TextMeshProUGUI>(true), Scale);
    }

    static void Options(MenuInjectionLibrary.TabBuilder tab)
    {
        built = true;
        tagToggle = tab.AddToggle("Show the rules on screen", Core.ShowTag.Value, (UnityAction<bool>)(v => { Core.ShowTag.Value = v; Core.Changed(); }));
        if (tagToggle != null) Style(tagToggle.GetComponentInChildren<TextMeshProUGUI>(true), Scale);
        var reset = resetBtn = tab.AddButton("Reset everything to normal", (UnityAction)(() =>
        {
            Core.ResetAll();
            foreach (var (s, d) in sliders) { if (s == null) continue; s.SetValueWithoutNotify(d.Entry.Value); Label(s, d); }
            if (itemsToggle != null) itemsToggle.SetIsOnWithoutNotify(false);
            if (hatToggle != null) hatToggle.SetIsOnWithoutNotify(false);
        }));
        if (reset != null) Style(reset.GetComponentInChildren<TextMeshProUGUI>(true), Scale);
    }

    static string Text(Core.Dial d, float v)
    {
        string s = d.Name.ToUpperInvariant() + " " + d.Show(v);
        if (d == Core.Grip && v < 0.3f) s += " (ICE)";
        return s + $"  <alpha=#80><size=70%>NORMAL {d.Show(d.Normal)}</size><alpha=#FF>";
    }

    // a slider whose label shows its value and its normal value
    static void Row(MenuInjectionLibrary.TabBuilder tab, Core.Dial d)
    {
        Slider s = null;
        s = tab.AddSlider(Text(d, d.Entry.Value), d.Min, d.Max, d.Entry.Value, (UnityAction<float>)(v =>
        {
            v = Mathf.Round(v * 20f) / 20f;   // steps of 0.05
            d.Entry.Value = v;
            Core.Changed();
            Label(s, d);
        }));
        if (s == null) return;
        sliders.Add((s, d));
        Label(s, d);
    }

    // the library puts the slider's label next to it in the same row
    static void Label(Slider s, Core.Dial d)
    {
        var t = LabelOf(s);
        if (t == null) return;
        t.text = Text(d, d.Entry.Value); Style(t, Scale);
        // the label keeps room for its whole text so it never runs under the slider; the slider gets what's left
        var le = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
        float w = Mathf.Max(le.minWidth, t.preferredWidth + 12f);
        le.minWidth = le.preferredWidth = w;
    }

    static TextMeshProUGUI LabelOf(Slider s) =>
        s == null || s.transform.parent == null ? null : s.transform.parent.GetComponentInChildren<TextMeshProUGUI>(true);

    // the game shows which menu item is selected through the item's own highlight; the library's copies
    // don't have one, so the selected row's label turns white and underlined like a selected menu button
    internal static void Update()
    {
        if (!built && MenuHub.ChooserButtons.Count == 0) return;
        var es = UnityEngine.EventSystems.EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        foreach (var (s, _) in sliders) Highlight(LabelOf(s), s, sel);
        if (tagToggle != null) Highlight(tagToggle.GetComponentInChildren<TextMeshProUGUI>(true), tagToggle, sel);
        if (itemsToggle != null) Highlight(itemsToggle.GetComponentInChildren<TextMeshProUGUI>(true), itemsToggle, sel);
        if (hatToggle != null) Highlight(hatToggle.GetComponentInChildren<TextMeshProUGUI>(true), hatToggle, sel);
        if (resetBtn != null) Highlight(resetBtn.GetComponentInChildren<TextMeshProUGUI>(true), resetBtn, sel);
        foreach (var b in MenuHub.ChooserButtons)
            if (b != null) Highlight(b.GetComponentInChildren<TextMeshProUGUI>(true), b, sel);
    }

    static readonly Dictionary<int, FontStyles> baseStyle = new();

    static void Highlight(TextMeshProUGUI t, Selectable s, GameObject sel)
    {
        if (t == null || s == null || !s.gameObject.activeInHierarchy) return;
        bool on = sel != null && sel == s.gameObject;
        t.color = MenuColor(on);
        // keep the text's own style (the game's buttons are bold caps), only add the underline
        int id = t.GetInstanceID();
        if (!baseStyle.TryGetValue(id, out var st)) { st = t.fontStyle & ~FontStyles.Underline; baseStyle[id] = st; }
        t.fontStyle = on ? st | FontStyles.Underline : st;
    }

    // the game's menu text colour: its buttons' normal (pale) or selected tint
    static Color MenuColor(bool selected)
    {
        var m = Il2Cpp.Menu.me;
        TMP_Text src = m != null ? m.ContinueGameButtonText : null;
        var c = src != null ? src.color : Color.white;
        var btn = m != null ? m.ContinueGameButton : null;
        if (btn != null) { var cb = btn.colors; c *= (selected ? cb.selectedColor : cb.normalColor) * cb.colorMultiplier; c.a = Mathf.Min(c.a, 1f); }
        return c;
    }

    // the library makes text as big as the menu's buttons, in white: smaller, in the menu's colour
    const float Scale = 0.5f;
    static readonly HashSet<int> styled = new();

    static void Style(TMP_Text t, float scale)
    {
        if (t == null || !styled.Add(t.GetInstanceID())) return;
        var m = Il2Cpp.Menu.me;
        TMP_Text src = m != null ? m.ContinueGameButtonText : null;
        if (src != null) t.fontSize = src.fontSize * scale; else t.fontSize *= scale;
        t.enableAutoSizing = false;
        t.richText = true;
        t.color = MenuColor(false);
    }
}

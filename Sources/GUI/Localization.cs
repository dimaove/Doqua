using System.Globalization;
using System.Text.Json;

namespace Doqua.GUI;

/// <summary>
/// Texts shown by Doqua and by the application, in the current <see cref="Culture"/> (English by default).
/// <list type="bullet">
/// <item><see cref="Get"/> returns a text for a key: the translation for the culture ("ru-RU"), else for its
/// language ("ru"), else Doqua's English default (<see cref="DefaultTexts"/>), else the key itself. So an
/// application may use the English text itself as the key: <c>Localization.Get("Hello")</c>.</item>
/// <item>Translations come from code (<see cref="Add(string, string, string)"/>) or from JSON files with one
/// flat object of key / text pairs (<see cref="LoadJson(string, string)"/>).</item>
/// <item><see cref="Culture"/> also gives month and weekday names and date formats (e.g. in DateInput).
/// Changing it redraws all windows and raises <see cref="CultureChanged"/>, where the application updates
/// the texts it has set on its controls.</item>
/// </list>
/// Doqua's own keys start with "Doqua."; exception messages are translated too. Use it from the UI thread.
/// </summary>
public static class Localization
{
    private static readonly Dictionary<string, string> s_defaults = new()
    {
        // Text editing menu (Input, TextArea, NumberInput).
        ["Doqua.Menu.Cut"] = "Cut",
        ["Doqua.Menu.Copy"] = "Copy",
        ["Doqua.Menu.Paste"] = "Paste",
        ["Doqua.Menu.SelectAll"] = "Select all",
        // DateInput calendar; {0} is today's date (short format).
        ["Doqua.Calendar.Today"] = "Today: {0}",
        // MessageBox buttons.
        ["Doqua.MessageBox.Ok"] = "OK",
        ["Doqua.MessageBox.Cancel"] = "Cancel",
        ["Doqua.MessageBox.Yes"] = "Yes",
        ["Doqua.MessageBox.No"] = "No",
        // FileInput: browser, filter, and the messages of FileInput.Error ({0}: the file masks).
        ["Doqua.FileInput.Select"] = "Select",
        ["Doqua.FileInput.Cancel"] = "Cancel",
        ["Doqua.FileInput.Computer"] = "Computer",
        ["Doqua.FileInput.Empty"] = "(empty)",
        ["Doqua.FileInput.CannotRead"] = "This folder cannot be read.",
        ["Doqua.FileInput.AllFiles"] = "All files",
        ["Doqua.FileInput.FileRequired"] = "A file must be specified.",
        ["Doqua.FileInput.FolderRequired"] = "A folder must be specified.",
        ["Doqua.FileInput.InvalidPath"] = "The path is not valid.",
        ["Doqua.FileInput.FileNotFound"] = "The file does not exist.",
        ["Doqua.FileInput.FolderNotFound"] = "The folder does not exist.",
        ["Doqua.FileInput.FolderOfFileNotFound"] = "The folder of the file does not exist.",
        ["Doqua.FileInput.IsFolder"] = "This is a folder, not a file.",
        ["Doqua.FileInput.IsFile"] = "This is a file, not a folder.",
        ["Doqua.FileInput.FilterMismatch"] = "The file name must match {0}.",

        // Exception messages.
        ["Doqua.Error.ControlHasParent"] = "The control already has a parent.",
        ["Doqua.Error.ControlInsideItself"] = "A control cannot be added to itself or its descendant.",
        ["Doqua.Error.CannotFocus"] = "The control cannot receive focus: it must be focusable, visible, enabled and in this window.",
        ["Doqua.Error.NotInWindow"] = "The control is not shown in a window.",
        ["Doqua.Error.IconsContainNull"] = "Icons cannot contain null.",
        ["Doqua.Error.AlreadyRunning"] = "Application is already running.",
        ["Doqua.Error.UnsupportedPlatform"] = "Doqua supports only Windows and Linux.",
        ["Doqua.Error.PageMustBeSelected"] = "A page must be selected while any page is enabled.",
        ["Doqua.Error.PageNotInTabControl"] = "The page does not belong to this TabControl.",
        ["Doqua.Error.DisabledPage"] = "A disabled page cannot be selected.",
        ["Doqua.Error.NoRadioButtonWithValue"] = "No radio button in the group has the value '{0}'.",
        ["Doqua.Error.ItemNotInList"] = "The item '{0}' is not in the list.",
        ["Doqua.Error.NodeNotInTree"] = "The node does not belong to this tree view.",
        ["Doqua.Error.NodeHasParent"] = "The node already belongs to a tree.",
        ["Doqua.Error.ModalOwner"] = "ShowModal needs another window as the owner and can be called only once per window.",
        ["Doqua.Error.RowSelectionOff"] = "Rows can be selected only while RowSelection is on.",
        ["Doqua.Error.FilterPatterns"] = "A file filter needs at least one non-empty pattern.",
        ["Doqua.Error.NodeInsideItself"] = "A node cannot be added to itself or its descendant.",
        ["Doqua.Error.FontSize"] = "Font size must be a positive number.",
        ["Doqua.Error.BitmapTooLarge"] = "The bitmap is too large.",
        ["Doqua.Error.BitmapSource"] = "The source rectangle must be non-empty and inside the bitmap.",
        ["Doqua.Error.PngTruncated"] = "The PNG data is truncated.",
        ["Doqua.Error.NotPng"] = "Not a PNG file.",
        ["Doqua.Error.PngChunkLength"] = "Invalid PNG chunk length.",
        ["Doqua.Error.PngNoHeader"] = "The PNG has no IHDR chunk.",
        ["Doqua.Error.PngNoPalette"] = "The PNG has a palette color type but no PLTE chunk.",
        ["Doqua.Error.PngFilter"] = "Unknown PNG filter type {0}.",
        ["Doqua.Error.PngHeader"] = "Invalid PNG IHDR chunk.",
        ["Doqua.Error.PngSize"] = "Unsupported PNG size {0} x {1}.",
        ["Doqua.Error.PngColorType"] = "Invalid PNG color type {0} with bit depth {1}.",
        ["Doqua.Error.PngMethod"] = "Unsupported PNG compression, filter or interlace method.",
        ["Doqua.Error.PngPaletteIndex"] = "PNG palette index out of range.",
        ["Doqua.Error.X11Display"] = "Cannot open X11 display. Is the DISPLAY environment variable set?",
        ["Doqua.Error.X11Depth"] = "X11 display depth {0} is not supported (24 or 32 required).",
        ["Doqua.Error.FreeType64Bit"] = "The FreeType backend supports only 64-bit processes.",
        ["Doqua.Error.FreeTypeInit"] = "FT_Init_FreeType failed with error {0}.",
        ["Doqua.Error.FontconfigInit"] = "Cannot initialize fontconfig.",
        ["Doqua.Error.NoFont"] = "No font found for '{0}'.",
        ["Doqua.Error.NoFontFile"] = "No font file found for '{0}'.",
        ["Doqua.Error.OpenFont"] = "Cannot open font '{0}' (FreeType error {1}).",
        ["Doqua.Error.SetFontSize"] = "Cannot set size {0} for font '{1}' (FreeType error {2}).",
        ["Doqua.Error.GdiFont"] = "Cannot create GDI font '{0}'.",
        ["Doqua.Error.InvalidTranslationFile"] = "The translation file must contain one JSON object with string values.",
    };

    // Culture or language name ("ru-RU", "ru") -> key -> text.
    private static readonly Dictionary<string, Dictionary<string, string>> s_translations = new(StringComparer.OrdinalIgnoreCase);

    private static CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Raised after <see cref="Culture"/> changes (all windows are already scheduled for redrawing).</summary>
    public static event EventHandler? CultureChanged;

    /// <summary>Language of the texts and culture for dates and numbers shown by Doqua; English (en-US) by default.</summary>
    public static CultureInfo Culture
    {
        get => s_culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Equals(s_culture))
                return;
            s_culture = value;
            Window.InvalidateAll();
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Doqua's own keys with their English texts: a starting point for a translation file.</summary>
    public static IReadOnlyDictionary<string, string> DefaultTexts => s_defaults;

    /// <summary>The text for <paramref name="key"/> in the current culture (see the class description for the fallbacks).</summary>
    public static string Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if ((Lookup(s_culture.Name, key) ?? Lookup(s_culture.TwoLetterISOLanguageName, key)) is { } text)
            return text;
        return s_defaults.TryGetValue(key, out var english) ? english : key;
    }

    /// <summary><see cref="Get"/> with {0}, {1}... replaced by <paramref name="args"/>, formatted for the current culture.</summary>
    public static string Format(string key, params object?[] args) => string.Format(s_culture, Get(key), args);

    /// <summary>Adds or replaces one translation, e.g. <c>Add("ru", "Doqua.Menu.Copy", "Копировать")</c>.</summary>
    public static void Add(string culture, string key, string text)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(text);
        if (!s_translations.TryGetValue(culture, out var texts))
            s_translations[culture] = texts = new Dictionary<string, string>();
        texts[key] = text;
        Window.InvalidateAll();
    }

    /// <summary>Adds or replaces several translations for <paramref name="culture"/>.</summary>
    public static void Add(string culture, IEnumerable<KeyValuePair<string, string>> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        foreach (var (key, text) in texts)
            Add(culture, key, text);
    }

    /// <summary>
    /// Loads translations for <paramref name="culture"/> ("ru" or "ru-RU") from a JSON file holding one object:
    /// <c>{ "Doqua.Menu.Copy": "Копировать", "Hello": "Привет" }</c>.
    /// </summary>
    public static void LoadJson(string culture, string path)
    {
        using var stream = File.OpenRead(path);
        LoadJson(culture, stream);
    }

    /// <inheritdoc cref="LoadJson(string, string)"/>
    public static void LoadJson(string culture, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(Get("Doqua.Error.InvalidTranslationFile"));
        var texts = new List<KeyValuePair<string, string>>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException(Get("Doqua.Error.InvalidTranslationFile"));
            texts.Add(new(property.Name, property.Value.GetString()!));
        }
        Add(culture, texts);
    }

    private static string? Lookup(string culture, string key) =>
        culture.Length > 0 && s_translations.TryGetValue(culture, out var texts) && texts.TryGetValue(key, out var text) ? text : null;
}

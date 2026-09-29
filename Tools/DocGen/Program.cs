using DocGen;

// Generates Docs/html from doqua.dll, its XML documentation, Tools/DocGen/Pages and the Examples.
// Usage (from anywhere in the repository): dotnet run --project Tools/DocGen [output folder]

var root = FindRepositoryRoot(AppContext.BaseDirectory);
var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(root, "Docs", "html");
var assembly = typeof(Doqua.GUI.Window).Assembly;
var xml = Path.ChangeExtension(assembly.Location, ".xml");
if (!File.Exists(xml))
{
    Console.Error.WriteLine($"XML documentation not found: {xml} (GenerateDocumentationFile must be on in Doqua.csproj)");
    return 1;
}

List<ExampleInfo> examples =
[
    new("Simple", "Simple", "A menu bar (File, View, Help) with Alt keys and shortcuts, anchored panels with clickable rectangles, buttons, an input, a live clock and a status line.", ["simple.png"], []),
    new("Tabs", "Tabs", "A TabControl with pages for text, shapes, check boxes, radio groups in group boxes and scrolling panels, plus context menus.",
        ["tabs-general.png", "tabs-settings.png", "tabs-scrolling.png"], []),
    new("Dictionary", "Dictionary", "A TreeView and a Table in a SplitContainer: each node keeps its own key/value pairs and cell colors; rows can be selected, edited and removed (with a MessageBox asking first).",
        ["dictionary.png"], []),
    new("Editor", "Editor", "A multi-line TextArea with scroll bars and a caret position status line.", ["editor.png"], []),
    new("DateTime", "DateTime", "Two DateInputs whose ranges limit each other, and a date format selector.", ["datetime.png"], []),
    new("Files", "Files", "FileInputs with different options: required, file masks, new files, folders and relative paths, with their validation messages.",
        ["files.png", "files-browser.png"], []),
    new("Fonts", "Fonts", "All installed font families in a ComboBox, with size and style controls and a live preview.", ["fonts.png"], []),
    new("Localization", "Localization", "Switching the whole user interface between English and Russian at run time.",
        ["localization-en.png", "localization-ru.png"], ["ru.json"]),
    new("Threads", "Threads", "A background reader posting values to the GUI, an async button that awaits work on the thread pool, Progress<T>, and the error for a control touched from another thread.",
        ["threads.png"], []),
    new("Bitmap", "Bitmap", "Loading, drawing, scaling and saving PNG images, and drawing into a bitmap.", ["bitmap.png"], []),
    new("Clock", "Clock", "A custom control drawing an analog clock with lines and ellipses, redrawn every second.", ["clock.png"], []),
];

new Site(assembly, new XmlDocs(xml), root, output, examples).Generate();
Console.WriteLine($"Documentation written to {output}");
return 0;

static string FindRepositoryRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "Sources")) && Directory.Exists(Path.Combine(dir.FullName, "Examples")))
            return dir.FullName;
    }
    throw new DirectoryNotFoundException("Run the generator inside the Doqua repository.");
}

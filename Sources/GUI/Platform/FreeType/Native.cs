using System.Runtime.InteropServices;

namespace Doqua.GUI.Platform.FreeType;

// Only the fields Doqua reads are mapped. Offsets are for LP64 (64-bit Linux), where
// FT_Long, FT_Pos and FT_Fixed are 64-bit. These public structs are part of the stable FreeType 2 ABI.

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct FT_FaceRec
{
    [FieldOffset(152)] public FT_GlyphSlotRec* glyph;
    [FieldOffset(160)] public FT_SizeRec* size;
}

/// <summary>FT_SizeRec with its FT_Size_Metrics; values are 26.6 fixed point.</summary>
[StructLayout(LayoutKind.Explicit)]
internal struct FT_SizeRec
{
    [FieldOffset(48)] public nint ascender;
    [FieldOffset(56)] public nint descender;
    [FieldOffset(64)] public nint height;
}

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct FT_GlyphSlotRec
{
    [FieldOffset(128)] public nint advanceX; // 26.6

    // FT_Bitmap bitmap
    [FieldOffset(152)] public uint bitmapRows;
    [FieldOffset(156)] public uint bitmapWidth;
    [FieldOffset(160)] public int bitmapPitch;
    [FieldOffset(168)] public byte* bitmapBuffer;
    [FieldOffset(178)] public byte bitmapPixelMode;

    [FieldOffset(192)] public int bitmapLeft;
    [FieldOffset(196)] public int bitmapTop;
}

internal static unsafe partial class FT
{
    private const string Lib = "libfreetype.so.6";

    public const int LOAD_RENDER = 1 << 2;
    public const int LOAD_TARGET_LIGHT = 1 << 16; // Light hinting: vertical only, keeps glyph shapes.

    public const byte PIXEL_MODE_MONO = 1;
    public const byte PIXEL_MODE_GRAY = 2;

    [LibraryImport(Lib)]
    public static partial int FT_Init_FreeType(nint* library);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int FT_New_Face(nint library, string path, nint faceIndex, FT_FaceRec** face);

    [LibraryImport(Lib)]
    public static partial int FT_Set_Char_Size(FT_FaceRec* face, nint width, nint height, uint hres, uint vres);

    [LibraryImport(Lib)]
    public static partial int FT_Load_Char(FT_FaceRec* face, nuint charCode, int loadFlags);
}

internal static unsafe partial class Fc
{
    private const string Lib = "libfontconfig.so.1";

    public const int MatchPattern = 0;
    public const int ResultMatch = 0;

    public const int WEIGHT_REGULAR = 80;
    public const int WEIGHT_BOLD = 200;
    public const int SLANT_ROMAN = 0;
    public const int SLANT_ITALIC = 100;

    [LibraryImport(Lib)]
    public static partial int FcInit();

    [LibraryImport(Lib)]
    public static partial nint FcPatternCreate();

    [LibraryImport(Lib)]
    public static partial void FcPatternDestroy(nint pattern);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int FcPatternAddString(nint pattern, string obj, string value);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int FcPatternAddInteger(nint pattern, string obj, int value);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int FcPatternGetString(nint pattern, string obj, int n, byte** value);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int FcPatternGetInteger(nint pattern, string obj, int n, int* value);

    [LibraryImport(Lib)]
    public static partial int FcConfigSubstitute(nint config, nint pattern, int kind);

    [LibraryImport(Lib)]
    public static partial void FcDefaultSubstitute(nint pattern);

    [LibraryImport(Lib)]
    public static partial nint FcFontMatch(nint config, nint pattern, int* result);
}

namespace Doqua.GUI;

/// <summary>Window pixels: one uint per pixel as 0x00RRGGBB, rows from top to bottom.</summary>
internal sealed class Framebuffer
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public uint[] Pixels { get; private set; } = [];

    public void Resize(int width, int height)
    {
        if (width == Width && height == Height)
            return;
        Width = width;
        Height = height;
        Pixels = new uint[width * height];
    }
}

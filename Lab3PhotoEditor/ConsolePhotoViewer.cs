using System.Drawing;

namespace Lab3PhotoEditor;

public static class ConsolePhotoViewer
{
    private static readonly (ConsoleColor Key, Color Color)[] Palette =
    [ (ConsoleColor.Black, Color.Black), (ConsoleColor.DarkBlue, Color.Navy),
      (ConsoleColor.DarkGreen, Color.Green), (ConsoleColor.DarkCyan, Color.Teal),
      (ConsoleColor.DarkRed, Color.Maroon), (ConsoleColor.DarkMagenta, Color.Purple),
      (ConsoleColor.DarkYellow, Color.Olive), (ConsoleColor.Gray, Color.Silver),
      (ConsoleColor.DarkGray, Color.Gray), (ConsoleColor.Blue, Color.Blue),
      (ConsoleColor.Green, Color.Lime), (ConsoleColor.Cyan, Color.Cyan),
      (ConsoleColor.Red, Color.Red), (ConsoleColor.Magenta, Color.Magenta),
      (ConsoleColor.Yellow, Color.Yellow), (ConsoleColor.White, Color.White) ];

    public static void Show(PhotoDocument photo)
    {
        using var image = ImageProcessor.Decode(photo.ImageData);
        Console.WriteLine($"\n{photo.Name}: {image.Width} × {image.Height} пикселей");
        int width = Math.Min(64, image.Width);
        if (!Console.IsOutputRedirected) width = Math.Min(width, Math.Max(1, Console.WindowWidth - 2));
        int height = Math.Max(1, Math.Min(24, (int)Math.Round(image.Height * (double)width / image.Width / 2)));
        const string shades = " .:-=+*#%@";
        ConsoleColor previous = Console.ForegroundColor;
        try
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = image.GetPixel(x * image.Width / width, y * image.Height / height);
                    if (Console.IsOutputRedirected)
                        Console.Write(shades[(int)((.299 * pixel.R + .587 * pixel.G + .114 * pixel.B) / 255 * (shades.Length - 1))]);
                    else
                    {
                        Console.ForegroundColor = Palette.MinBy(p =>
                            Math.Pow(p.Color.R - pixel.R, 2) + Math.Pow(p.Color.G - pixel.G, 2) + Math.Pow(p.Color.B - pixel.B, 2)).Key;
                        Console.Write('█');
                    }
                }
                Console.WriteLine();
            }
        }
        finally { if (!Console.IsOutputRedirected) Console.ForegroundColor = previous; }
        Console.WriteLine("Миниатюра терминала; для просмотра всех деталей экспортируйте изображение.");
    }
}

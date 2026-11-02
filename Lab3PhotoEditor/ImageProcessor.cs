using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Lab3PhotoEditor;

public sealed class ImageProcessor : IImageProcessor
{
    public const int MaxPixels = 24_000_000;

    public static void CheckSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels)
            throw new ArgumentOutOfRangeException(nameof(width), "Размер должен быть положительным, не более 24 млн пикселей.");
    }

    public static Bitmap Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0) throw new ArgumentException("Изображение пустое.");
        using var stream = new MemoryStream(data);
        using var image = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
        CheckSize(image.Width, image.Height);
        // Создаём независимую копию, не связанную с потоком или исходным файлом.
        var result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(result);
            graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    public static byte[] Encode(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    public byte[] Crop(byte[] source, int x, int y, int width, int height)
    {
        using var image = Decode(source);
        CheckSize(width, height);
        if (x < 0 || y < 0 || (long)x + width > image.Width || (long)y + height > image.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Прямоугольник обрезки должен находиться внутри изображения.");
        using var result = image.Clone(new Rectangle(x, y, width, height), PixelFormat.Format32bppArgb);
        return Encode(result);
    }

    public byte[] Rotate(byte[] source, int degrees)
    {
        if (degrees % 90 != 0) throw new ArgumentException("Допустимы углы, кратные 90 градусам.");
        using var image = Decode(source);
        int angle = (degrees % 360 + 360) % 360;
        image.RotateFlip(angle switch
        { 90 => RotateFlipType.Rotate90FlipNone, 180 => RotateFlipType.Rotate180FlipNone,
            270 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone });
        return Encode(image);
    }

    public byte[] SetPixel(byte[] source, int x, int y, Color color)
    {
        using var image = Decode(source);
        CheckPoint(image, x, y);
        image.SetPixel(x, y, color);
        return Encode(image);
    }

    public byte[] AddText(byte[] source, string text, int x, int y, float size, Color color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!float.IsFinite(size) || size < 1 || size > 500) throw new ArgumentOutOfRangeException(nameof(size));
        using var image = Decode(source);
        CheckPoint(image, x, y);
        using var graphics = Graphics.FromImage(image);
        using var font = new Font(FontFamily.GenericSansSerif, size, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(text, font, brush, x, y);
        return Encode(image);
    }

    public byte[] ApplyFilter(byte[] source, PhotoFilter filter, int value = 0, int green = 0, int blue = 0)
    {
        if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
        if (filter == PhotoFilter.Brightness && (value < -100 || value > 100))
            throw new ArgumentOutOfRangeException(nameof(value), "Яркость задаётся от -100 до 100 процентов.");
        if (filter == PhotoFilter.ColorBalance && new[] { value, green, blue }.Any(channel => channel < -255 || channel > 255))
            throw new ArgumentOutOfRangeException(nameof(value), "Сдвиг каналов задаётся от -255 до 255.");
        using var image = Decode(source);
        var matrix = new ColorMatrix();
        switch (filter)
        {
            case PhotoFilter.Brightness:
                matrix.Matrix40 = matrix.Matrix41 = matrix.Matrix42 = value / 100f; break;
            case PhotoFilter.Invert:
                matrix.Matrix00 = matrix.Matrix11 = matrix.Matrix22 = -1;
                matrix.Matrix40 = matrix.Matrix41 = matrix.Matrix42 = 1; break;
            case PhotoFilter.Grayscale:
                matrix = new ColorMatrix(new[] {
                    new[] { .299f, .299f, .299f, 0, 0 }, new[] { .587f, .587f, .587f, 0, 0 },
                    new[] { .114f, .114f, .114f, 0, 0 }, new[] { 0f, 0, 0, 1, 0 }, new[] { 0f, 0, 0, 0, 1 } }); break;
            case PhotoFilter.Sepia:
                matrix = new ColorMatrix(new[] {
                    new[] { .393f, .349f, .272f, 0, 0 }, new[] { .769f, .686f, .534f, 0, 0 },
                    new[] { .189f, .168f, .131f, 0, 0 }, new[] { 0f, 0, 0, 1, 0 }, new[] { 0f, 0, 0, 0, 1 } }); break;
            case PhotoFilter.ColorBalance:
                matrix.Matrix40 = value / 255f; matrix.Matrix41 = green / 255f; matrix.Matrix42 = blue / 255f; break;
        }
        using var result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height), 0, 0,
                image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
        return Encode(result);
    }

    public byte[] Collage(IReadOnlyList<byte[]> sources, int columns, int cellWidth, int cellHeight)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0 || columns < 1 || columns > sources.Count) throw new ArgumentException("Укажите фотографии и число столбцов от 1 до числа фото.");
        CheckSize(cellWidth, cellHeight);
        int rows = (sources.Count - 1) / columns + 1;
        int width = checked(columns * cellWidth), height = checked(rows * cellHeight);
        CheckSize(width, height);
        using var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            for (int i = 0; i < sources.Count; i++)
            {
                using var image = Decode(sources[i]);
                double scale = Math.Min((double)cellWidth / image.Width, (double)cellHeight / image.Height);
                int w = Math.Max(1, (int)(image.Width * scale)), h = Math.Max(1, (int)(image.Height * scale));
                int x = i % columns * cellWidth + (cellWidth - w) / 2;
                int y = i / columns * cellHeight + (cellHeight - h) / 2;
                graphics.DrawImage(image, new Rectangle(x, y, w, h));
            }
        }
        return Encode(result);
    }

    public static void Export(byte[] source, string path)
    {
        using var image = Decode(source);
        ImageFormat format = Path.GetExtension(path).ToLowerInvariant() switch
        { ".png" => ImageFormat.Png, ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp, _ => throw new ArgumentException("Используйте расширение .png, .jpg, .jpeg или .bmp.") };
        // JPEG не поддерживает прозрачность: используем белый фон.
        using var flattened = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
        if (format.Guid == ImageFormat.Jpeg.Guid)
        {
            using var g = Graphics.FromImage(flattened); g.Clear(Color.White); g.DrawImageUnscaled(image, 0, 0);
        }
        using var stream = new MemoryStream();
        (format.Guid == ImageFormat.Jpeg.Guid ? flattened : image).Save(stream, format);
        File.WriteAllBytes(path, stream.ToArray());
    }

    private static void CheckPoint(Bitmap image, int x, int y)
    {
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Координаты пикселя вне изображения.");
    }
}

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Lab3PhotoEditor;

// Обработка растровых изображений. using освобождает Bitmap, Graphics, кисти и потоки.
public sealed class ImageProcessor : IImageProcessor
{
    // Ограничиваем площадь изображения, чтобы не создавать чрезмерно большие холсты.
    public const int MaxPixels = 24_000_000;

    // Приведение к long предотвращает переполнение при проверке произведения размеров.
    public static void CheckSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels)
            throw new ArgumentOutOfRangeException(nameof(width), "Размер должен быть положительным, не более 24 млн пикселей.");
    }

    // Загружаем изображение из байтов. Полученный Bitmap должен освободить вызывающий код.
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

    // Единый внутренний формат PNG не вносит потерь от повторного сжатия JPEG.
    public static byte[] Encode(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    // Проверяем прямоугольник и копируем только выбранные пиксели в новое изображение.
    public byte[] Crop(byte[] source, int x, int y, int width, int height)
    {
        using var image = Decode(source);
        CheckSize(width, height);
        if (x < 0 || y < 0 || (long)x + width > image.Width || (long)y + height > image.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Прямоугольник обрезки должен находиться внутри изображения.");
        using var result = image.Clone(new Rectangle(x, y, width, height), PixelFormat.Format32bppArgb);
        return Encode(result);
    }

    // Поворот на 90 или 270 градусов автоматически меняет местами ширину и высоту.
    public byte[] Rotate(byte[] source, int degrees)
    {
        if (degrees % 90 != 0) throw new ArgumentException("Допустимы углы, кратные 90 градусам.");
        using var image = Decode(source);
        // Нормализуем отрицательные углы: например, -90 становится 270.
        int angle = (degrees % 360 + 360) % 360;
        image.RotateFlip(angle switch
        { 90 => RotateFlipType.Rotate90FlipNone, 180 => RotateFlipType.Rotate180FlipNone,
            270 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone });
        return Encode(image);
    }

    // Изменение выполняется в декодированной копии, исходные байты остаются прежними.
    public byte[] SetPixel(byte[] source, int x, int y, Color color)
    {
        using var image = Decode(source);
        CheckPoint(image, x, y);
        image.SetPixel(x, y, color);
        return Encode(image);
    }

    // Размер шрифта задаётся в пикселях; часть текста за границами холста обрезается.
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

    // Преобразуем каналы RGBA цветовой матрицей и возвращаем результат отдельным PNG.
    public byte[] ApplyFilter(byte[] source, PhotoFilter filter, int value = 0, int green = 0, int blue = 0)
    {
        if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
        if (filter == PhotoFilter.Brightness && (value < -100 || value > 100))
            throw new ArgumentOutOfRangeException(nameof(value), "Яркость задаётся от -100 до 100 процентов.");
        if (filter == PhotoFilter.ColorBalance && new[] { value, green, blue }.Any(channel => channel < -255 || channel > 255))
            throw new ArgumentOutOfRangeException(nameof(value), "Сдвиг каналов задаётся от -255 до 255.");
        using var image = Decode(source);
        // Начальная матрица единичная: до изменений сохраняет исходные цвета и прозрачность.
        var matrix = new ColorMatrix();
        switch (filter)
        {
            // Прибавляем одинаковую долю полного диапазона ко всем трём цветовым каналам.
            case PhotoFilter.Brightness:
                matrix.Matrix40 = matrix.Matrix41 = matrix.Matrix42 = value / 100f; break;
            // Негатив: для нормализованного канала c вычисляем 1 - c.
            case PhotoFilter.Invert:
                matrix.Matrix00 = matrix.Matrix11 = matrix.Matrix22 = -1;
                matrix.Matrix40 = matrix.Matrix41 = matrix.Matrix42 = 1; break;
            // Общая яркость равна 0,299R + 0,587G + 0,114B; она записывается во все каналы.
            case PhotoFilter.Grayscale:
                matrix = new ColorMatrix(new[] {
                    new[] { .299f, .299f, .299f, 0, 0 }, new[] { .587f, .587f, .587f, 0, 0 },
                    new[] { .114f, .114f, .114f, 0, 0 }, new[] { 0f, 0, 0, 1, 0 }, new[] { 0f, 0, 0, 0, 1 } }); break;
            // Взвешенное смешивание каналов создаёт тёплый коричневый оттенок.
            case PhotoFilter.Sepia:
                matrix = new ColorMatrix(new[] {
                    new[] { .393f, .349f, .272f, 0, 0 }, new[] { .769f, .686f, .534f, 0, 0 },
                    new[] { .189f, .168f, .131f, 0, 0 }, new[] { 0f, 0, 0, 1, 0 }, new[] { 0f, 0, 0, 0, 1 } }); break;
            // Независимые сдвиги R, G и B переводим из диапазона байта в нормализованный диапазон.
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

    // Фото заполняют ячейки целиком. Лишние края обрезаются, геометрические пропорции сохраняются.
    public byte[] Collage(IReadOnlyList<byte[]> sources, int columns, int cellWidth, int cellHeight)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0 || columns < 1 || columns > sources.Count)
            throw new ArgumentException("Укажите фотографии и число столбцов от 1 до числа фото.");
        CheckSize(cellWidth, cellHeight);
        // Округление вверх учитывает неполный последний ряд.
        int rows = (sources.Count - 1) / columns + 1;
        // checked не позволяет переполнению размеров дать некорректный размер холста.
        int width = checked(columns * cellWidth), height = checked(rows * cellHeight);
        CheckSize(width, height);
        using var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(result))
        using (var attributes = new ImageAttributes())
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            // Продолжаем крайние пиксели при интерполяции, избегая прозрачных швов.
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            for (int i = 0; i < sources.Count; i++)
            {
                using var image = Decode(sources[i]);
                int row = i / columns, column = i % columns;
                int countInRow = Math.Min(columns, sources.Count - row * columns);
                // В последнем ряду оставшиеся фотографии делят всю ширину.
                // Общие границы ячеек распределяют остаток деления ширины без однопиксельных щелей.
                int left = (int)((long)column * width / countInRow);
                int right = (int)((long)(column + 1) * width / countInRow);
                var destination = new Rectangle(left, row * cellHeight, right - left, cellHeight);
                // Максимум коэффициентов обеспечивает заполнение всей ячейки без полей.
                double scale = Math.Max((double)destination.Width / image.Width,
                    (double)destination.Height / image.Height);
                // Определяем видимую область исходника при выбранном масштабе.
                float sourceWidth = (float)(destination.Width / scale);
                float sourceHeight = (float)(destination.Height / scale);
                // Сдвигаем область к центру, симметрично убирая лишние края.
                float sourceX = (image.Width - sourceWidth) / 2;
                float sourceY = (image.Height - sourceHeight) / 2;
                graphics.DrawImage(image, destination, sourceX, sourceY, sourceWidth, sourceHeight,
                    GraphicsUnit.Pixel, attributes);
            }
        }
        return Encode(result);
    }

    // Формат файла определяется расширением. Обработка и экспорт отделены от сохранения проекта.
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

    // Допустимые координаты: от 0 включительно до ширины или высоты не включительно.
    private static void CheckPoint(Bitmap image, int x, int y)
    {
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Координаты пикселя вне изображения.");
    }
}

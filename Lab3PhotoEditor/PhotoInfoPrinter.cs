namespace Lab3PhotoEditor;

// Показывает сведения о документе, не изменяя его и не рисуя миниатюру.
public static class PhotoInfoPrinter
{
    // Размеры считываются из изображения, объём — из массива PNG, хранящегося в проекте.
    public static void Show(PhotoDocument photo)
    {
        using var image = ImageProcessor.Decode(photo.ImageData);
        Console.WriteLine($"\nНазвание: {photo.Name}");
        Console.WriteLine($"Размер: {image.Width} × {image.Height} пикселей");
        Console.WriteLine($"Данные PNG: {photo.ImageData.Length:N0} байт");
        Console.WriteLine("Для просмотра экспортируйте фото пунктом 10 и откройте полученный файл.");
    }
}

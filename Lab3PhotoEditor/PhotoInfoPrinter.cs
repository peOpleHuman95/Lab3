namespace Lab3PhotoEditor;

public static class PhotoInfoPrinter
{
    public static void Show(PhotoDocument photo)
    {
        using var image = ImageProcessor.Decode(photo.ImageData);
        Console.WriteLine($"\nНазвание: {photo.Name}");
        Console.WriteLine($"Размер: {image.Width} × {image.Height} пикселей");
        Console.WriteLine($"Данные PNG: {photo.ImageData.Length:N0} байт");
        Console.WriteLine("Для просмотра экспортируйте фото пунктом 10 и откройте полученный файл.");
    }
}

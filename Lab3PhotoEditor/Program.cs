using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Lab3PhotoEditor;

internal static class Program
{
    private static readonly IImageProcessor Processor = new ImageProcessor();
    private static readonly IProjectStorage Storage = new JsonProjectStorage();
    private static readonly string SavePath = Path.Combine(AppContext.BaseDirectory, "saves", "photo-project.json");
    private static PhotoProject project = new();

    private static void Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        while (true)
        {
            Console.WriteLine("\nЛАБОРАТОРНАЯ РАБОТА № 3 — ВАРИАНТ 1. ФОТОРЕДАКТОР");
            Console.WriteLine(project.Photos.Count == 0 ? "Проект пуст." : $"Фотографий: {project.Photos.Count}. Выбрана: {project.Current.Name}");
            Console.WriteLine("1. Добавить фото из файла\n2. Список и выбор фото\n3. Сведения о фото\n4. Обрезать фото\n5. Повернуть фото\n6. Добавить надпись\n7. Изменить пиксель\n8. Применить фильтр\n9. Создать коллаж\n10. Экспортировать фото\n11. Сохранить проект\n12. Загрузить проект\n13. Удалить выбранное фото\n14. Справка\n0. Сохранить и выйти");
            try
            {
                switch (ReadInt("Ваш выбор: ", 0, 14))
                {
                    case 0: if (Save()) return; break;
                    case 1: Import(); break;
                    case 2: Select(); break;
                    case 3: PhotoInfoPrinter.Show(project.Current); break;
                    case 4: Crop(); break;
                    case 5:
                        var photo = project.Current;
                        photo.ImageData = Processor.Rotate(photo.ImageData, ReadInt("Угол по часовой стрелке (кратно 90, отрицательный — против): ", -360, 360));
                        Console.WriteLine("Фото повёрнуто."); break;
                    case 6: Caption(); break;
                    case 7: Pixel(); break;
                    case 8: Filter(); break;
                    case 9: Collage(); break;
                    case 10: Export(); break;
                    case 11: Save(); break;
                    case 12:
                        if (Confirm("Несохранённые изменения будут заменены. Загрузить проект?"))
                        { project = Storage.Load(SavePath); Console.WriteLine("Проект восстановлен."); }
                        break;
                    case 13:
                        _ = project.Current;
                        if (Confirm("Удалить выбранное фото из проекта?")) project.RemoveCurrent();
                        break;
                    case 14: Help(); break;
                }
            }
            catch (EndOfStreamException) { Save(); return; }
            catch (Exception error) when (IsUserError(error)) { Console.WriteLine("Ошибка: " + error.Message); }
        }
    }

    private static void Import()
    {
        string path = ReadPath("Путь к фото (PNG, JPEG, BMP, GIF, TIFF): ");
        project.Add(Path.GetFileName(path), File.ReadAllBytes(path));
        Console.WriteLine("Фото добавлено. Оригинальный файл не изменяется.");
        PhotoInfoPrinter.Show(project.Current);
    }
    private static void Select()
    {
        ListPhotos();
        if (project.Photos.Count == 0) return;
        int number = ReadInt("Номер фото (0 — назад): ", 0, project.Photos.Count);
        if (number > 0) project.Select(number - 1);
    }
    private static void ListPhotos()
    {
        for (int i = 0; i < project.Photos.Count; i++)
        {
            using var image = ImageProcessor.Decode(project.Photos[i].ImageData);
            Console.WriteLine($"{i + 1}. {(i == project.SelectedIndex ? "[выбрано] " : "")}{project.Photos[i].Name} — {image.Width} × {image.Height}");
        }
        if (project.Photos.Count == 0) Console.WriteLine("Список пуст.");
    }
    private static void Crop()
    {
        var photo = project.Current;
        using var image = ImageProcessor.Decode(photo.ImageData);
        Console.WriteLine($"Размер: {image.Width} × {image.Height}. Координаты от нуля, левый верхний угол — (0, 0).");
        int x = ReadInt("X: ", 0, image.Width - 1), y = ReadInt("Y: ", 0, image.Height - 1);
        int width = ReadInt("Ширина: ", 1, image.Width - x), height = ReadInt("Высота: ", 1, image.Height - y);
        photo.ImageData = Processor.Crop(photo.ImageData, x, y, width, height);
        Console.WriteLine("Фото обрезано.");
    }
    private static void Caption()
    {
        var photo = project.Current;
        using var image = ImageProcessor.Decode(photo.ImageData);
        string text = ReadText("Текст: ");
        int x = ReadInt("X: ", 0, image.Width - 1), y = ReadInt("Y: ", 0, image.Height - 1);
        int size = ReadInt("Размер шрифта в пикселях: ", 1, 500);
        photo.ImageData = Processor.AddText(photo.ImageData, text, x, y, size, ReadColor());
        Console.WriteLine("Надпись добавлена; часть за границей фото обрезается.");
    }
    private static void Pixel()
    {
        var photo = project.Current;
        using var image = ImageProcessor.Decode(photo.ImageData);
        int x = ReadInt("X: ", 0, image.Width - 1), y = ReadInt("Y: ", 0, image.Height - 1);
        Color old = image.GetPixel(x, y);
        Console.WriteLine($"Текущий цвет: R={old.R}, G={old.G}, B={old.B}, A={old.A}.");
        photo.ImageData = Processor.SetPixel(photo.ImageData, x, y, ReadColor());
        Console.WriteLine("Пиксель изменён.");
    }
    private static void Filter()
    {
        var photo = project.Current;
        Console.WriteLine("1. Яркость\n2. Чёрно-белый\n3. Негатив\n4. Сепия\n5. Баланс RGB\n0. Назад");
        int choice = ReadInt("Фильтр: ", 0, 5);
        if (choice == 0) return;
        int value = 0, green = 0, blue = 0;
        PhotoFilter filter = choice switch
        { 1 => PhotoFilter.Brightness, 2 => PhotoFilter.Grayscale, 3 => PhotoFilter.Invert,
            4 => PhotoFilter.Sepia, _ => PhotoFilter.ColorBalance };
        if (choice == 1) value = ReadInt("Изменение яркости (-100..100 %): ", -100, 100);
        if (choice == 5)
        {
            value = ReadInt("Сдвиг R (-255..255): ", -255, 255);
            green = ReadInt("Сдвиг G (-255..255): ", -255, 255);
            blue = ReadInt("Сдвиг B (-255..255): ", -255, 255);
        }
        photo.ImageData = Processor.ApplyFilter(photo.ImageData, filter, value, green, blue);
        Console.WriteLine("Фильтр применён.");
    }
    private static void Collage()
    {
        _ = project.Current;
        ListPhotos();
        string[] parts = ReadText("Номера фото через пробел в нужном порядке (повторы разрешены): ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sources = new List<byte[]>();
        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int number) || number < 1 || number > project.Photos.Count)
                throw new ArgumentException("Неверный номер фото: " + part);
            sources.Add(project.Photos[number - 1].ImageData);
        }
        int columns = ReadInt("Столбцов: ", 1, sources.Count);
        int width = ReadInt("Ширина одной ячейки, пикс.: ", 1, 10000);
        int height = ReadInt("Высота одной ячейки, пикс.: ", 1, 10000);
        byte[] result = Processor.Collage(sources, columns, width, height);
        project.Add(ReadText("Название коллажа: "), result);
        Console.WriteLine("Коллаж добавлен отдельным фото. Исходные фотографии сохранены.");
    }
    private static void Export()
    {
        var photo = project.Current;
        string path = ReadPath("Куда экспортировать (.png, .jpg, .bmp): ");
        if (File.Exists(path) && !Confirm("Файл существует. Перезаписать?")) return;
        ImageProcessor.Export(photo.ImageData, path);
        Console.WriteLine("Фото экспортировано: " + Path.GetFullPath(path));
    }
    private static Color ReadColor() => Color.FromArgb(
        ReadInt("R (0..255): ", 0, 255), ReadInt("G (0..255): ", 0, 255), ReadInt("B (0..255): ", 0, 255));
    private static bool Save()
    {
        try { Storage.Save(SavePath, project); Console.WriteLine("Проект сохранён: " + SavePath); return true; }
        catch (Exception error) when (IsUserError(error)) { Console.WriteLine("Ошибка сохранения: " + error.Message); return false; }
    }
    private static bool Confirm(string prompt) => ReadText(prompt + " (да/нет): ").Equals("да", StringComparison.OrdinalIgnoreCase);
    private static string ReadPath(string prompt) => ReadText(prompt).Trim('"');
    private static string ReadText(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string text = Console.ReadLine() ?? throw new EndOfStreamException();
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
            Console.WriteLine("Введите непустую строку.");
        }
    }
    private static int ReadInt(string prompt, int min, int max)
    {
        while (true)
        {
            if (int.TryParse(ReadText(prompt), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= min && value <= max) return value;
            Console.WriteLine($"Введите целое число от {min} до {max}.");
        }
    }
    private static bool IsUserError(Exception error) => error is ArgumentException or IOException or
        UnauthorizedAccessException or JsonException or ExternalException or OverflowException or InvalidOperationException;
    private static void Help()
    {
        Console.WriteLine("Добавьте фотографии, выберите одну и применяйте операции через меню.");
        Console.WriteLine("Координаты начинаются с нуля в левом верхнем углу. Повороты кратны 90°.");
        Console.WriteLine("Исходные файлы не изменяются при обработке. Экспорт создаёт PNG/JPEG/BMP.");
        Console.WriteLine("Коллаж сохраняет пропорции фотографий, свободные участки заполняются белым.");
        Console.WriteLine("Для просмотра фото экспортируйте его пунктом 10 и откройте полученный файл.");
        Console.WriteLine("Сохранение проекта включает все фото, правки и выбранный номер. После перезапуска выберите 12.");
        Console.WriteLine("Пункт 0 сохраняет и завершает работу. Принудительное закрытие окна не сохраняет правки.");
    }
}

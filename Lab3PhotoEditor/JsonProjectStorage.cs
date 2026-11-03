using System.Text.Json;

namespace Lab3PhotoEditor;

// Хранилище всей сессии в JSON. Массивы ImageData сериализуются как Base64.
public sealed class JsonProjectStorage : IProjectStorage
{
    // Сначала проверяем проект; неверные данные не должны заменять корректное сохранение.
    public void Save(string path, PhotoProject project)
    {
        Validate(project);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        // Уникальный временный файл создаётся рядом с основным файлом сохранения.
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
            // Заменяем основной файл только после успешной записи полного JSON.
            File.Move(temporary, fullPath, overwrite: true);
        }
        // При ошибке или успешном завершении удаляем оставшийся временный файл.
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    // Восстанавливаем объекты из JSON, затем проверяем индексы, версию и данные изображений.
    public PhotoProject Load(string path)
    {
        var project = JsonSerializer.Deserialize<PhotoProject>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Пустой проект.");
        Validate(project);
        return project;
    }

    // Пустой проект допустим только с индексом -1; для непустого индекс должен существовать.
    public static void Validate(PhotoProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Version != 1 || project.Photos is null ||
            (project.Photos.Count == 0 ? project.SelectedIndex != -1 : project.SelectedIndex < 0 || project.SelectedIndex >= project.Photos.Count))
            throw new InvalidDataException("Несовместимая версия проекта или неверный выбранный индекс.");
        // Проверяем не только структуру JSON, но и возможность декодирования каждого фото.
        foreach (var photo in project.Photos)
        {
            if (photo is null || string.IsNullOrWhiteSpace(photo.Name)) throw new InvalidDataException("Некорректная фотография в проекте.");
            using var image = ImageProcessor.Decode(photo.ImageData);
        }
    }
}

using System.Text.Json;

namespace Lab3PhotoEditor;

public sealed class JsonProjectStorage : IProjectStorage
{
    public void Save(string path, PhotoProject project)
    {
        Validate(project);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public PhotoProject Load(string path)
    {
        var project = JsonSerializer.Deserialize<PhotoProject>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Пустой проект.");
        Validate(project);
        return project;
    }

    public static void Validate(PhotoProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Version != 1 || project.Photos is null ||
            (project.Photos.Count == 0 ? project.SelectedIndex != -1 : project.SelectedIndex < 0 || project.SelectedIndex >= project.Photos.Count))
            throw new InvalidDataException("Несовместимая версия проекта или неверный выбранный индекс.");
        foreach (var photo in project.Photos)
        {
            if (photo is null || string.IsNullOrWhiteSpace(photo.Name)) throw new InvalidDataException("Некорректная фотография в проекте.");
            using var image = ImageProcessor.Decode(photo.ImageData);
        }
    }
}

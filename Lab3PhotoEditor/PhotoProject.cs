namespace Lab3PhotoEditor;

public enum PhotoFilter { Brightness, Grayscale, Invert, Sepia, ColorBalance }

public sealed class PhotoDocument
{
    public string Name { get; set; } = "Фото";
    // PNG хранится в самом проекте: исходный файл можно переместить или удалить.
    public byte[] ImageData { get; set; } = [];
}

public sealed class PhotoProject
{
    public int Version { get; set; } = 1;
    public int SelectedIndex { get; set; } = -1;
    public List<PhotoDocument> Photos { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public PhotoDocument Current => SelectedIndex >= 0 && SelectedIndex < Photos.Count
        ? Photos[SelectedIndex] : throw new InvalidOperationException("Сначала добавьте фотографию.");

    public void Add(string name, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var bitmap = ImageProcessor.Decode(data);
        Photos.Add(new PhotoDocument { Name = name.Trim(), ImageData = ImageProcessor.Encode(bitmap) });
        SelectedIndex = Photos.Count - 1;
    }

    public void Select(int index)
    {
        if (index < 0 || index >= Photos.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedIndex = index;
    }

    public void RemoveCurrent()
    {
        _ = Current;
        Photos.RemoveAt(SelectedIndex);
        SelectedIndex = Photos.Count == 0 ? -1 : Math.Min(SelectedIndex, Photos.Count - 1);
    }
}

public interface IImageProcessor
{
    byte[] Crop(byte[] source, int x, int y, int width, int height);
    byte[] Rotate(byte[] source, int degrees);
    byte[] SetPixel(byte[] source, int x, int y, System.Drawing.Color color);
    byte[] AddText(byte[] source, string text, int x, int y, float size, System.Drawing.Color color);
    byte[] ApplyFilter(byte[] source, PhotoFilter filter, int value = 0, int green = 0, int blue = 0);
    byte[] Collage(IReadOnlyList<byte[]> sources, int columns, int cellWidth, int cellHeight);
}

public interface IProjectStorage
{
    void Save(string path, PhotoProject project);
    PhotoProject Load(string path);
}

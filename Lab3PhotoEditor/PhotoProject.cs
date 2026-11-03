namespace Lab3PhotoEditor;

// Доступные преобразования цвета. Числовое значение выбирается через меню.
public enum PhotoFilter { Brightness, Grayscale, Invert, Sepia, ColorBalance }

// Одна фотография: название и текущие данные изображения после обработки.
public sealed class PhotoDocument
{
    public string Name { get; set; } = "Фото";
    // PNG хранится в самом проекте: исходный файл можно переместить или удалить.
    public byte[] ImageData { get; set; } = [];
}

// Рабочая сессия: коллекция фотографий и индекс выбранного документа.
public sealed class PhotoProject
{
    // Версия позволяет отклонить сохранение с несовместимой структурой.
    public int Version { get; set; } = 1;
    // Индекс начинается с нуля; -1 означает, что в проекте нет фотографий.
    public int SelectedIndex { get; set; } = -1;
    public List<PhotoDocument> Photos { get; set; } = [];

    // Текущее фото вычисляется по индексу, поэтому отдельно в JSON не записывается.
    [System.Text.Json.Serialization.JsonIgnore]
    public PhotoDocument Current => SelectedIndex >= 0 && SelectedIndex < Photos.Count
        ? Photos[SelectedIndex] : throw new InvalidOperationException("Сначала добавьте фотографию.");

    // Проверяем входные данные, переводим изображение в PNG и выбираем добавленный документ.
    public void Add(string name, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var bitmap = ImageProcessor.Decode(data);
        Photos.Add(new PhotoDocument { Name = name.Trim(), ImageData = ImageProcessor.Encode(bitmap) });
        SelectedIndex = Photos.Count - 1;
    }

    // Не допускаем выбора индекса, отсутствующего в списке.
    public void Select(int index)
    {
        if (index < 0 || index >= Photos.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedIndex = index;
    }

    // После удаления выбираем соседнее фото; для пустого списка устанавливаем -1.
    public void RemoveCurrent()
    {
        _ = Current;
        Photos.RemoveAt(SelectedIndex);
        SelectedIndex = Photos.Count == 0 ? -1 : Math.Min(SelectedIndex, Photos.Count - 1);
    }
}

// Контракт обработки: методы возвращают новый PNG, не изменяя исходный массив байтов.
public interface IImageProcessor
{
    // Оставить прямоугольную область. X и Y отсчитываются от левого верхнего угла.
    byte[] Crop(byte[] source, int x, int y, int width, int height);
    // Повернуть на угол, кратный 90 градусам; положительный угол — по часовой стрелке.
    byte[] Rotate(byte[] source, int degrees);
    // Заменить цвет одного пикселя по его координатам.
    byte[] SetPixel(byte[] source, int x, int y, System.Drawing.Color color);
    // Нанести текст заданного размера и цвета на изображение.
    byte[] AddText(byte[] source, string text, int x, int y, float size, System.Drawing.Color color);
    // Применить фильтр; value задаёт яркость либо сдвиг красного канала для баланса RGB.
    byte[] ApplyFilter(byte[] source, PhotoFilter filter, int value = 0, int green = 0, int blue = 0);
    // Собрать фото по строкам без зазоров, сохранив пропорции центральной обрезкой.
    byte[] Collage(IReadOnlyList<byte[]> sources, int columns, int cellWidth, int cellHeight);
}

// Контракт хранения позволяет менять способ сохранения независимо от меню.
public interface IProjectStorage
{
    // Сохранить всю сессию, включая данные обработанных фотографий.
    void Save(string path, PhotoProject project);
    // Восстановить сессию из файла и проверить её корректность.
    PhotoProject Load(string path);
}

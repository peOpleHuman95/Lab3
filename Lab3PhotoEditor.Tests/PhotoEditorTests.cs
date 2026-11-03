using System.Drawing;
using System.Text.Json;

namespace Lab3PhotoEditor.Tests;

// Модульные тесты проверяют результат по размерам и пикселям, а не только факт отсутствия ошибок.
[TestClass]
public sealed class ImageProcessorTests
{
    private readonly IImageProcessor processor = new ImageProcessor();
    // Создаём небольшое одноцветное изображение с известными данными для проверок.
    internal static byte[] Solid(Color color, int width = 4, int height = 3)
    {
        using var image = new Bitmap(width, height);
        using (var g = Graphics.FromImage(image)) g.Clear(color);
        return ImageProcessor.Encode(image);
    }
    // Считываем пиксель результата, освобождая декодированное изображение.
    private static Color Pixel(byte[] data, int x = 0, int y = 0)
    { using var image = ImageProcessor.Decode(data); return image.GetPixel(x, y); }

    [TestMethod]
    public void Crop_KeepsRequestedPixelsAndDimensions()
    {
        byte[] source = processor.SetPixel(Solid(Color.Blue), 2, 1, Color.Red);
        using var result = ImageProcessor.Decode(processor.Crop(source, 2, 1, 2, 2));
        Assert.AreEqual(2, result.Width); Assert.AreEqual(2, result.Height);
        Assert.AreEqual(Color.Red.ToArgb(), result.GetPixel(0, 0).ToArgb());
        Assert.AreEqual(Color.Blue.ToArgb(), result.GetPixel(1, 1).ToArgb());
    }
    [DataTestMethod]
    [DataRow(-1, 0, 2, 2)] [DataRow(3, 2, 2, 2)] [DataRow(0, 0, 0, 2)]
    public void Crop_RejectsInvalidRectangle(int x, int y, int w, int h) =>
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => processor.Crop(Solid(Color.Red), x, y, w, h));

    [DataTestMethod]
    [DataRow(90, 3, 4)] [DataRow(-90, 3, 4)] [DataRow(180, 4, 3)] [DataRow(360, 4, 3)]
    public void Rotate_HasExpectedDimensions(int angle, int width, int height)
    {
        using var result = ImageProcessor.Decode(processor.Rotate(Solid(Color.Red), angle));
        Assert.AreEqual(width, result.Width); Assert.AreEqual(height, result.Height);
    }
    [TestMethod]
    public void Rotate_ClockwiseMovesTopLeftToTopRight()
    {
        byte[] source = processor.SetPixel(Solid(Color.Black), 0, 0, Color.Red);
        Assert.AreEqual(Color.Red.ToArgb(), Pixel(processor.Rotate(source, 90), 2, 0).ToArgb());
    }
    [TestMethod]
    public void Rotate_RejectsUnsupportedAngle() =>
        Assert.ThrowsException<ArgumentException>(() => processor.Rotate(Solid(Color.Red), 45));
    [TestMethod]
    public void SetPixel_ChangesOnlyRequestedPixelAndPreservesSource()
    {
        byte[] source = Solid(Color.Blue);
        byte[] result = processor.SetPixel(source, 1, 1, Color.Lime);
        Assert.AreEqual(Color.Lime.ToArgb(), Pixel(result, 1, 1).ToArgb());
        Assert.AreEqual(Color.Blue.ToArgb(), Pixel(result, 0, 0).ToArgb());
        Assert.AreEqual(Color.Blue.ToArgb(), Pixel(source, 1, 1).ToArgb());
    }
    [TestMethod]
    public void SetPixel_RejectsOutsidePoint() =>
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => processor.SetPixel(Solid(Color.Blue), 4, 0, Color.Red));
    [TestMethod]
    public void Grayscale_MakesChannelsEqual()
    {
        Color c = Pixel(processor.ApplyFilter(Solid(Color.Red), PhotoFilter.Grayscale));
        Assert.AreEqual(c.R, c.G); Assert.AreEqual(c.G, c.B);
        Assert.IsTrue(c.R >= 75 && c.R <= 77);
    }
    [TestMethod]
    public void Invert_ReversesEachChannel()
    {
        Color c = Pixel(processor.ApplyFilter(Solid(Color.FromArgb(10, 30, 200)), PhotoFilter.Invert));
        Assert.IsTrue(Math.Abs(c.R - 245) <= 1 && Math.Abs(c.G - 225) <= 1 && Math.Abs(c.B - 55) <= 1);
    }
    [DataTestMethod]
    [DataRow(100, 255)] [DataRow(-100, 0)] [DataRow(0, 120)]
    public void Brightness_ClampsAndAllowsIdentity(int amount, int expected)
    {
        Color c = Pixel(processor.ApplyFilter(Solid(Color.FromArgb(120, 120, 120)), PhotoFilter.Brightness, amount));
        Assert.IsTrue(Math.Abs(c.R - expected) <= 1);
    }
    [TestMethod]
    public void ColorBalance_ChangesChannelsIndependently()
    {
        Color c = Pixel(processor.ApplyFilter(Solid(Color.FromArgb(100, 100, 100)), PhotoFilter.ColorBalance, 30, -20, 0));
        Assert.IsTrue(Math.Abs(c.R - 130) <= 1 && Math.Abs(c.G - 80) <= 1 && Math.Abs(c.B - 100) <= 1);
    }
    [TestMethod]
    public void Sepia_ProducesWarmTone()
    {
        Color c = Pixel(processor.ApplyFilter(Solid(Color.Gray), PhotoFilter.Sepia));
        Assert.IsTrue(c.R > c.G && c.G > c.B);
    }
    [TestMethod]
    public void Filter_RejectsInvalidBrightness() =>
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => processor.ApplyFilter(Solid(Color.Red), PhotoFilter.Brightness, 101));
    [TestMethod]
    public void Collage_PlacesPhotosInOrderAndFillsIncompleteRow()
    {
        byte[][] photos = [Solid(Color.Red), Solid(Color.Blue), Solid(Color.Lime)];
        using var result = ImageProcessor.Decode(processor.Collage(photos, 2, 40, 30));
        Assert.AreEqual(80, result.Width); Assert.AreEqual(60, result.Height);
        Assert.AreEqual(Color.Red.ToArgb(), result.GetPixel(20, 15).ToArgb());
        Assert.AreEqual(Color.Blue.ToArgb(), result.GetPixel(60, 15).ToArgb());
        Assert.AreEqual(Color.Lime.ToArgb(), result.GetPixel(20, 45).ToArgb());
        Assert.AreEqual(Color.Lime.ToArgb(), result.GetPixel(60, 45).ToArgb());
    }
    [TestMethod]
    public void Collage_FillsCellWithoutPadding()
    {
        using var result = ImageProcessor.Decode(processor.Collage([Solid(Color.Red, 10, 20)], 1, 40, 40));
        Assert.AreEqual(Color.Red.ToArgb(), result.GetPixel(0, 20).ToArgb());
        Assert.AreEqual(Color.Red.ToArgb(), result.GetPixel(20, 20).ToArgb());
    }
    [TestMethod]
    public void Collage_HasNoUnpaintedPixelsAtSeamsOrIncompleteRow()
    {
        byte[][] photos = [Solid(Color.Red, 3, 20), Solid(Color.Blue, 30, 2),
            Solid(Color.Lime, 1, 1), Solid(Color.Yellow, 7, 15), Solid(Color.Magenta, 25, 3)];
        using var result = ImageProcessor.Decode(processor.Collage(photos, 3, 7, 9));
        Assert.AreEqual(21, result.Width); Assert.AreEqual(18, result.Height);
        for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                Color pixel = result.GetPixel(x, y);
                Assert.AreEqual(255, (int)pixel.A, $"Прозрачный шов в ({x},{y})");
                Assert.AreNotEqual(Color.White.ToArgb(), pixel.ToArgb(), $"Белый зазор в ({x},{y})");
            }
        Assert.AreEqual(Color.Yellow.ToArgb(), result.GetPixel(9, 13).ToArgb());
        Assert.AreEqual(Color.Magenta.ToArgb(), result.GetPixel(10, 13).ToArgb());
    }

    [TestMethod]
    public void Collage_CenterCropsInsteadOfStretchingPortrait()
    {
        using var portrait = new Bitmap(10, 30);
        using (var g = Graphics.FromImage(portrait))
        {
            g.Clear(Color.Red);
            using var brush = new SolidBrush(Color.Blue);
            g.FillRectangle(brush, 0, 10, 10, 10);
        }
        byte[] source = ImageProcessor.Encode(portrait);
        using var result = ImageProcessor.Decode(processor.Collage([source], 1, 10, 10));
        Assert.AreEqual(Color.Blue.ToArgb(), result.GetPixel(5, 2).ToArgb());
        Assert.AreEqual(Color.Blue.ToArgb(), result.GetPixel(5, 7).ToArgb());
        using var original = ImageProcessor.Decode(source);
        Assert.AreEqual(Color.Red.ToArgb(), original.GetPixel(5, 0).ToArgb());
    }

    [TestMethod]
    public void Collage_RejectsEmptySelection() =>
        Assert.ThrowsException<ArgumentException>(() => processor.Collage([], 1, 40, 40));
    [TestMethod]
    public void Caption_ChangesImageWithoutChangingSize()
    {
        byte[] source = Solid(Color.White, 180, 70);
        using var result = ImageProcessor.Decode(processor.AddText(source, "Привет", 5, 5, 24, Color.Black));
        Assert.AreEqual(180, result.Width); Assert.AreEqual(70, result.Height);
        bool changed = false;
        for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++) changed |= result.GetPixel(x, y).R < 200;
        Assert.IsTrue(changed, "Надпись должна содержать видимые пиксели.");
    }
    [TestMethod]
    public void Decode_RejectsNonImage() => Assert.ThrowsException<ArgumentException>(() => ImageProcessor.Decode([1, 2, 3]));
    [TestMethod]
    public void Size_RejectsExcessiveCanvas() => Assert.ThrowsException<ArgumentOutOfRangeException>(() => ImageProcessor.CheckSize(10000, 10000));
}

// Модульные тесты проверяют результат по размерам и пикселям, а не только факт отсутствия ошибок.
[TestClass]
// Тесты сохранения и экспорта используют собственные временные файлы, не пользовательский проект.
public sealed class ProjectTests
{
    private string directory = null!;
    private string SavePath => Path.Combine(directory, "project.json");
    private readonly IProjectStorage storage = new JsonProjectStorage();
    // Перед каждым тестом выделяем отдельный каталог, чтобы параллельные проверки не мешали друг другу.
    [TestInitialize]
    public void Initialize() => directory = Path.Combine(Path.GetTempPath(), "PhotoEditorTests", Guid.NewGuid().ToString("N"));
    // После проверки удаляем только её временный каталог.
    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    [TestMethod]
    public void EmptyProject_CanBeSavedAndLoaded()
    {
        storage.Save(SavePath, new PhotoProject());
        var loaded = storage.Load(SavePath);
        Assert.AreEqual(0, loaded.Photos.Count); Assert.AreEqual(-1, loaded.SelectedIndex);
    }
    [TestMethod]
    public void Project_RestoresEditedPixelsAndSelectedPhotoWithoutOriginalFiles()
    {
        var project = new PhotoProject();
        project.Add("Первое", ImageProcessorTests.Solid(Color.Blue));
        project.Add("Второе", ImageProcessorTests.Solid(Color.Red));
        project.Select(0);
        project.Current.ImageData = new ImageProcessor().SetPixel(project.Current.ImageData, 2, 1, Color.Lime);
        storage.Save(SavePath, project);
        var loaded = storage.Load(SavePath);
        Assert.AreEqual(2, loaded.Photos.Count); Assert.AreEqual(0, loaded.SelectedIndex);
        using var image = ImageProcessor.Decode(loaded.Current.ImageData);
        Assert.AreEqual(Color.Lime.ToArgb(), image.GetPixel(2, 1).ToArgb());
        Assert.AreEqual("Первое", loaded.Current.Name);
    }
    [TestMethod]
    public void Remove_KeepsSelectionValid()
    {
        var project = new PhotoProject();
        project.Add("One", ImageProcessorTests.Solid(Color.Blue));
        project.Add("Two", ImageProcessorTests.Solid(Color.Red));
        project.RemoveCurrent(); Assert.AreEqual(0, project.SelectedIndex);
        project.RemoveCurrent(); Assert.AreEqual(-1, project.SelectedIndex);
    }
    [TestMethod]
    public void Add_ClonesInputData()
    {
        byte[] data = ImageProcessorTests.Solid(Color.Blue);
        var project = new PhotoProject(); project.Add("One", data);
        Array.Clear(data);
        using var image = ImageProcessor.Decode(project.Current.ImageData);
        Assert.AreEqual(Color.Blue.ToArgb(), image.GetPixel(0, 0).ToArgb());
    }
    [TestMethod]
    public void Load_RejectsInvalidSelection()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SavePath, "{\"Version\":1,\"SelectedIndex\":8,\"Photos\":[]}");
        Assert.ThrowsException<InvalidDataException>(() => storage.Load(SavePath));
    }
    [TestMethod]
    public void InvalidSave_DoesNotReplacePreviousProject()
    {
        var project = new PhotoProject(); storage.Save(SavePath, project);
        string before = File.ReadAllText(SavePath); project.Version = 99;
        Assert.ThrowsException<InvalidDataException>(() => storage.Save(SavePath, project));
        Assert.AreEqual(before, File.ReadAllText(SavePath));
    }
    [TestMethod]
    public void Load_RejectsMalformedJson()
    {
        Directory.CreateDirectory(directory); File.WriteAllText(SavePath, "{broken");
        Assert.ThrowsException<JsonException>(() => storage.Load(SavePath));
    }
    [DataTestMethod]
    [DataRow("png")] [DataRow("jpg")] [DataRow("bmp")]
    public void Export_ProducesReadableImage(string extension)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "export." + extension);
        ImageProcessor.Export(ImageProcessorTests.Solid(Color.Blue), path);
        using var image = ImageProcessor.Decode(File.ReadAllBytes(path));
        Assert.AreEqual(4, image.Width); Assert.AreEqual(3, image.Height);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Rusgeocom;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string downloadFolder = @"C:\Users\user\Downloads";
    private readonly string imageResizerExeDir;
    private readonly RusgeocomParser parser = new(downloadFolder);
    private readonly SqlProductFormatter sqlFormatter;
    private readonly FtpUploader ftpUploader;
    private readonly DbQueryRunner dbQueryRunner;
    private readonly ObservableCollection<string> logItems = new();
    public MainWindow()
    {
        InitializeComponent();
        var configuration = App.AppHost.Services.GetRequiredService<IConfiguration>();
        ftpUploader = App.AppHost.Services.GetRequiredService<FtpUploader>();
        dbQueryRunner = App.AppHost.Services.GetRequiredService<DbQueryRunner>();
        imageResizerExeDir = configuration["ImageResizerExeDir"];
        sqlFormatter = new SqlProductFormatter(configuration["YandexDiskRootUri"]);
        lbLogs.ItemsSource = logItems;

    }

    private async void btnParseFromHtml_Click(object sender, RoutedEventArgs e)
    {
        SetStatus(false);

        string desc = "";
        try
        {
            desc = await parser.ParseFromHtml(Clipboard.GetText(), txtModelName.Text, new Progress<string>((v) =>
            {
                Title = v;
                this.AddLogEntry(v);
            }));
            txtModelName.Text = parser.LastModelData;

            // путь до папки с изображениями бренда подставляется автоматически: catalog/{бренд}/products
            if (!string.IsNullOrEmpty(parser.LastBrandFolder))
            {
                txtImageManufacturerPath.Text = $"catalog/{parser.LastBrandFolder}/products";
            }
            else
            {
                AddLogEntry("Бренд не найден на странице, путь до папки с изображениями не изменён");
            }
        }
        catch (FormatException)
        {
            MessageBox.Show("Ошибка разбора HTML или модели (менее 3 символов)", "Информация", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            SetStatus(true);
            return;
        }

        DeletePrevFiles(parser.LastImagesData ?? []);

        txtDescription.Text = desc.ToString();

        string lastSql = "";

        var swGlobal = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();

        if (btnGenerateSql.IsChecked == true)
        {
            AddLogEntry("Начало генерации SQL");
            if (parser.LastPdfData.Count > 0 && !sqlFormatter.CanGeneratePdfSql(parser))
            {
                AddLogEntry("PDF не добавлены в SQL: не задан YandexDiskRootUri в secrets.json или не найден бренд");
            }
            lastSql = sqlFormatter.GenerateSqlForLastParsedProduct(parser, txtImageManufacturerPath.Text);
            Clipboard.SetText(lastSql);
            AddLogEntry($"Конец генерации SQL", sw);

        }

        var expectedImages = parser.LastImagesData ?? [];
        var expectedBaseNames = expectedImages.Select(Path.GetFileNameWithoutExtension).ToList();
        var stableDelay = TimeSpan.FromSeconds(1);
        var waitTimeout = TimeSpan.FromMinutes(5);

        if (expectedImages.Length > 0 && (chkResizeImages.IsChecked == true || btnUploadToFtp.IsChecked == true))
        {
            AddLogEntry($"Ожидание изображений в {downloadFolder}: {string.Join(", ", expectedImages)}");
        }

        if (expectedImages.Length > 0 && chkResizeImages.IsChecked == true)
        {
            sw.Restart();
            // изображения сохраняет скрипт из браузера (webp/png/jpg), после того как все файлы записаны - изменяем размер и конвертируем в jpg
            bool ready = await ImageFilesWaiter.WaitAsync(downloadFolder, expectedBaseNames,
                [".webp", ".png", ".avif", ".jpeg", ".jpg"], stableDelay, waitTimeout);
            if (!ready)
            {
                StopWaitingForImages();
                return;
            }

            AddLogEntry("Начало изменения размера изображений");
            await ResizeImages();
            AddLogEntry($"Конец изменения размера изображений", sw);
        }

        if (btnUploadToFtp.IsChecked == true && expectedImages.Length > 0)
        {
            sw.Restart();
            // выгрузка стартует только когда все jpg на месте и не менялись 1 секунду (конвертация webp -> jpg завершена)
            bool ready = await ImageFilesWaiter.WaitAsync(downloadFolder, expectedBaseNames, [".jpg"], stableDelay, waitTimeout);
            if (!ready)
            {
                StopWaitingForImages();
                return;
            }

            AddLogEntry("Начало загрузки изображений на FTP");
            await ftpUploader.UploadImages(txtImageManufacturerPath.Text,
                expectedImages.Select(img => Path.Combine(downloadFolder, img)),
                new Progress<double>((v) => Title = $"Uploading images: {v:P2}"));
            AddLogEntry($"Конец загрузки изображений на FTP", sw);
        }

        if (btnGenerateSql.IsChecked == true && chkAutoRunSql.IsChecked == true && !string.IsNullOrWhiteSpace(lastSql))
        {
            sw.Restart();
            AddLogEntry("Начало выполнения SQL");

            await dbQueryRunner.ValidateModelName(parser.LastModelData);
            await dbQueryRunner.Execute(lastSql);
            AddLogEntry($"Конец выполнения SQL", sw);
        }

        AddLogEntry($"Парсинг {parser.LastModelData ?? "???"} завершен", swGlobal);
        SetStatus(true);


        txtModelName.Text = "";
    }

    private void StopWaitingForImages()
    {
        AddLogEntry("Время ожидания изображений истекло: проверьте, что файлы сохранены как {модель}_{номер} и конвертированы в jpg. SQL не выполнен");
        SetStatus(true);
    }

    private void SetStatus(bool status)
    {
        this.IsEnabled = status;
        myTaskbarProgress.ProgressState = status == false ? System.Windows.Shell.TaskbarItemProgressState.Indeterminate : System.Windows.Shell.TaskbarItemProgressState.Normal;
    }

    // удаляются jpg предыдущих товаров; файлы текущего товара (скрипт мог отработать до нажатия кнопки) остаются
    private void DeletePrevFiles(IEnumerable<string> keepFileNames)
    {
        var keep = new HashSet<string>(keepFileNames, StringComparer.OrdinalIgnoreCase);
        var prevFiles = Directory.GetFiles(downloadFolder, "*.jpg").Where(file => !keep.Contains(Path.GetFileName(file)));
        foreach (var file in prevFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                AddLogEntry($"Не удалось удалить файл {file}: {ex.Message}");
            }
        }
    }

    private void AddLogEntry(string v, Stopwatch sw = null)
    {
        string msg = $"{DateTime.Now} {v}";
        if (sw != null)
        {
            msg += $" (заняло {(int)sw.Elapsed.TotalMilliseconds} ms)";
        }
        logItems.Insert(0, msg);
    }

    private async Task ResizeImages()
    {
        await Task.Run(async () =>
        {
            string resolution = "1000x1000";
            string exe = $@"{imageResizerExeDir}\ImageCompressorApp.exe";
            var pi = new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = $"{downloadFolder} {resolution}",
                WorkingDirectory = imageResizerExeDir,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };
            var p = Process.Start(pi);
            await p.WaitForExitAsync();
        });
        await Task.Delay(TimeSpan.FromSeconds(0.5));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(App.SETTINGS_FILE_NAME))
        {
            File.WriteAllText(App.SETTINGS_FILE_NAME, "catalog/manufacturer/products");
        }
        txtImageManufacturerPath.Text = File.ReadAllText(App.SETTINGS_FILE_NAME);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        File.WriteAllText(App.SETTINGS_FILE_NAME, txtImageManufacturerPath.Text);
    }

}

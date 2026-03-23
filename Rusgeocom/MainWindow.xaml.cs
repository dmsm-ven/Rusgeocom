using Rusgeocom.ParserLib;
using System;
using System.IO;
using System.Media;
using System.Windows;

namespace Rusgeocom
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string MISSING_SKUS_FILE_NAME = "missing_skus.txt";
        private const string URLS_TO_PARSE_FILE_NAME = "urls_to_parse.txt";
        private const string CUSTOM_BRAND_URI = "https://spb.rusgeocom.ru/brands/hikmicro";

        private readonly Manager manager;

        public MainWindow()
        {
            InitializeComponent();

            manager = new Manager("products.json", () =>
            {
                var window = new StartProductIdWindow();
                window.ShowDialog();
                var id = window.StartId;
                if (id > 0)
                {
                    return id;
                }
                throw new ArgumentOutOfRangeException("Стартовый ID не выбран");
            });

        }

        private async void btnParse_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            try
            {
                await manager.Parse(new Progress<double>(v => pbIndicator.Value = v), MISSING_SKUS_FILE_NAME);
                IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void btnParseFile_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            try
            {
                await manager.ParseUrls(URLS_TO_PARSE_FILE_NAME, new Progress<double>(v => pbIndicator.Value = v));
                IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private async void btnParseBrand_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            pbIndicator.IsIndeterminate = true;
            try
            {
                await manager.ParseBrand(CUSTOM_BRAND_URI); // hikmicro
                IsEnabled = true;
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
                pbIndicator.IsIndeterminate = false;
            }
        }

        private async void btnDownloadResources_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;

            string resourceFolder = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "downloads");
            if (!Directory.Exists(resourceFolder))
            {
                Directory.CreateDirectory(resourceFolder);
            }

            await manager.DownloadResource(resourceFolder, new Progress<double>(v => pbIndicator.Value = v));

            IsEnabled = true;

        }

        private void btnExportGeneral_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetGeneralExport();
            Clipboard.SetText(data);
        }

        private void btnAdditionalImages_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetAdditionalImagesExport();
            Clipboard.SetText(data);
        }

        private void btnFillDescriptions_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                manager.FillDescription(ofd.FileName);
            }
        }

        private void btnGetModelRanges_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetModelRanges();
            Clipboard.SetText(data);
        }

        private void btnGetAccessories_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetAccessories();
            Clipboard.SetText(data);
        }

        private void btnGetZondes_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetProbes();
            Clipboard.SetText(data);
        }

        private void btnGosreetr_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetGosreetr();
            Clipboard.SetText(data);
        }

        private void btnGetCategories_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetCategories();
            Clipboard.SetText(data);
        }

        private void btnGetCategoriesProducts_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetCategoriesProducts();
            Clipboard.SetText(data);
        }

        private void btnDescEquip_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetDescriptionAndEquipmentSql();
            Clipboard.SetText(data);
        }

        private void btnGetPdf_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetPdfSql();
            Clipboard.SetText(data);
        }

        private void btnGetDimensions_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetDimensionsSql();
            Clipboard.SetText(data);
        }

        private void btnImagesSql_Click(object sender, RoutedEventArgs e)
        {
            string data = manager.GetImagesSql();
            Clipboard.SetText(data);
        }

        private async void btnSingleFromHtml_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Clipboard.GetText()))
            {
                MessageBox.Show("HTML разметка не найдена в буфере обмена");
                return;
            }

            btnSingleFromHtml.IsEnabled = false;
            try
            {
                string data = await manager.ParseSingleProductFromHtml(Clipboard.GetText());
                Clipboard.SetText(data);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSingleFromHtml.IsEnabled = true;
                SystemSounds.Exclamation.Play();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Перенести на .NET 10 , этот проект legacy .net framework 4.7", "INFO", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }
    }
}

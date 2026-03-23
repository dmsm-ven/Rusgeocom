using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Rusgeocom.ParserLib
{
    public class ResourceDownloader
    {
        private HttpClient httpClient;

        public ResourceDownloader(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task DownloadResource(IEnumerable<Product> products, IProgress<double> indicator)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException();
            }

            int total = products.Count();
            int current = 0;

            foreach (var product in products)
            {
                try
                {
                    await DownloadProductResources(product, folder);
                }
                catch
                {
                    throw;
                }

                indicator?.Report((double)++current / total);
            }

        }

        public async Task DownloadProductResources(Product product, string folder = null)
        {
            if (folder == null)
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(folder))
                {
                    throw new DirectoryNotFoundException();
                }
            }

            if (product.Images != null)
            {
                int sort_order = 1;
                foreach (var image in product.Images)
                {
                    string localPath = Path.Combine(folder, product.manufacturer_ftp_path, "products", $"{product.Sku}_{sort_order++}.jpg");

                    if (!File.Exists(localPath))
                    {
                        var dir = Path.GetDirectoryName(localPath);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        var bytes = await httpClient.GetByteArrayAsync(image);
                        File.WriteAllBytes(localPath, bytes);
                    }
                }
            }

            if (product.Instructions != null)
            {
                foreach (var pdf in product.Instructions)
                {
                    string localPath = Path.Combine(folder, product.manufacturer_ftp_path, "pdf", pdf.Uri.CreateMD5() + ".pdf");

                    if (!File.Exists(localPath))
                    {
                        var dir = Path.GetDirectoryName(localPath);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        var bytes = await httpClient.GetByteArrayAsync(pdf.Uri);
                        File.WriteAllBytes(localPath, bytes);
                    }
                }
            }
        }
    }
}
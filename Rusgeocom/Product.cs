using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Rusgeocom.ParserLib
{
    public class Product
    {
        public string Name { get; set; }
        public string Uri { get; set; }
        public string Category { get; set; }
        public string Manufacturer { get; set; }
        public string SearchSku { get; set; }

        [JsonIgnore]
        public string manufacturer_ftp_path
        {
            get => (Manufacturer ?? "Hikmicro").ToLower().Replace(" ", "_");
        }
        [JsonIgnore]
        public string ProductType
        {
            get => Regex.Match(Name, $"^(.*?) ({Manufacturer}) (.*?)$").Groups[1].Value;
        }
        [JsonIgnore]
        public string Model
        {
            get => Regex.Match(Name, $"^(.*?) ({Manufacturer}) (.*?)$").Groups[3].Value;
        }

        public string Sku { get; set; }
        public string Code { get; set; }
        public string DescriptionMarkup { get; set; }
        public decimal? Weight { get; set; }
        public string EAN { get; set; }

        public List<Pdf> Instructions { get; set; } = new List<Pdf>();
        public List<string> Images { get; set; } = new List<string>();
        public List<string> Breadcrumbs { get; set; } = new List<string>();
        public List<string> AccessoriesCodes { get; set; } = new List<string>();
        public List<string> ProbeCodes { get; set; } = new List<string>();
        public List<string> ModelRangeCodes { get; set; } = new List<string>();
        public List<string> ComplectationItems { get; set; } = new List<string>();
        public List<Characteristic> Characteristics { get; set; } = new List<Characteristic>();
        public ProductDimensions Dimensions { get; set; } = new ProductDimensions();
        public string Gosreestr { get; set; }
        public bool IsParsed { get; set; }
    }

    public class Characteristic
    {
        public string Group { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class Pdf
    {
        public string Name { get; set; }
        public string Uri { get; set; }
    }
}

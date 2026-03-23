using System.Text.Json.Serialization;

namespace Rusgeocom
{
    public class ProductDimensions
    {
        public int Length { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public decimal Weight { get; set; }
        [JsonIgnore]
        public bool NotEmpty => Length > 0 || Width > 0 || Height > 0 || Weight != decimal.Zero;
    }
}
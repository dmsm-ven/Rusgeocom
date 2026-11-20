namespace Rusgeocom;

public class DimensionsData
{
    public decimal WeightKg { get; set; }
    public decimal LengthMm { get; set; }
    public decimal WidthMm { get; set; }
    public decimal HeightMm { get; set; }

    public bool NotEmpty => WeightKg != 0 || LengthMm != 0 || WidthMm != 0 || HeightMm != 0;
}

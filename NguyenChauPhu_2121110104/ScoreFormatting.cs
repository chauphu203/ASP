namespace NguyenChauPhu_2121110104;

/// <summary>Cắt 2 chữ số thập phân (không làm tròn lên), đồng bộ với hiển thị trên web.</summary>
public static class ScoreFormatting
{
    public static double Trunc2(double value) => Math.Truncate(value * 100d) / 100d;

    public static double? Trunc2Nullable(double? value) => value is null ? null : Trunc2(value.Value);
}

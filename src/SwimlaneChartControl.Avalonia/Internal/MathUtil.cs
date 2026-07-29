namespace SwimlaneChartControl.Avalonia.Internal;

internal static class MathUtil
{
    public static double Clamp(double value, double min, double max)
    {
        if (min > max) (min, max) = (max, min);
        return value < min ? min : value > max ? max : value;
    }
}

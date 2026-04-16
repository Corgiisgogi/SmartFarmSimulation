using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartFarmUI.Views.Converters
{
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class BoolToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = Brushes.LimeGreen;
        public Brush FalseBrush { get; set; } = Brushes.Red;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? TrueBrush : FalseBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    [ValueConversion(typeof(int), typeof(bool))]
    public class IntEqualConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i && parameter != null && int.TryParse(parameter.ToString(), out int p))
                return i == p;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

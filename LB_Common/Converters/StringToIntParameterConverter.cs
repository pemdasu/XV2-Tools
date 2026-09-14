using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LB_Common.Converters
{
    public class StringToIntParameterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string text && int.TryParse(text, NumberStyles.Integer, culture, out int result))
            {
                return result;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is int number)
            {
                return number.ToString(culture);
            }

            return "0";
        }
    }
}
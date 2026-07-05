using System;
using System.Globalization;
using System.Windows.Data;

namespace LB_Mod_Installer.Binding
{
    /// <summary>
    /// Loads a picker tile image from the loaded .installinfo archive by its zip-root relative path.
    /// Returns null when the path is empty or missing so the Image simply renders nothing.
    /// </summary>
    public class ZipImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;

            if (string.IsNullOrWhiteSpace(path) || GeneralInfo.ZipManager == null)
                return null;

            try
            {
                if (!GeneralInfo.ZipManager.Exists(path))
                    return null;

                return GeneralInfo.ZipManager.LoadBitmapFromArchive(path);
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

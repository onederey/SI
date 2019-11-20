﻿using System;
using System.Windows.Data;

namespace SImulator.Converters
{
    public sealed class MappedConverter : IValueConverter
    {
        public StringDictionary Map { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;

            if (Map.TryGetValue(value.ToString(), out string result))
                return result;

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var val = value.ToString();
            foreach (var item in Map)
            {
                if (item.Value == val)
                    return item.Key;
            }
            return value;
        }
    }
}

﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Volumey.View.Converters
{
	public class CollectionCountToVisibilityConverter : IValueConverter
	{
		public object Convert(object listCount, Type targetType, object parameter, CultureInfo culture)
		{
			if(listCount is int count)
				return count == 0 ? Visibility.Collapsed : Visibility.Visible;
			return Visibility.Visible;
		}

		public object ConvertBack(object listCount, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}
}
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PL;

public class ConverterUpdateToTrue: IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue && strValue == "Update")
        {
            return true;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

public class ConverterAddToTrue: IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue && strValue == "Add")
        {
            return true;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

public class ConverterAddToFalse : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue && strValue == "Add")
            return false;

        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

public class ConverterUpdateToVisible : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue && strValue == "Update")
        {
            return Visibility.Hidden;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

public class ConvertAddToVisibleKey : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue && strValue == "Add")
        {
            return Visibility.Visible;
        }
        return Visibility.Hidden;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
public class ConvertAddToNotVisibleKey : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue && strValue == "Add")
        {
            return Visibility.Hidden;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

public class TransportTypeToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BO.DeliveryTransport transportType)
        {
            return transportType switch
            {
                BO.DeliveryTransport.Car => new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),       // brand blue
                BO.DeliveryTransport.Motorcycle => new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)), // green
                BO.DeliveryTransport.Bicycle => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),    // amber (white text readable)
                BO.DeliveryTransport.Walk => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),       // red
                BO.DeliveryTransport.All => new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),       // slate
                _ => new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DeliveryTypeToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BO.DeliveryType deliveryType)
        {
            return deliveryType switch
            {
                BO.DeliveryType.Express => new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)),    // amber/orange
                BO.DeliveryType.Regular => new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),    // green
                _ => new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))                           // blue-gray slate
            };
        }
        return new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)); // soft blue #DBEAFE for null
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DeliveryTypeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BO.DeliveryType dt)
            return dt.ToString();
        return "Not Set";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DeliveryTypeToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BO.DeliveryType)
            return new SolidColorBrush(Colors.White);
        return new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0xAF)); // dark blue #1E40AF
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v == Visibility.Visible;
        return false;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v != Visibility.Visible;
        return false;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        return !flag;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}

public class OrderStatusToCancelVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BO.OrderStatus status)
            return (status == BO.OrderStatus.Delivered || status == BO.OrderStatus.Cancelled)
                ? Visibility.Collapsed
                : Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns true when the bound value is non-null (and, for strings, non-empty).
/// Used to enable a dependent control (e.g. the Value ComboBox) only after a
/// prerequisite selection (e.g. Filter by) has been made.
/// </summary>
public class NullableToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s) return !string.IsNullOrEmpty(s);
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TimeSpanToReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TimeSpan ts)
            return value?.ToString() ?? string.Empty;

        if (ts < TimeSpan.Zero)
        {
            var abs = ts.Negate();
            if (abs.TotalDays >= 1)
                return $"Overdue by {(int)abs.TotalDays}d {abs.Hours}h";
            if (abs.TotalHours >= 1)
                return $"Overdue by {(int)abs.TotalHours}h {abs.Minutes}m";
            return $"Overdue by {(int)abs.TotalMinutes}m";
        }

        if (ts.TotalMinutes < 1)
            return "Due now";

        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h remaining";

        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m remaining";

        return $"{(int)ts.TotalMinutes}m remaining";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ScheduleStatusToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BO.ScheduleStatus s)
        {
            return s switch
            {
                BO.ScheduleStatus.OnTime  => new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)), // green
                BO.ScheduleStatus.InRisk  => new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)), // amber
                BO.ScheduleStatus.Late    => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)), // red
                _                        => new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ScheduleStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BO.ScheduleStatus s)
        {
            return s switch
            {
                BO.ScheduleStatus.OnTime => "On Time",
                BO.ScheduleStatus.InRisk => "Urgent",
                BO.ScheduleStatus.Late   => "Overdue",
                _                       => s.ToString()
            };
        }
        return value?.ToString() ?? string.Empty;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
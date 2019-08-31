using System;
using System.ComponentModel;

namespace Sds.Storage.Blob.WebApi.Converters
{
    public class EnvironmentInt64Converter : Int64Converter
    {
        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value != null && value is string)
            {
                string stringValue = value as string;
                if (stringValue.StartsWith('%') && stringValue.EndsWith('%'))
                {
                    return Convert.ToInt64(Environment.ExpandEnvironmentVariables(stringValue));
                }
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}

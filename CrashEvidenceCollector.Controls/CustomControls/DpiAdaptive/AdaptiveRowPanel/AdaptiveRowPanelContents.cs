using System;
using System.ComponentModel;
using System.Drawing.Design;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    [Editor(typeof(AdaptiveRowPanelContentsEditor), typeof(UITypeEditor))]
    [TypeConverter(typeof(AdaptiveRowPanelContentsConverter))]
    public sealed class AdaptiveRowPanelContents
    {
        private readonly AdaptiveRowPanel _owner;

        internal AdaptiveRowPanelContents(AdaptiveRowPanel owner)
        {
            _owner = owner;
        }

        internal AdaptiveRowPanel Owner => _owner;

        public override string ToString()
        {
            int count = _owner.Controls.Count;
            return count == 1 ? "1 child" : $"{count} children";
        }
    }

    internal sealed class AdaptiveRowPanelContentsConverter : TypeConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is AdaptiveRowPanelContents contents)
            {
                return contents.ToString();
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext? context)
        {
            return false;
        }
    }
}

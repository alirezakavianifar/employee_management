using System;
using System.Windows.Markup;
using Shared.Utils;

namespace ManagementApp.Converters
{
    /// <summary>
    /// Markup extension for loading UI strings from resources.xml in XAML.
    /// Usage: Text="{local:Res Key=header_employee_list}" or Content="{local:Res header_employee_list}"
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class ResExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;
        public string Fallback { get; set; } = string.Empty;

        public ResExtension() { }

        public ResExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return string.IsNullOrEmpty(Key) ? Fallback : ResourceManager.GetString(Key, Fallback);
        }
    }
}

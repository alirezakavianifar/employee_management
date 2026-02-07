using System;
using System.Windows.Markup;
using Shared.Utils;

namespace DisplayApp.Converters
{
    /// <summary>
    /// Markup extension for loading UI strings from resources.xml in XAML.
    /// Usage: Text="{converters:Res Key=display_app_title}" or Content="{converters:Res display_app_title}"
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

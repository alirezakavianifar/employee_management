using System;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Shared.Utils;

namespace DisplayApp.Converters
{
    /// <summary>
    /// Markup extension for loading UI strings from resources.xml in XAML.
    /// Usage: Text="{converters:Res display_app_title}" or Content="{converters:Res display_app_title}"
    /// Returns a binding that automatically updates when the language changes.
    /// Falls back to static string for properties that don't support bindings.
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public class ResExtension : MarkupExtension
    {
        private static readonly ResKeyConverter _converter = new ResKeyConverter();
        
        public string Key { get; set; } = string.Empty;
        public string Fallback { get; set; } = string.Empty;

        public ResExtension() { }

        public ResExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return Fallback;

            // Check if we can use a binding (target must be a DependencyProperty)
            var provideValueTarget = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            var targetProperty = provideValueTarget?.TargetProperty;
            
            // If target is not a DependencyProperty, return static string (e.g., Window.Title, template contexts)
            if (targetProperty is not DependencyProperty)
            {
                return ResourceManager.GetString(Key, Fallback);
            }

            // Create a binding to ResourceBridge.Version
            // When Version changes (e.g. language change or resource reload), the binding re-evaluates
            var binding = new Binding(nameof(ResourceBridge.Version))
            {
                Source = ResourceBridge.Instance,
                Converter = _converter,
                ConverterParameter = Key,
                Mode = BindingMode.OneWay
            };

            // Return the binding's provided value so WPF can set up the data binding
            return binding.ProvideValue(serviceProvider);
        }
    }
}


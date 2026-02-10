using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Shared.Utils
{
    /// <summary>
    /// Singleton that exposes current UI language for data binding. When language changes,
    /// PropertyChanged is raised so XAML bindings that depend on CurrentLanguage refresh.
    /// </summary>
    public sealed class ResourceBridge : INotifyPropertyChanged
    {
        private static readonly Lazy<ResourceBridge> _instance = new Lazy<ResourceBridge>(() => new ResourceBridge());
        private string _currentLanguage = LanguageConfigHelper.LanguageEn;

        public static ResourceBridge Instance => _instance.Value;

        /// <summary>
        /// Current language code ("en" or "fa"). Changing this and calling NotifyLanguageChanged
        /// causes bindings to refresh.
        /// </summary>
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage == value)
                    return;
                _currentLanguage = value ?? LanguageConfigHelper.LanguageEn;
                OnPropertyChanged(nameof(CurrentLanguage));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private int _version;

        /// <summary>
        /// Incrementing version number to force binding updates even if language code is the same.
        /// </summary>
        public int Version => _version;

        /// <summary>
        /// Call after reloading resources and optionally setting FlowDirection so all
        /// bindings that use CurrentLanguage re-evaluate.
        /// </summary>
        public void NotifyLanguageChanged()
        {
            _version++;
            OnPropertyChanged(nameof(CurrentLanguage));
            OnPropertyChanged(nameof(Version));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

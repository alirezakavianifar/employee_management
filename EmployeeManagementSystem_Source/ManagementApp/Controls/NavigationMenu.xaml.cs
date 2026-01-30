using System;
using System.Windows;
using System.Windows.Controls;

namespace ManagementApp.Controls
{
    public partial class NavigationMenu : UserControl
    {
        public event EventHandler<string>? NavigationItemSelected;

        public NavigationMenu()
        {
            InitializeComponent();
        }

        private void NavigationTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem selectedItem && selectedItem.Tag is string tag)
            {
                NavigationItemSelected?.Invoke(this, tag);
            }
        }

        public void SelectItem(string tag)
        {
            SelectItemRecursive(NavigationTreeView.Items, tag);
        }

        private bool SelectItemRecursive(ItemCollection items, string tag)
        {
            foreach (TreeViewItem item in items)
            {
                if (item.Tag is string itemTag && itemTag == tag)
                {
                    item.IsSelected = true;
                    item.BringIntoView();
                    return true;
                }
                
                if (item.Items.Count > 0)
                {
                    if (SelectItemRecursive(item.Items, tag))
                    {
                        item.IsExpanded = true;
                        return true;
                    }
                }
            }
            return false;
        }

        public void ExpandCategory(string categoryName)
        {
            ExpandCategoryRecursive(NavigationTreeView.Items, categoryName);
        }

        private bool ExpandCategoryRecursive(ItemCollection items, string categoryName)
        {
            foreach (TreeViewItem item in items)
            {
                if (item.Header is string header && header.Contains(categoryName))
                {
                    item.IsExpanded = true;
                    return true;
                }
                
                if (item.Items.Count > 0)
                {
                    ExpandCategoryRecursive(item.Items, categoryName);
                }
            }
            return false;
        }
    }
}


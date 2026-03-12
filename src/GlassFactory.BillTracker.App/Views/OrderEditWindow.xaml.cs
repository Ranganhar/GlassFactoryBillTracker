using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using GlassFactory.BillTracker.App.ViewModels;

namespace GlassFactory.BillTracker.App.Views;

public partial class OrderEditWindow : Window
{
    public OrderEditWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is OrderEditViewModel vm)
        {
            vm.ValidationFailed -= OnValidationFailed;
            vm.ValidationFailed += OnValidationFailed;
            InitializeItemsContextMenu(vm);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is OrderEditViewModel vm)
        {
            vm.ValidationFailed -= OnValidationFailed;
        }

        Closing -= OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is not OrderEditViewModel vm || !vm.IsDirty)
        {
            return;
        }

        var result = MessageBox.Show(
            "检测到未保存的修改，是否保存？\n是：保存，否：不保存，取消：继续编辑",
            "提示",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == MessageBoxResult.No)
        {
            vm.AcceptChanges();
            return;
        }

        e.Cancel = true;
        if (vm.SaveCommand.CanExecute(null))
        {
            vm.SaveCommand.Execute(null);
        }
    }

    private void OnValidationFailed(string field, int? rowIndex)
    {
        if (field == "Customer")
        {
            CustomerComboBox.Focus();
            return;
        }

        if (field == "Model" && rowIndex.HasValue && rowIndex.Value >= 0 && rowIndex.Value < ItemsDataGrid.Items.Count)
        {
            var item = ItemsDataGrid.Items[rowIndex.Value];
            ItemsDataGrid.SelectedItem = item;
            ItemsDataGrid.ScrollIntoView(item);

            if (ItemsDataGrid.Columns.Count > 0)
            {
                ItemsDataGrid.CurrentCell = new DataGridCellInfo(item, ItemsDataGrid.Columns[0]);
                ItemsDataGrid.Focus();
                ItemsDataGrid.BeginEdit();
            }
        }
    }

    private void InitializeItemsContextMenu(OrderEditViewModel vm)
    {
        if (ItemsDataGrid.ContextMenu is not null)
        {
            ItemsDataGrid.ContextMenu.DataContext = vm;
            return;
        }

        var contextMenu = new ContextMenu
        {
            DataContext = vm
        };

        var selectedItemBinding = new Binding(nameof(ItemsDataGrid.SelectedItem))
        {
            Source = ItemsDataGrid
        };

        var copyMenuItem = new MenuItem
        {
            Header = "复制行",
            Command = vm.CopySelectedItemCommand
        };
        copyMenuItem.SetBinding(MenuItem.CommandParameterProperty, selectedItemBinding);

        var deleteMenuItem = new MenuItem
        {
            Header = "删除行",
            Command = vm.DeleteSelectedItemCommand
        };
        deleteMenuItem.SetBinding(MenuItem.CommandParameterProperty, selectedItemBinding);

        contextMenu.Items.Add(copyMenuItem);
        contextMenu.Items.Add(deleteMenuItem);
        ItemsDataGrid.ContextMenu = contextMenu;
    }

    private void ItemsDataGrid_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dependencyObject = e.OriginalSource as DependencyObject;
        while (dependencyObject is not null && dependencyObject is not DataGridRow)
        {
            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        if (dependencyObject is DataGridRow row)
        {
            row.IsSelected = true;
            ItemsDataGrid.SelectedItem = row.Item;
            if (ItemsDataGrid.Columns.Count > 0)
            {
                ItemsDataGrid.CurrentCell = new DataGridCellInfo(row.Item, ItemsDataGrid.Columns[0]);
            }
        }
    }
}

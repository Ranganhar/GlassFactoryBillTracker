using System.Windows;
using System.Windows.Controls;
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

}

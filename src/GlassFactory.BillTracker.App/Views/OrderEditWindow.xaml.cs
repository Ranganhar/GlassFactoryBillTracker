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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Input;
using GlassFactory.BillTracker.App.Services;
using GlassFactory.BillTracker.App.ViewModels;
using GlassFactory.BillTracker.Data.Exports;
using GlassFactory.BillTracker.Infrastructure.Services;

namespace GlassFactory.BillTracker.App.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var customerService = new CustomerService();
        var attachmentService = new AttachmentService(AppRuntimeContext.DataDir, AppRuntimeContext.DbPath);
        var orderService = new OrderService(attachmentService);
        var exportService = new ExportService(AppRuntimeContext.DbPath);
        var fileDialogService = new FileDialogService();
        var printService = new PrintService();

        _viewModel = new MainWindowViewModel(customerService, orderService, exportService, fileDialogService, printService);
        DataContext = _viewModel;
    }

    private async void OrdersDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: not null })
        {
            await _viewModel.OpenSelectedOrderAsync();
        }
    }

    private async void OrdersDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        var sortMember = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortMember) && e.Column is DataGridBoundColumn { Binding: Binding binding })
        {
            sortMember = binding.Path?.Path;
        }

        var descending = e.Column.SortDirection != ListSortDirection.Ascending;

        foreach (var col in OrdersDataGrid.Columns)
        {
            col.SortDirection = null;
        }

        e.Column.SortDirection = descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        _viewModel.SetSort(sortMember ?? "DateTime", descending);
        await _viewModel.ApplyFiltersAsync(force: true, showValidationError: false);
    }

}

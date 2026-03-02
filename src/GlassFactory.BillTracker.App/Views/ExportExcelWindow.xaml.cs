using System.Windows;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.App.Services;

namespace GlassFactory.BillTracker.App.Views;

public partial class ExportExcelWindow : Window
{
    private readonly IFileDialogService _fileDialogService;
    private readonly int _selectedOrderCount;

    public ExportExcelWindow(
        IEnumerable<(Guid? Value, string DisplayName)> customers,
        int selectedOrderCount,
        string initialPath,
        IFileDialogService fileDialogService,
        bool preferSelectedMode)
    {
        InitializeComponent();
        _fileDialogService = fileDialogService;
        _selectedOrderCount = selectedOrderCount;

        CustomerComboBox.ItemsSource = customers.ToList();
        CustomerComboBox.SelectedIndex = 0;

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            OutputPathTextBox.Text = initialPath;
        }

        if (preferSelectedMode)
        {
            UseSelectedOrdersCheckBox.IsChecked = true;
        }

        if (selectedOrderCount <= 0)
        {
            UseSelectedOrdersCheckBox.Content = "导出选中订单（当前无勾选）";
        }

        UpdateExportButtonState();
    }

    public ExportExcelOptions? Options { get; private set; }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = _fileDialogService.SelectSaveFilePath(
            "导出 Excel",
            "Excel 文件 (*.xlsx)|*.xlsx",
            ".xlsx",
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"BillTracker_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            OutputPathTextBox.Text = selectedPath;
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var outputPath = OutputPathTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            MessageBox.Show("请选择导出文件路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var start = StartDatePicker.SelectedDate;
        var end = EndDatePicker.SelectedDate;
        if (UseDateRangeCheckBox.IsChecked == true && start.HasValue && end.HasValue && start > end)
        {
            (start, end) = (end, start);
        }

        Options = new ExportExcelOptions
        {
            UseSelectedOrders = UseSelectedOrdersCheckBox.IsChecked == true,
            UseDateRange = UseDateRangeCheckBox.IsChecked == true,
            StartDate = start,
            EndDate = end,
            UseCustomerFilter = UseCustomerCheckBox.IsChecked == true,
            CustomerId = (Guid?)CustomerComboBox.SelectedValue,
            OutputPath = outputPath
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UseSelectedOrdersCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateExportButtonState();
    }

    private void UpdateExportButtonState()
    {
        var selectedMode = UseSelectedOrdersCheckBox.IsChecked == true;
        ExportButton.IsEnabled = !(selectedMode && _selectedOrderCount <= 0);
    }
}

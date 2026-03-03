using System.Windows;
using System.Windows.Controls;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.App.Services;

namespace GlassFactory.BillTracker.App.Views;

public partial class PrintBillsWindow : Window
{
    private readonly IReadOnlyList<OrderExportDto> _orders;
    private readonly IPrintService? _printService;
    private PrintDialog? _selectedPrintDialog;
    private bool _isInitializing;

    public PrintBillsWindow(IReadOnlyList<OrderExportDto> orders, IPrintService printService)
    {
        _isInitializing = true;
        _orders = orders ?? Array.Empty<OrderExportDto>();
        _printService = printService;

        InitializeComponent();
        DataContext = this;
        OrdersListBox.ItemsSource = _orders;
        PrinterTextBlock.Text = "未选择打印机";
        if (TemplateComboBox.SelectedItem is null && TemplateComboBox.Items.Count > 0)
        {
            TemplateComboBox.SelectedIndex = 0;
        }

        _isInitializing = false;
        ApplyTemplateUiState();
    }

    private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        if (TemplateComboBox is null || DotMatrixHeightComboBox is null)
        {
            return;
        }

        if (TemplateComboBox.SelectedItem is null)
        {
            return;
        }

        if (DataContext is null)
        {
            return;
        }

        ApplyTemplateUiState();
    }

    private void SelectPrinterButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _selectedPrintDialog = dialog;
        PrinterTextBlock.Text = dialog.PrintQueue?.FullName ?? "默认打印机";
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (_orders.Count == 0)
        {
            MessageBox.Show("请先选择要打印的订单", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_printService is null)
        {
            MessageBox.Show("打印服务不可用，请重启后重试。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var printDialog = _selectedPrintDialog ?? new PrintDialog();
        if (_selectedPrintDialog is null && printDialog.ShowDialog() != true)
        {
            return;
        }

        var options = BuildOptions();
        var document = options.TemplateKind == PrintTemplateKind.DotMatrix
            ? _printService.RenderDotMatrixTriplicate(_orders, options)
            : _printService.RenderA4(_orders, options);

        if (document.Pages.Count == 0)
        {
            MessageBox.Show("没有可打印内容。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        printDialog.PrintDocument(document.DocumentPaginator, "GlassFactoryBillTracker_Bills");
        DialogResult = true;
        Close();
    }

    private PrintBillOptions BuildOptions()
    {
        var templateTag = (TemplateComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var heightTag = (DotMatrixHeightComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        var templateKind = string.Equals(templateTag, "A4", StringComparison.OrdinalIgnoreCase)
            ? PrintTemplateKind.A4
            : PrintTemplateKind.DotMatrix;

        var heightMode = heightTag switch
        {
            "Full" => DotMatrixHeightMode.Full,
            "Half" => DotMatrixHeightMode.Half,
            _ => DotMatrixHeightMode.Third
        };

        return new PrintBillOptions
        {
            HeaderText = string.IsNullOrWhiteSpace(HeaderTextBox.Text) ? "亿达夹丝玻璃" : HeaderTextBox.Text.Trim(),
            UseCustomerPhone = UseCustomerPhoneCheckBox.IsChecked == true,
            CustomPhone = string.IsNullOrWhiteSpace(CustomPhoneTextBox.Text) ? null : CustomPhoneTextBox.Text.Trim(),
            TemplateKind = templateKind,
            DotMatrixHeightMode = heightMode
        };
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyTemplateUiState()
    {
        var tag = (TemplateComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        DotMatrixHeightComboBox.IsEnabled = string.Equals(tag, "DotMatrix", StringComparison.OrdinalIgnoreCase);
    }
}

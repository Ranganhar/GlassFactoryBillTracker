using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.App.Services;

public sealed class PrintService : IPrintService
{
    public FixedDocument RenderDotMatrixTriplicate(IReadOnlyList<OrderExportDto> orders, PrintBillOptions options)
    {
        options ??= new PrintBillOptions();
        var document = new FixedDocument();
        if (orders is null || orders.Count == 0)
        {
            return document;
        }

        var pageWidth = MmToDip(241);
        var copyHeightMm = GetDotMatrixCopyHeight(options.DotMatrixHeightMode);
        var copiesPerPage = GetCopiesPerPage(options.DotMatrixHeightMode);
        var pageHeight = MmToDip(copyHeightMm * copiesPerPage);

        foreach (var order in orders)
        {
            var pageContent = new PageContent();
            var fixedPage = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = pageWidth,
                Height = pageHeight
            };

            for (var i = 0; i < copiesPerPage; i++)
            {
                var copyPanel = CreateBillCopyPanel(order, options, pageWidth, MmToDip(copyHeightMm), isDotMatrix: true);
                stack.Children.Add(copyPanel);
            }

            fixedPage.Children.Add(stack);
            ((IAddChild)pageContent).AddChild(fixedPage);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    public FixedDocument RenderA4(IReadOnlyList<OrderExportDto> orders, PrintBillOptions options)
    {
        options ??= new PrintBillOptions();
        var document = new FixedDocument();
        if (orders is null || orders.Count == 0)
        {
            return document;
        }

        var pageWidth = MmToDip(210);
        var pageHeight = MmToDip(297);

        foreach (var order in orders)
        {
            var pageContent = new PageContent();
            var fixedPage = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            var copyPanel = CreateBillCopyPanel(order, options, pageWidth, pageHeight, isDotMatrix: false);
            fixedPage.Children.Add(copyPanel);

            ((IAddChild)pageContent).AddChild(fixedPage);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    private static Border CreateBillCopyPanel(OrderExportDto order, PrintBillOptions options, double pageWidth, double pageHeight, bool isDotMatrix)
    {
        options ??= new PrintBillOptions();
        order ??= new OrderExportDto();

        var outerBorder = new Border
        {
            Width = pageWidth,
            Height = pageHeight,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.8),
            Padding = new Thickness(isDotMatrix ? 8 : 20),
            Margin = new Thickness(0)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(options.HeaderText) ? "亿达夹丝玻璃" : options.HeaderText,
            FontWeight = FontWeights.Bold,
            FontSize = isDotMatrix ? 16 : 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var contactPhone = options.UseCustomerPhone ? (order.CustomerPhone ?? string.Empty) : (options.CustomPhone ?? string.Empty);
        var meta = new TextBlock
        {
            Text = $"订单号: {order.OrderNo ?? string.Empty}    日期: {order.DateTime:yyyy-MM-dd HH:mm:ss}    客户: {order.CustomerName ?? string.Empty}    电话: {contactPhone}",
            FontSize = isDotMatrix ? 11 : 13,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(meta, 1);
        root.Children.Add(meta);

        var table = new Grid { Margin = new Thickness(0, 2, 0, 6) };
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });

        var headers = new[] { "型号", "长", "宽", "数量", "单价", "打孔费", "其他费", "金额" };
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var col = 0; col < headers.Length; col++)
        {
            var cell = CreateCell(headers[col], bold: true, isDotMatrix);
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, col);
            table.Children.Add(cell);
        }

        var rowIndex = 1;
        foreach (var item in order.Items ?? Array.Empty<OrderExportItemDto>())
        {
            if (item is null)
            {
                continue;
            }

            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var values = new[]
            {
                item.Model ?? string.Empty,
                item.GlassLengthMm.ToString("F0"),
                item.GlassWidthMm.ToString("F0"),
                item.Quantity.ToString(),
                item.GlassUnitPricePerM2.ToString("F4"),
                item.HoleFee.ToString("F4"),
                item.OtherFee.ToString("F4"),
                item.Amount.ToString("F4")
            };

            for (var col = 0; col < values.Length; col++)
            {
                var cell = CreateCell(values[col], bold: false, isDotMatrix);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, col);
                table.Children.Add(cell);
            }

            rowIndex++;
        }

        Grid.SetRow(table, 2);
        root.Children.Add(table);

        var footer = new TextBlock
        {
            Text = $"合计金额: {OrderAmountCalculator.Round(order.TotalAmount):F4}      签字: __________________",
            FontWeight = FontWeights.Bold,
            FontSize = isDotMatrix ? 12 : 14,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0)
        };
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        outerBorder.Child = root;
        return outerBorder;
    }

    private static Border CreateCell(string text, bool bold, bool isDotMatrix)
    {
        return new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(3, 2, 3, 2),
            Child = new TextBlock
            {
                Text = text,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                FontSize = isDotMatrix ? 10 : 11,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static double MmToDip(double mm)
    {
        return mm * 96d / 25.4d;
    }

    private static double GetDotMatrixCopyHeight(DotMatrixHeightMode mode)
    {
        return mode switch
        {
            DotMatrixHeightMode.Full => 280d,
            DotMatrixHeightMode.Half => 140d,
            _ => 93d
        };
    }

    private static int GetCopiesPerPage(DotMatrixHeightMode mode)
    {
        return mode switch
        {
            DotMatrixHeightMode.Full => 1,
            DotMatrixHeightMode.Half => 2,
            _ => 3
        };
    }
}

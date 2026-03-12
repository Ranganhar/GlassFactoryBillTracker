using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Controls;
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
            var slotHeight = MmToDip(copyHeightMm);
            var generatedCopies = 0;

            for (var i = 0; i < copiesPerPage; i++)
            {
                var copyPanel = CreateBillCopyPanel(order, options, pageWidth, slotHeight, isDotMatrix: true);
                FixedPage.SetLeft(copyPanel, 0d);
                FixedPage.SetTop(copyPanel, i * slotHeight);
                fixedPage.Children.Add(copyPanel);
                generatedCopies++;
            }

            ValidateDotMatrixBlockCount(copiesPerPage, generatedCopies, options.DotMatrixHeightMode);
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
        var baseFontSize = Math.Max(8d, options.FontSize);

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
            FontSize = isDotMatrix ? baseFontSize + 4 : baseFontSize + 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var contactPhone = options.UseCustomerPhone ? (order.CustomerPhone ?? string.Empty) : (options.CustomPhone ?? string.Empty);
        var meta = new TextBlock
        {
            Text = $"订单号: {order.OrderNo ?? string.Empty}    日期: {order.DateTime:yyyy-MM-dd HH:mm:ss}    客户: {order.CustomerName ?? string.Empty}    电话: {contactPhone}",
            FontSize = baseFontSize,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(meta, 1);
        root.Children.Add(meta);

        var table = new Grid { Margin = new Thickness(0, 2, 0, 6) };
        var contentWidth = Math.Max(100d, pageWidth - 24d);
        foreach (var width in BuildColumnWidths(contentWidth))
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        }

        var headers = new[] { "型号", "长", "宽", "数量", "单价", "打孔", "其他费用", "金额", "备注" };
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var col = 0; col < headers.Length; col++)
        {
            var cell = CreateCell(headers[col], bold: true, isDotMatrix, baseFontSize, TextAlignment.Center, wrap: false, trim: true);
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
                item.GlassLengthMm.ToString("F1"),
                item.GlassWidthMm.ToString("F1"),
                item.Quantity.ToString(),
                Math.Round(item.GlassUnitPricePerM2, 0, MidpointRounding.AwayFromZero).ToString("F0"),
                Math.Round(item.HoleFee, 0, MidpointRounding.AwayFromZero).ToString("F0"),
                Math.Round(item.OtherFee, 0, MidpointRounding.AwayFromZero).ToString("F0"),
                OrderAmountCalculator.Round(item.Amount).ToString("F2"),
                item.Note ?? string.Empty
            };

            for (var col = 0; col < values.Length; col++)
            {
                var alignment = col is >= 1 and <= 7 ? TextAlignment.Right : TextAlignment.Left;
                var trim = col == 8;
                var cell = CreateCell(values[col], bold: false, isDotMatrix, baseFontSize, alignment, wrap: !trim, trim: trim);
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, col);
                table.Children.Add(cell);
            }

            rowIndex++;
        }

        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var totalCell = CreateCell($"合计：{OrderAmountCalculator.Round(order.TotalAmount):F2}", bold: true, isDotMatrix, baseFontSize, TextAlignment.Right, wrap: false, trim: false);
        Grid.SetRow(totalCell, rowIndex);
        Grid.SetColumn(totalCell, 0);
        Grid.SetColumnSpan(totalCell, headers.Length);
        table.Children.Add(totalCell);

        Grid.SetRow(table, 2);
        root.Children.Add(table);

        outerBorder.Child = root;
        return outerBorder;
    }

    private static Border CreateCell(string text, bool bold, bool isDotMatrix, double baseFontSize, TextAlignment alignment, bool wrap, bool trim)
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
                FontSize = isDotMatrix ? Math.Max(8d, baseFontSize - 1) : baseFontSize,
                TextAlignment = alignment,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None
            }
        };
    }

    private static IReadOnlyList<double> BuildColumnWidths(double contentWidth)
    {
        var weights = new[] { 1.35d, 0.75d, 0.75d, 0.65d, 0.85d, 0.75d, 0.75d, 0.9d, 1.3d };
        var sum = weights.Sum();
        return weights.Select(x => contentWidth * x / sum).ToArray();
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

    [Conditional("DEBUG")]
    private static void ValidateDotMatrixBlockCount(int expected, int actual, DotMatrixHeightMode mode)
    {
        Debug.Assert(expected == actual, $"Dot-matrix visual block mismatch. Mode={mode}, expected={expected}, actual={actual}.");
        Debug.WriteLine($"Dot-matrix visual block count checked. Mode={mode}, expected={expected}, actual={actual}.");
    }
}

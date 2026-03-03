using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.App.Services;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.App.ViewModels.Rows;
using GlassFactory.BillTracker.App.Views;
using GlassFactory.BillTracker.Data.Exports;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Enums;
using Serilog;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ICustomerService _customerService;
    private readonly IOrderService _orderService;
    private readonly IExportService _exportService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IPrintService _printService;

    private bool _suppressAutoApply;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _queryCts;
    private string? _lastFilterSignature;

    private string _sortBy = "DateTime";
    private bool _sortDescending = true;
    private bool _updatingSelection;
    private int _selectedOrderCount;

    public HashSet<Guid> SelectedOrderIds { get; } = new();

    public ObservableCollection<CustomerListItemViewModel> Customers { get; } = new();
    public ObservableCollection<OrderListItemViewModel> Orders { get; } = new();
    public ObservableCollection<FilterChipItem> FilterChips { get; } = new();

    public IReadOnlyList<LookupItem<PaymentMethod?>> PaymentMethodFilters { get; } =
        new List<LookupItem<PaymentMethod?>>
        {
            new() { DisplayName = "全部支付方式", Value = null }
        }.Concat(Enum.GetValues<PaymentMethod>().Select(x => new LookupItem<PaymentMethod?>
        {
            DisplayName = x.ToString(),
            Value = x
        })).ToList();

    public IReadOnlyList<LookupItem<OrderStatus?>> OrderStatusFilters { get; } =
        new List<LookupItem<OrderStatus?>>
        {
            new() { DisplayName = "全部状态", Value = null }
        }.Concat(Enum.GetValues<OrderStatus>().Select(x => new LookupItem<OrderStatus?>
        {
            DisplayName = x.ToString(),
            Value = x
        })).ToList();

    private CustomerListItemViewModel? _selectedCustomer;
    private OrderListItemViewModel? _selectedOrder;
    private LookupItem<PaymentMethod?>? _selectedPaymentFilter;
    private LookupItem<OrderStatus?>? _selectedStatusFilter;

    private DateTime? _startDate;
    private DateTime? _endDate;
    private string? _minAmountText;
    private string? _maxAmountText;
    private string? _orderSearchKeyword;
    private string? _customerSearchKeyword;

    private int _resultCount;
    private decimal _resultTotalAmount;

    public CustomerListItemViewModel? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value) && !_suppressAutoApply)
            {
                _ = ApplyFiltersAsync(force: false, showValidationError: false);
            }
        }
    }

    public OrderListItemViewModel? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (SetProperty(ref _selectedOrder, value))
            {
                DeleteOrderCommand.RaiseCanExecuteChanged();
                EditOrderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public LookupItem<PaymentMethod?>? SelectedPaymentFilter
    {
        get => _selectedPaymentFilter;
        set
        {
            if (SetProperty(ref _selectedPaymentFilter, value))
            {
                DebounceApplyFilters();
            }
        }
    }

    public LookupItem<OrderStatus?>? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                DebounceApplyFilters();
            }
        }
    }

    public DateTime? StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                DebounceApplyFilters();
            }
        }
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set
        {
            if (SetProperty(ref _endDate, value))
            {
                DebounceApplyFilters();
            }
        }
    }

    public string? MinAmountText
    {
        get => _minAmountText;
        set
        {
            if (SetProperty(ref _minAmountText, value))
            {
                DebounceApplyFilters();
            }
        }
    }

    public string? MaxAmountText
    {
        get => _maxAmountText;
        set
        {
            if (SetProperty(ref _maxAmountText, value))
            {
                DebounceApplyFilters();
            }
        }
    }

    public string? OrderSearchKeyword
    {
        get => _orderSearchKeyword;
        set
        {
            if (SetProperty(ref _orderSearchKeyword, value))
            {
                DebounceApplyFilters();
            }
        }
    }

    public string? CustomerSearchKeyword
    {
        get => _customerSearchKeyword;
        set => SetProperty(ref _customerSearchKeyword, value);
    }

    public int ResultCount
    {
        get => _resultCount;
        private set
        {
            if (SetProperty(ref _resultCount, value))
            {
                OnPropertyChanged(nameof(ResultSummaryText));
            }
        }
    }

    public decimal ResultTotalAmount
    {
        get => _resultTotalAmount;
        private set
        {
            if (SetProperty(ref _resultTotalAmount, value))
            {
                OnPropertyChanged(nameof(ResultSummaryText));
            }
        }
    }

    public string ResultSummaryText => $"共 {ResultCount} 条订单，合计金额 SumTotal={ResultTotalAmount:F4}";

    public int SelectedOrderCount
    {
        get => _selectedOrderCount;
        private set => SetProperty(ref _selectedOrderCount, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand NewOrderCommand { get; }
    public RelayCommand EditOrderCommand { get; }
    public RelayCommand DeleteOrderCommand { get; }
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand ExportJsonCommand { get; }
    public RelayCommand SelectAllOrdersCommand { get; }
    public RelayCommand ClearOrderSelectionCommand { get; }
    public RelayCommand ExportSelectedCommand { get; }
    public RelayCommand PrintSelectedCommand { get; }
    public RelayCommand DeleteSelectedOrdersCommand { get; }

    public RelayCommand AddCustomerCommand { get; }
    public RelayCommand EditCustomerCommand { get; }
    public RelayCommand DeleteCustomerCommand { get; }
    public RelayCommand SearchCustomersCommand { get; }
    public RelayCommand<string> RemoveChipCommand { get; }

    public MainWindowViewModel(
        ICustomerService customerService,
        IOrderService orderService,
        IExportService exportService,
        IFileDialogService fileDialogService,
        IPrintService printService)
    {
        _customerService = customerService;
        _orderService = orderService;
        _exportService = exportService;
        _fileDialogService = fileDialogService;
        _printService = printService;

        SelectedPaymentFilter = PaymentMethodFilters.FirstOrDefault();
        SelectedStatusFilter = OrderStatusFilters.FirstOrDefault();

        RefreshCommand = new RelayCommand(() => _ = ApplyFiltersAsync(force: true, showValidationError: true));
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        SearchCommand = new RelayCommand(() => _ = ApplyFiltersAsync(force: true, showValidationError: true));
        NewOrderCommand = new RelayCommand(() => ExecuteUiAction(() => OpenOrderDialogAsync(null), "新建订单"));
        EditOrderCommand = new RelayCommand(() => ExecuteUiAction(EditSelectedOrderAsync, "编辑订单"), () => SelectedOrder is not null);
        DeleteOrderCommand = new RelayCommand(() => ExecuteUiAction(DeleteSelectedOrderAsync, "删除订单"), () => SelectedOrder is not null);
        ExportExcelCommand = new RelayCommand(() => ExecuteUiAction(() => ExportExcelAsync(exportSelectedOnly: false), "导出Excel"));
        ExportJsonCommand = new RelayCommand(() => ExecuteUiAction(ExportJsonAsync, "导出JSON"));
        SelectAllOrdersCommand = new RelayCommand(SelectAllCurrentResult);
        ClearOrderSelectionCommand = new RelayCommand(ClearSelection);
        ExportSelectedCommand = new RelayCommand(() => ExecuteUiAction(() => ExportExcelAsync(exportSelectedOnly: true), "导出选中订单"), () => SelectedOrderIds.Count > 0);
        PrintSelectedCommand = new RelayCommand(() => ExecuteUiAction(PrintSelectedAsync, "打印选中订单"));
        DeleteSelectedOrdersCommand = new RelayCommand(() => ExecuteUiAction(DeleteSelectedOrdersAsync, "删除选中订单"));

        AddCustomerCommand = new RelayCommand(() => ExecuteUiAction(() => OpenCustomerDialogAsync(null), "新增客户"));
        EditCustomerCommand = new RelayCommand(() => ExecuteUiAction(EditSelectedCustomerAsync, "编辑客户"));
        DeleteCustomerCommand = new RelayCommand(() => ExecuteUiAction(DeleteSelectedCustomerAsync, "删除客户"));
        SearchCustomersCommand = new RelayCommand(() => ExecuteUiAction(LoadCustomersAsync, "客户搜索"));

        RemoveChipCommand = new RelayCommand<string>(chipKey =>
        {
            if (!string.IsNullOrWhiteSpace(chipKey))
            {
                RemoveFilterChip(chipKey);
            }
        });

        _ = RefreshAllAsync();
    }

    public bool IsOrderEmpty => Orders.Count == 0;
    public bool IsCustomerEmpty => Customers.Count <= 1;

    public async Task RefreshAllAsync()
    {
        await LoadCustomersAsync();
        await ApplyFiltersAsync(force: true, showValidationError: false);
    }

    public void DebounceApplyFilters()
    {
        if (_suppressAutoApply)
        {
            return;
        }

        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _ = ApplyFiltersAsync(force: false, showValidationError: false);
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    public async Task ApplyFiltersAsync(bool force, bool showValidationError, Guid? preferredOrderId = null)
    {
        try
        {
            var selectedOrderId = preferredOrderId ?? SelectedOrder?.Id;
            if (!TryBuildFilter(showValidationError, out var filter))
            {
                return;
            }

            var signature = BuildFilterSignature(filter);
            if (!force && signature == _lastFilterSignature)
            {
                return;
            }

            _queryCts?.Cancel();
            _queryCts = new CancellationTokenSource();
            var token = _queryCts.Token;

            var result = await _orderService.QueryOrdersAsync(filter, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            _lastFilterSignature = signature;
            Orders.Clear();

            _updatingSelection = true;
            foreach (var row in result.Rows)
            {
                var rowVm = new OrderListItemViewModel
                {
                    Id = row.Id,
                    IsSelected = SelectedOrderIds.Contains(row.Id),
                    OrderNo = row.OrderNo,
                    DateTime = row.DateTime,
                    CustomerName = row.CustomerName,
                    PaymentMethod = row.PaymentMethod,
                    OrderStatus = row.OrderStatus,
                    TotalAmount = row.TotalAmount,
                    Note = row.Note,
                    AttachmentPath = row.AttachmentPath
                };

                rowVm.SelectionChangedCallback = OnOrderSelectionChanged;
                Orders.Add(rowVm);
            }
            _updatingSelection = false;
            UpdateSelectionSummary();

            ResultCount = result.TotalCount;
            ResultTotalAmount = result.SumTotalAmount;
            RestoreSelection(selectedOrderId);
            UpdateFilterChips(filter);

            OnPropertyChanged(nameof(IsOrderEmpty));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "刷新订单失败");
            MessageBox.Show($"刷新订单失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void SetSort(string sortBy, bool descending)
    {
        _sortBy = string.IsNullOrWhiteSpace(sortBy) ? "DateTime" : sortBy;
        _sortDescending = descending;
    }

    public async Task TriggerImmediateSearchAsync(bool showValidationError)
    {
        _debounceCts?.Cancel();
        await ApplyFiltersAsync(force: true, showValidationError: showValidationError);
    }

    public void ClearOrderSearchText()
    {
        OrderSearchKeyword = string.Empty;
    }

    public async Task OpenSelectedOrderAsync()
    {
        await EditSelectedOrderAsync();
    }

    private async Task LoadCustomersAsync()
    {
        try
        {
            var previousCustomerId = SelectedCustomer?.Id;
            var customers = await _customerService.GetCustomersAsync(CustomerSearchKeyword);

            _suppressAutoApply = true;
            Customers.Clear();
            Customers.Add(new CustomerListItemViewModel
            {
                Id = null,
                Name = "全部客户",
                IsAllCustomers = true
            });

            foreach (var customer in customers)
            {
                Customers.Add(new CustomerListItemViewModel
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    IsAllCustomers = false
                });
            }

            SelectedCustomer = Customers.FirstOrDefault(x => x.Id == previousCustomerId) ?? Customers.FirstOrDefault();
            _suppressAutoApply = false;

            OnPropertyChanged(nameof(IsCustomerEmpty));
        }
        catch (Exception ex)
        {
            _suppressAutoApply = false;
            Log.Error(ex, "加载客户失败");
            MessageBox.Show($"加载客户失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool TryBuildFilter(bool showValidationError, out OrderQueryFilter filter)
    {
        filter = new OrderQueryFilter
        {
            CustomerId = SelectedCustomer?.IsAllCustomers == true ? null : SelectedCustomer?.Id,
            StartDate = StartDate,
            EndDate = EndDate,
            PaymentMethod = SelectedPaymentFilter?.Value,
            OrderStatus = SelectedStatusFilter?.Value,
            Keyword = string.IsNullOrWhiteSpace(OrderSearchKeyword) ? null : OrderSearchKeyword.Trim(),
            SortBy = _sortBy,
            SortDescending = _sortDescending,
            IncludeWireTypeInKeyword = true
        };

        var minValid = TryParseAmount(MinAmountText, out var minAmount, out var minError);
        var maxValid = TryParseAmount(MaxAmountText, out var maxAmount, out var maxError);
        if (!minValid || !maxValid)
        {
            if (showValidationError)
            {
                MessageBox.Show(minError ?? maxError ?? "金额输入无效。", "筛选提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        filter.MinAmount = minAmount;
        filter.MaxAmount = maxAmount;

        if (filter.StartDate.HasValue && filter.EndDate.HasValue && filter.StartDate > filter.EndDate)
        {
            (filter.StartDate, filter.EndDate) = (filter.EndDate, filter.StartDate);
            _suppressAutoApply = true;
            StartDate = filter.StartDate;
            EndDate = filter.EndDate;
            _suppressAutoApply = false;

            if (showValidationError)
            {
                MessageBox.Show("开始日期大于结束日期，已自动交换。", "筛选提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        if (filter.MinAmount.HasValue && filter.MaxAmount.HasValue && filter.MinAmount > filter.MaxAmount)
        {
            (filter.MinAmount, filter.MaxAmount) = (filter.MaxAmount, filter.MinAmount);
            _suppressAutoApply = true;
            MinAmountText = filter.MinAmount.Value.ToString(CultureInfo.InvariantCulture);
            MaxAmountText = filter.MaxAmount.Value.ToString(CultureInfo.InvariantCulture);
            _suppressAutoApply = false;

            if (showValidationError)
            {
                MessageBox.Show("最小金额大于最大金额，已自动交换。", "筛选提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        if (filter.EndDate.HasValue)
        {
            filter.EndDate = filter.EndDate.Value.Date.AddDays(1).AddSeconds(-1);
        }

        return true;
    }

    private static bool TryParseAmount(string? text, out decimal? amount, out string? error)
    {
        amount = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!decimal.TryParse(text.Trim(), out var parsed))
        {
            error = $"金额“{text}”不是有效数字。";
            return false;
        }

        amount = parsed;
        return true;
    }

    private static string BuildFilterSignature(OrderQueryFilter filter)
    {
        return string.Join("|", new[]
        {
            filter.CustomerId?.ToString() ?? "ALL",
            filter.StartDate?.ToString("O") ?? "NULL",
            filter.EndDate?.ToString("O") ?? "NULL",
            filter.MinAmount?.ToString("F4", CultureInfo.InvariantCulture) ?? "NULL",
            filter.MaxAmount?.ToString("F4", CultureInfo.InvariantCulture) ?? "NULL",
            filter.PaymentMethod?.ToString() ?? "ALL",
            filter.OrderStatus?.ToString() ?? "ALL",
            filter.Keyword ?? "",
            filter.IncludeWireTypeInKeyword.ToString(),
            filter.SortBy,
            filter.SortDescending.ToString()
        });
    }

    private void UpdateFilterChips(OrderQueryFilter filter)
    {
        FilterChips.Clear();

        if (SelectedCustomer is { IsAllCustomers: false, Name: not null })
        {
            FilterChips.Add(new FilterChipItem { Key = "customer", Label = $"客户={SelectedCustomer.Name}" });
        }

        if (filter.OrderStatus.HasValue)
        {
            FilterChips.Add(new FilterChipItem { Key = "status", Label = $"状态={filter.OrderStatus.Value}" });
        }

        if (filter.PaymentMethod.HasValue)
        {
            FilterChips.Add(new FilterChipItem { Key = "payment", Label = $"支付方式={filter.PaymentMethod.Value}" });
        }

        if (filter.StartDate.HasValue || filter.EndDate.HasValue)
        {
            var left = filter.StartDate?.ToString("yyyy-MM-dd") ?? "-";
            var right = filter.EndDate?.ToString("yyyy-MM-dd") ?? "-";
            FilterChips.Add(new FilterChipItem { Key = "date", Label = $"日期={left}~{right}" });
        }

        if (filter.MinAmount.HasValue)
        {
            FilterChips.Add(new FilterChipItem { Key = "min", Label = $"金额>={filter.MinAmount.Value:F4}" });
        }

        if (filter.MaxAmount.HasValue)
        {
            FilterChips.Add(new FilterChipItem { Key = "max", Label = $"金额<={filter.MaxAmount.Value:F4}" });
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            FilterChips.Add(new FilterChipItem { Key = "keyword", Label = $"关键词={filter.Keyword}" });
        }
    }

    private void RemoveFilterChip(string chipKey)
    {
        _suppressAutoApply = true;

        switch (chipKey)
        {
            case "customer":
                SelectedCustomer = Customers.FirstOrDefault(x => x.IsAllCustomers);
                break;
            case "status":
                SelectedStatusFilter = OrderStatusFilters.FirstOrDefault();
                break;
            case "payment":
                SelectedPaymentFilter = PaymentMethodFilters.FirstOrDefault();
                break;
            case "date":
                StartDate = null;
                EndDate = null;
                break;
            case "min":
                MinAmountText = null;
                break;
            case "max":
                MaxAmountText = null;
                break;
            case "keyword":
                OrderSearchKeyword = null;
                break;
        }

        _suppressAutoApply = false;
        _ = ApplyFiltersAsync(force: true, showValidationError: false);
    }

    private void RestoreSelection(Guid? selectedOrderId)
    {
        if (!selectedOrderId.HasValue)
        {
            if (Orders.Count > 0)
            {
                SelectedOrder = Orders[0];
            }

            return;
        }

        SelectedOrder = Orders.FirstOrDefault(x => x.Id == selectedOrderId.Value) ?? Orders.FirstOrDefault();
    }

    private void ClearFilters()
    {
        _suppressAutoApply = true;

        StartDate = null;
        EndDate = null;
        MinAmountText = null;
        MaxAmountText = null;
        OrderSearchKeyword = null;
        SelectedPaymentFilter = PaymentMethodFilters.FirstOrDefault();
        SelectedStatusFilter = OrderStatusFilters.FirstOrDefault();
        SelectedCustomer = Customers.FirstOrDefault(x => x.IsAllCustomers) ?? Customers.FirstOrDefault();

        _suppressAutoApply = false;
        _ = ApplyFiltersAsync(force: true, showValidationError: false);
    }

    private async Task OpenCustomerDialogAsync(Guid? customerId)
    {
        try
        {
            Customer? existing = null;
            if (customerId.HasValue)
            {
                existing = await _customerService.GetByIdAsync(customerId.Value);
            }

            var vm = new CustomerEditViewModel(existing);
            var window = new CustomerEditWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            var isSaving = false;

            vm.Saved += async () =>
            {
                if (isSaving)
                {
                    return;
                }

                try
                {
                    isSaving = true;
                    vm.SetSaving(true);
                    await _customerService.SaveAsync(vm.BuildCustomer());
                    window.DialogResult = true;
                    window.Close();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "保存客户失败");
                    MessageBox.Show($"保存客户失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    if (window.IsVisible)
                    {
                        isSaving = false;
                        vm.SetSaving(false);
                    }
                }
            };

            vm.Canceled += () =>
            {
                window.DialogResult = false;
                window.Close();
            };

            var result = window.ShowDialog();
            if (result == true)
            {
                await LoadCustomersAsync();
                await ApplyFiltersAsync(force: true, showValidationError: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "客户编辑窗口打开失败");
            MessageBox.Show($"客户编辑窗口打开失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OpenOrderDialogAsync(Guid? orderId)
    {
        try
        {
            var customerEntities = await _customerService.GetCustomersAsync();
            if (customerEntities.Count == 0)
            {
                MessageBox.Show("请先新增客户，再创建订单。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Order? existingOrder = null;
            string orderNo;

            if (orderId.HasValue)
            {
                existingOrder = await _orderService.GetByIdAsync(orderId.Value);
                if (existingOrder is null)
                {
                    MessageBox.Show("订单不存在或已被删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await ApplyFiltersAsync(force: true, showValidationError: false);
                    return;
                }

                orderNo = existingOrder.OrderNo;
            }
            else
            {
                orderNo = await _orderService.GenerateOrderNoAsync(DateTime.Now);
            }

            var vm = new OrderEditViewModel(customerEntities, orderNo, existingOrder);
            var window = new OrderEditWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };

            var isSaving = false;

            vm.SelectAttachmentRequested += () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|所有文件|*.*",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    vm.SetSelectedAttachment(dialog.FileName);
                }
            };

            vm.Saved += async () =>
            {
                if (isSaving)
                {
                    return;
                }

                try
                {
                    isSaving = true;
                    vm.SetSaving(true);
                    var model = vm.BuildModel();
                    var saved = await _orderService.SaveAsync(model, vm.BuildItems(), vm.AttachmentSourcePath, vm.RemoveAttachment);
                    vm.AttachmentRelativePath = saved.AttachmentPath;
                    window.DialogResult = true;
                    window.Close();
                    await ApplyFiltersAsync(force: true, showValidationError: false, preferredOrderId: saved.Id);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "保存订单失败");
                    MessageBox.Show($"保存订单失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    if (window.IsVisible)
                    {
                        isSaving = false;
                        vm.SetSaving(false);
                    }
                }
            };

            vm.Canceled += () =>
            {
                window.DialogResult = false;
                window.Close();
            };

            window.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "订单编辑窗口打开失败");
            MessageBox.Show($"订单编辑窗口打开失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task EditSelectedOrderAsync()
    {
        if (SelectedOrder is null)
        {
            return;
        }

        await OpenOrderDialogAsync(SelectedOrder.Id);
    }

    private async Task DeleteSelectedOrderAsync()
    {
        if (SelectedOrder is null)
        {
            return;
        }

        var toDeleteId = SelectedOrder.Id;

        var confirm = MessageBox.Show(
            $"确认删除订单 {SelectedOrder.OrderNo} 吗？",
            "删除确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _orderService.DeleteAsync(toDeleteId);
            SelectedOrderIds.Remove(toDeleteId);
            UpdateSelectionSummary();
            await ApplyFiltersAsync(force: true, showValidationError: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除订单失败");
            MessageBox.Show($"删除订单失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task EditSelectedCustomerAsync()
    {
        if (SelectedCustomer is null || SelectedCustomer.IsAllCustomers || !SelectedCustomer.Id.HasValue)
        {
            MessageBox.Show("请选择具体客户后再编辑。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await OpenCustomerDialogAsync(SelectedCustomer.Id.Value);
    }

    private async Task DeleteSelectedCustomerAsync()
    {
        if (SelectedCustomer is null || SelectedCustomer.IsAllCustomers || !SelectedCustomer.Id.HasValue)
        {
            MessageBox.Show("请选择具体客户后再删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"确认删除客户“{SelectedCustomer.Name}”吗？",
            "删除确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _customerService.DeleteAsync(SelectedCustomer.Id.Value);
            await LoadCustomersAsync();
            await ApplyFiltersAsync(force: true, showValidationError: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除客户失败");
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ExportExcelAsync(bool exportSelectedOnly)
    {
        try
        {
            if (!TryBuildFilter(showValidationError: true, out var filter))
            {
                return;
            }

            var customers = await _customerService.GetCustomersAsync();
            var customerLookup = new List<(Guid? Value, string DisplayName)> { (null, "全部客户") };
            customerLookup.AddRange(customers.Select(x => ((Guid?)x.Id, x.Name)));

            var defaultDir = Path.Combine(AppRuntimeContext.DataDir, "exports");
            Directory.CreateDirectory(defaultDir);
            var defaultPath = Path.Combine(defaultDir, $"BillTracker_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            var dialog = new ExportExcelWindow(
                customerLookup,
                SelectedOrderIds.Count,
                defaultPath,
                _fileDialogService,
                exportSelectedOnly)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || dialog.Options is null)
            {
                return;
            }

            var options = dialog.Options;
            if (options.UseSelectedOrders || exportSelectedOnly)
            {
                if (SelectedOrderIds.Count == 0)
                {
                    MessageBox.Show("当前没有勾选订单，无法导出选中订单。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                filter.SelectedOrderIds = SelectedOrderIds.ToList();
                filter.CustomerId = null;
                filter.StartDate = null;
                filter.EndDate = null;
            }
            else
            {
                filter.SelectedOrderIds = null;

                if (options.UseDateRange)
                {
                    filter.StartDate = options.StartDate;
                    filter.EndDate = options.EndDate;
                    if (filter.EndDate.HasValue)
                    {
                        filter.EndDate = filter.EndDate.Value.Date.AddDays(1).AddSeconds(-1);
                    }
                }

                if (options.UseCustomerFilter)
                {
                    filter.CustomerId = options.CustomerId;
                }
            }

            var result = await _exportService.ExportExcelAsync(ToExportFilter(filter), AppRuntimeContext.DataDir, options.OutputPath);
            ShowExportResult("Excel", result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导出Excel失败");
            MessageBox.Show($"导出Excel失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExportJsonAsync()
    {
        try
        {
            if (!TryBuildFilter(showValidationError: true, out var filter))
            {
                return;
            }

            var defaultDir = Path.Combine(AppRuntimeContext.DataDir, "exports");
            Directory.CreateDirectory(defaultDir);
            var defaultFileName = $"GlassFactoryBillTracker_Orders_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            var path = _fileDialogService.SelectSaveFilePath(
                "导出 JSON",
                "JSON 文件 (*.json)|*.json",
                ".json",
                defaultDir,
                defaultFileName);

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var result = await _exportService.ExportJsonAsync(ToExportFilter(filter), AppRuntimeContext.DataDir, path);
            ShowExportResult("JSON", result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导出JSON失败");
            MessageBox.Show($"导出JSON失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static ExportOrderFilter ToExportFilter(OrderQueryFilter filter)
    {
        return new ExportOrderFilter
        {
            SelectedOrderIds = filter.SelectedOrderIds,
            CustomerId = filter.CustomerId,
            StartDate = filter.StartDate,
            EndDate = filter.EndDate,
            MinAmount = filter.MinAmount,
            MaxAmount = filter.MaxAmount,
            PaymentMethod = filter.PaymentMethod,
            OrderStatus = filter.OrderStatus,
            Keyword = filter.Keyword,
            IncludeWireTypeInKeyword = filter.IncludeWireTypeInKeyword
        };
    }

    private void OnOrderSelectionChanged(OrderListItemViewModel row, bool isSelected)
    {
        if (_updatingSelection)
        {
            return;
        }

        if (isSelected)
        {
            SelectedOrderIds.Add(row.Id);
        }
        else
        {
            SelectedOrderIds.Remove(row.Id);
        }

        UpdateSelectionSummary();
    }

    private void SelectAllCurrentResult()
    {
        _updatingSelection = true;
        foreach (var row in Orders)
        {
            row.IsSelected = true;
            SelectedOrderIds.Add(row.Id);
        }

        _updatingSelection = false;
        UpdateSelectionSummary();
    }

    private void ClearSelection()
    {
        SelectedOrderIds.Clear();
        _updatingSelection = true;
        foreach (var row in Orders)
        {
            row.IsSelected = false;
        }

        _updatingSelection = false;
        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        SelectedOrderCount = SelectedOrderIds.Count;
        ExportSelectedCommand.RaiseCanExecuteChanged();
        PrintSelectedCommand.RaiseCanExecuteChanged();
        DeleteSelectedOrdersCommand.RaiseCanExecuteChanged();
    }

    private Task PrintSelectedAsync()
    {
        return PrintSelectedInternalAsync();
    }

    private async Task PrintSelectedInternalAsync()
    {
        var selectedIds = SelectedOrderIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (selectedIds.Count == 0)
        {
            MessageBox.Show("请先选择要打印的订单", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var filter = new OrderQueryFilter
        {
            SelectedOrderIds = selectedIds,
            SortBy = "DateTime",
            SortDescending = true
        };

        var orders = await _orderService.QueryOrdersForExportAsync(filter) ?? new List<OrderExportDto>();
        if (orders.Count == 0)
        {
            MessageBox.Show("未找到可打印订单。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var printWindow = new PrintBillsWindow(orders, _printService)
        {
            Owner = Application.Current.MainWindow
        };
        printWindow.ShowDialog();
    }

    private async Task DeleteSelectedOrdersAsync()
    {
        var selectedIds = SelectedOrderIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (selectedIds.Count == 0)
        {
            MessageBox.Show("请先勾选要删除的订单。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to delete {selectedIds.Count} selected orders? This cannot be undone.",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await _orderService.DeleteOrdersAsync(selectedIds);

        SelectedOrderIds.Clear();
        UpdateSelectionSummary();
        await ApplyFiltersAsync(force: true, showValidationError: false);

        var summary = $"批量删除完成。\n请求数: {result.RequestedCount}\n成功: {result.DeletedCount}\n未找到: {result.NotFoundCount}\n失败: {result.FailedCount}";
        if (result.AttachmentCleanupFailedCount > 0)
        {
            summary += $"\n附件目录清理失败: {result.AttachmentCleanupFailedCount}";
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            summary += $"\n错误信息: {result.ErrorMessage}";
        }

        MessageBox.Show(summary, result.FailedCount > 0 ? "部分失败" : "完成", MessageBoxButton.OK,
            result.FailedCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private static void ShowExportResult(string exportType, ExportResult result)
    {
        var message = $"{exportType} 导出完成。\n\n保存位置：{result.FilePath}\n订单数：{result.OrdersCount}\n明细数：{result.ItemsCount}\n合计金额：{result.SumTotalAmount:F4}\n\n是否打开所在文件夹？";
        var open = MessageBox.Show(message, "导出成功", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (open != MessageBoxResult.Yes)
        {
            return;
        }

        var folder = Path.GetDirectoryName(result.FilePath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{result.FilePath}\"",
            UseShellExecute = true
        });
    }

    private void ExecuteUiAction(Func<Task> action, string actionName)
    {
        _ = ExecuteUiActionAsync(action, actionName);
    }

    private async Task ExecuteUiActionAsync(Func<Task> action, string actionName)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{ActionName}发生未处理异常", actionName);
            if (actionName == "打印选中订单")
            {
                var logPath = string.IsNullOrWhiteSpace(AppRuntimeContext.DataDir)
                    ? "(logs path unavailable)"
                    : Path.Combine(AppRuntimeContext.DataDir, "logs", "app.log");
                MessageBox.Show($"打印失败：{ex.Message}\n日志路径：{logPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show($"{actionName}失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

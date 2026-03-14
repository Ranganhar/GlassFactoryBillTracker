using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.Models;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.App.ViewModels.Rows;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Enums;
using GlassFactory.BillTracker.Domain.Services;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class OrderEditViewModel : ObservableObject
{
    private readonly IReadOnlyList<Customer> _customers;
    private readonly bool _isEditMode;

    private Guid _id;
    private string _orderNo = string.Empty;
    private DateTime _selectedDate = DateTime.Today;
    private int _selectedHour = DateTime.Now.Hour;
    private int _selectedMinute = DateTime.Now.Minute;
    private Customer? _selectedCustomer;
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.现金;
    private OrderStatus _selectedOrderStatus = OrderStatus.未收款;
    private string? _note;
    private decimal _totalAmount;
    private string? _attachmentSourcePath;
    private string? _attachmentRelativePath;
    private bool _removeAttachment;
    private bool _isSaving;
    private bool _isDirty;
    private bool _suppressDirtyTracking = true;

    private static readonly HashSet<string> TrackedPropertyNames = new()
    {
        nameof(SelectedDate),
        nameof(SelectedHour),
        nameof(SelectedMinute),
        nameof(SelectedCustomer),
        nameof(SelectedPaymentMethod),
        nameof(SelectedOrderStatus),
        nameof(Note),
        nameof(AttachmentSourcePath),
        nameof(AttachmentRelativePath),
        nameof(RemoveAttachment)
    };

    public ObservableCollection<OrderItemRowViewModel> Items { get; } = new();

    public IReadOnlyList<Customer> Customers => _customers;
    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(0, 24).ToList();
    public IReadOnlyList<int> Minutes { get; } = Enumerable.Range(0, 60).ToList();
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } = Enum.GetValues<PaymentMethod>();
    public IReadOnlyList<OrderStatus> OrderStatuses { get; } = Enum.GetValues<OrderStatus>();

    public Guid Id
    {
        get => _id;
        private set => SetProperty(ref _id, value);
    }

    public string OrderNo
    {
        get => _orderNo;
        set => SetProperty(ref _orderNo, value);
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    public int SelectedHour
    {
        get => _selectedHour;
        set => SetProperty(ref _selectedHour, value);
    }

    public int SelectedMinute
    {
        get => _selectedMinute;
        set => SetProperty(ref _selectedMinute, value);
    }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set => SetProperty(ref _selectedCustomer, value);
    }

    public PaymentMethod SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set => SetProperty(ref _selectedPaymentMethod, value);
    }

    public OrderStatus SelectedOrderStatus
    {
        get => _selectedOrderStatus;
        set => SetProperty(ref _selectedOrderStatus, value);
    }

    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public decimal TotalAmount
    {
        get => _totalAmount;
        private set => SetProperty(ref _totalAmount, value);
    }

    public string? AttachmentSourcePath
    {
        get => _attachmentSourcePath;
        set => SetProperty(ref _attachmentSourcePath, value);
    }

    public string? AttachmentRelativePath
    {
        get => _attachmentRelativePath;
        set => SetProperty(ref _attachmentRelativePath, value);
    }

    public bool RemoveAttachment
    {
        get => _removeAttachment;
        set => SetProperty(ref _removeAttachment, value);
    }

    public string? AttachmentDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AttachmentSourcePath))
            {
                return Path.GetFileName(AttachmentSourcePath);
            }

            if (!string.IsNullOrWhiteSpace(AttachmentRelativePath))
            {
                return Path.GetFileName(AttachmentRelativePath);
            }

            return null;
        }
    }

    public string? AttachmentPreviewAbsolutePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AttachmentSourcePath) && File.Exists(AttachmentSourcePath))
            {
                return AttachmentSourcePath;
            }

            if (string.IsNullOrWhiteSpace(AttachmentRelativePath))
            {
                return null;
            }

            var absolute = Path.Combine(Services.AppRuntimeContext.DataDir, AttachmentRelativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(absolute) ? absolute : null;
        }
    }

    public RelayCommand AddItemCommand { get; }
    public RelayCommand RemoveSelectedItemCommand { get; }
    public RelayCommand<OrderItemRowViewModel> DeleteSelectedItemCommand { get; }
    public RelayCommand<OrderItemRowViewModel> CopySelectedItemCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand ChooseAttachmentCommand { get; }
    public RelayCommand RemoveAttachmentCommand { get; }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    private OrderItemRowViewModel? _selectedItem;
    public OrderItemRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public event Action? Saved;
    public event Action? Canceled;
    public event Action? SelectAttachmentRequested;
    public event Action<string, int?>? ValidationFailed;

    public OrderEditViewModel(IReadOnlyList<Customer> customers, string orderNo, Order? existing = null)
    {
        _customers = customers;
        _isEditMode = existing is not null;

        PropertyChanged += OnSelfPropertyChanged;
        Items.CollectionChanged += OnItemsCollectionChanged;

        Id = existing?.Id ?? Guid.Empty;
        OrderNo = existing?.OrderNo ?? orderNo;

        var dateTime = existing?.DateTime ?? DateTime.Now;
        SelectedDate = dateTime.Date;
        SelectedHour = dateTime.Hour;
        SelectedMinute = dateTime.Minute;

        SelectedCustomer = customers.FirstOrDefault(x => x.Id == existing?.CustomerId);
        SelectedPaymentMethod = existing?.PaymentMethod ?? PaymentMethod.现金;
        SelectedOrderStatus = existing?.OrderStatus ?? OrderStatus.未收款;
        Note = existing?.Note;
        AttachmentRelativePath = existing?.AttachmentPath;

        if (existing?.Items is not null)
        {
            foreach (var item in existing.Items)
            {
                Items.Add(OrderItemRowViewModel.FromEntity(item, RecalculateTotal));
            }
        }

        if (Items.Count == 0)
        {
            Items.Add(new OrderItemRowViewModel(RecalculateTotal));
        }

        AddItemCommand = new RelayCommand(AddItem);
        RemoveSelectedItemCommand = new RelayCommand(RemoveSelectedItem);
        DeleteSelectedItemCommand = new RelayCommand<OrderItemRowViewModel>(DeleteSelectedItem);
        CopySelectedItemCommand = new RelayCommand<OrderItemRowViewModel>(CopySelectedItem);
        SaveCommand = new RelayCommand(OnSave, () => !IsSaving);
        CancelCommand = new RelayCommand(() => Canceled?.Invoke());
        ChooseAttachmentCommand = new RelayCommand(() => SelectAttachmentRequested?.Invoke());
        RemoveAttachmentCommand = new RelayCommand(RemoveAttachmentAction);

        RecalculateTotal();
        _suppressDirtyTracking = false;
        AcceptChanges();
    }

    public void SetSelectedAttachment(string filePath)
    {
        AttachmentSourcePath = filePath;
        RemoveAttachment = false;
        OnPropertyChanged(nameof(AttachmentDisplayName));
        OnPropertyChanged(nameof(AttachmentPreviewAbsolutePath));
    }

    public void AcceptChanges()
    {
        IsDirty = false;
    }

    public OrderEditModel BuildModel()
    {
        return new OrderEditModel
        {
            Id = Id,
            OrderNo = OrderNo,
            DateTime = BuildDateTime(),
            CustomerId = SelectedCustomer?.Id,
            PaymentMethod = SelectedPaymentMethod,
            OrderStatus = SelectedOrderStatus,
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note?.Trim(),
            AttachmentPath = AttachmentRelativePath
        };
    }

    public IReadOnlyList<OrderItem> BuildItems()
    {
        return Items.Select(x => x.ToEntity()).ToList();
    }

    public void SetSaving(bool isSaving)
    {
        IsSaving = isSaving;
    }

    private void AddItem()
    {
        Items.Add(new OrderItemRowViewModel(RecalculateTotal)
        {
            Quantity = 1,
            WireType = "默认丝"
        });
        RecalculateTotal();
    }

    private void RemoveSelectedItem()
    {
        if (SelectedItem is null)
        {
            return;
        }

        DeleteItemCore(SelectedItem, showEmptyMessage: false);
    }

    private void DeleteSelectedItem(OrderItemRowViewModel? sourceRow)
    {
        var row = sourceRow ?? SelectedItem;
        if (row is null)
        {
            MessageBox.Show("请先选择要删除的明细行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DeleteItemCore(row, showEmptyMessage: true);
    }

    private void DeleteItemCore(OrderItemRowViewModel row, bool showEmptyMessage)
    {
        Items.Remove(row);
        if (Items.Count == 0)
        {
            Items.Add(new OrderItemRowViewModel(RecalculateTotal));
        }

        if (showEmptyMessage && SelectedItem is null && Items.Count > 0)
        {
            SelectedItem = Items[0];
        }

        RecalculateTotal();
    }

    private void CopySelectedItem(OrderItemRowViewModel? sourceRow)
    {
        var rowToCopy = sourceRow ?? SelectedItem;
        if (rowToCopy is null)
        {
            MessageBox.Show("请先选择要复制的明细行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var index = Items.IndexOf(rowToCopy);
        if (index < 0)
        {
            MessageBox.Show("无法定位选中的明细行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var copied = rowToCopy.CloneForCopy(RecalculateTotal);
        Items.Insert(index + 1, copied);
        SelectedItem = copied;
        RecalculateTotal();
    }

    private void RemoveAttachmentAction()
    {
        AttachmentSourcePath = null;
        if (_isEditMode && !string.IsNullOrWhiteSpace(AttachmentRelativePath))
        {
            RemoveAttachment = true;
        }

        AttachmentRelativePath = null;
        OnPropertyChanged(nameof(AttachmentDisplayName));
        OnPropertyChanged(nameof(AttachmentPreviewAbsolutePath));
    }

    private void RecalculateTotal()
    {
        var entities = BuildItems();
        TotalAmount = OrderAmountCalculator.CalculateOrderTotal(entities);
    }

    private void OnSave()
    {
        if (IsSaving)
        {
            return;
        }

        if (SelectedCustomer is null)
        {
            MessageBox.Show("请选择客户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            ValidationFailed?.Invoke("Customer", null);
            return;
        }

        if (Items.Count == 0)
        {
            MessageBox.Show("至少需要一条订单明细。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        for (var i = 0; i < Items.Count; i++)
        {
            var row = Items[i];

            if (row.GlassLengthMm <= 0 || row.GlassWidthMm <= 0 || row.Quantity <= 0)
            {
                MessageBox.Show("明细的长/宽/数量必须大于0。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (row.Model.Length > 13)
            {
                MessageBox.Show("型号最多13个字符。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                ValidationFailed?.Invoke("Model", i);
                return;
            }

            if (string.IsNullOrWhiteSpace(row.Model))
            {
                MessageBox.Show("请填写型号", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                ValidationFailed?.Invoke("Model", i);
                return;
            }

            if (row.GlassUnitPricePerM2 < 0 || row.HoleFee < 0 || row.OtherFee < 0)
            {
                MessageBox.Show("明细单价与费用不能为负数。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!IsIntegerValue(row.GlassLengthMm) || !IsIntegerValue(row.GlassWidthMm))
            {
                MessageBox.Show("长和宽必须为整数（mm）。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!IsIntegerValue(row.GlassUnitPricePerM2) || !IsIntegerValue(row.HoleFee) || !IsIntegerValue(row.OtherFee))
            {
                MessageBox.Show("单价、打孔费、其他费用必须为整数。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        var duplicateIds = Items
            .Select(x => x.Id)
            .Where(x => x != Guid.Empty)
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            MessageBox.Show("明细行存在重复 ID，请重新复制行后再保存。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Saved?.Invoke();
    }

    private static bool IsIntegerValue(decimal value)
    {
        return decimal.Truncate(value) == value;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<OrderItemRowViewModel>())
            {
                oldItem.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<OrderItemRowViewModel>())
            {
                newItem.PropertyChanged -= OnItemPropertyChanged;
                newItem.PropertyChanged += OnItemPropertyChanged;
            }
        }

        if (!_suppressDirtyTracking)
        {
            MarkDirty();
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressDirtyTracking)
        {
            return;
        }

        if (!string.Equals(e.PropertyName, nameof(OrderItemRowViewModel.Amount), StringComparison.Ordinal))
        {
            MarkDirty();
        }
    }

    private void OnSelfPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressDirtyTracking || string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        if (TrackedPropertyNames.Contains(e.PropertyName))
        {
            MarkDirty();
        }
    }

    private void MarkDirty()
    {
        if (!_suppressDirtyTracking)
        {
            IsDirty = true;
        }
    }

    private DateTime BuildDateTime()
    {
        var date = SelectedDate.Date;
        return new DateTime(date.Year, date.Month, date.Day, SelectedHour, SelectedMinute, 0);
    }
}

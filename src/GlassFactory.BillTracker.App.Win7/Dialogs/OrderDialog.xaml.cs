using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using GlassFactory.BillTracker.Domain.Enums;

namespace GlassFactory.BillTracker.App.Win7.Dialogs
{
    public partial class OrderDialog : Window
    {
        private readonly IReadOnlyList<CustomerRecord> _customers;

        public OrderDialog(IReadOnlyList<CustomerRecord> customers, OrderRecord existing = null)
        {
            InitializeComponent();
            _customers = customers;

            Result = existing == null ? new OrderRecord
            {
                DateTime = DateTime.Now,
                PaymentMethod = PaymentMethod.现金,
                OrderStatus = OrderStatus.未收款,
                TotalAmount = 0m
            } : new OrderRecord
            {
                Id = existing.Id,
                OrderNo = existing.OrderNo,
                DateTime = existing.DateTime,
                CustomerId = existing.CustomerId,
                CustomerName = existing.CustomerName,
                PaymentMethod = existing.PaymentMethod,
                OrderStatus = existing.OrderStatus,
                TotalAmount = existing.TotalAmount,
                Note = existing.Note
            };

            CustomerComboBox.ItemsSource = _customers;
            PaymentComboBox.ItemsSource = Enum.GetValues(typeof(PaymentMethod));
            StatusComboBox.ItemsSource = Enum.GetValues(typeof(OrderStatus));

            OrderNoTextBox.Text = Result.OrderNo;
            DatePicker.SelectedDate = Result.DateTime.Date;
            CustomerComboBox.SelectedValue = Result.CustomerId == Guid.Empty ? null : (object)Result.CustomerId;
            PaymentComboBox.SelectedItem = Result.PaymentMethod;
            StatusComboBox.SelectedItem = Result.OrderStatus;
            NoteTextBox.Text = Result.Note;
        }

        public OrderRecord Result { get; private set; }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (CustomerComboBox.SelectedValue == null)
            {
                MessageBox.Show("请选择客户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                CustomerComboBox.Focus();
                return;
            }

            var customerId = (Guid)CustomerComboBox.SelectedValue;
            var customer = _customers.FirstOrDefault(x => x.Id == customerId);
            if (customer == null)
            {
                MessageBox.Show("客户不存在，请刷新后重试。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result.OrderNo = string.IsNullOrWhiteSpace(OrderNoTextBox.Text) ? string.Empty : OrderNoTextBox.Text.Trim();
            Result.DateTime = DatePicker.SelectedDate ?? DateTime.Now;
            Result.CustomerId = customerId;
            Result.CustomerName = customer.Name;
            Result.PaymentMethod = (PaymentMethod)(PaymentComboBox.SelectedItem ?? PaymentMethod.现金);
            Result.OrderStatus = (OrderStatus)(StatusComboBox.SelectedItem ?? OrderStatus.未收款);
            Result.Note = string.IsNullOrWhiteSpace(NoteTextBox.Text) ? null : NoteTextBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

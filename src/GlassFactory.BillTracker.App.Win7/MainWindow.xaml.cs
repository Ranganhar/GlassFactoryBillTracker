using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using GlassFactory.BillTracker.App.Win7.Dialogs;

namespace GlassFactory.BillTracker.App.Win7
{
    public partial class MainWindow : Window
    {
        private readonly Win7Repository _repository;
        private List<CustomerRecord> _customers = new List<CustomerRecord>();
        private List<OrderRecord> _orders = new List<OrderRecord>();

        public MainWindow()
        {
            InitializeComponent();
            _repository = new Win7Repository(App.DbPath);
            RefreshAll();
        }

        private void RefreshAll()
        {
            _customers = _repository.GetCustomers();
            CustomersGrid.ItemsSource = _customers;
            LoadOrders();
        }

        private void LoadOrders()
        {
            var selectedCustomer = CustomersGrid.SelectedItem as CustomerRecord;
            var keyword = string.IsNullOrWhiteSpace(KeywordTextBox.Text) ? null : KeywordTextBox.Text.Trim();
            _orders = _repository.GetOrders(selectedCustomer == null ? (Guid?)null : selectedCustomer.Id, keyword);
            OrdersGrid.ItemsSource = _orders;
        }

        private CustomerRecord SelectedCustomer
        {
            get { return CustomersGrid.SelectedItem as CustomerRecord; }
        }

        private OrderRecord SelectedOrder
        {
            get { return OrdersGrid.SelectedItem as OrderRecord; }
        }

        private void AddCustomer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CustomerDialog { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _repository.SaveCustomer(dialog.Result);
            RefreshAll();
        }

        private void EditCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("请先选择客户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new CustomerDialog(SelectedCustomer) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _repository.SaveCustomer(dialog.Result);
            RefreshAll();
        }

        private void DeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("请先选择客户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show("确认删除该客户？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            if (!_repository.DeleteCustomer(SelectedCustomer.Id))
            {
                MessageBox.Show("该客户存在订单，无法删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RefreshAll();
        }

        private void AddOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_customers.Count == 0)
            {
                MessageBox.Show("请先新增客户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OrderDialog(_customers) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _repository.SaveOrder(dialog.Result);
            LoadOrders();
        }

        private void EditOrder_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedOrder == null)
            {
                MessageBox.Show("请先选择订单", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OrderDialog(_customers, SelectedOrder) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _repository.SaveOrder(dialog.Result);
            LoadOrders();
        }

        private void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedOrder == null)
            {
                MessageBox.Show("请先选择订单", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show("确认删除该订单？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _repository.DeleteOrder(SelectedOrder.Id);
            LoadOrders();
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var output = Path.Combine(App.DataDir, "exports", "BillTracker_Win7_Orders_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".xlsx");
            var path = _repository.ExportExcel(output, _orders);
            MessageBox.Show("导出完成：" + path, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            var output = Path.Combine(App.DataDir, "exports", "BillTracker_Win7_Orders_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".json");
            var path = _repository.ExportJson(output, _orders);
            MessageBox.Show("导出完成：" + path, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PrintSelected_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Win7 version: printing not supported yet.", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            LoadOrders();
        }

        private void CustomersGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            LoadOrders();
        }
    }
}

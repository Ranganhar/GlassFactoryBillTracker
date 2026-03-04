using System.Windows;

namespace GlassFactory.BillTracker.App.Win7.Dialogs
{
    public partial class CustomerDialog : Window
    {
        public CustomerDialog(CustomerRecord existing = null)
        {
            InitializeComponent();
            Result = existing == null ? new CustomerRecord() : new CustomerRecord
            {
                Id = existing.Id,
                Name = existing.Name,
                Phone = existing.Phone,
                Address = existing.Address,
                Note = existing.Note
            };

            NameTextBox.Text = Result.Name;
            PhoneTextBox.Text = Result.Phone;
            AddressTextBox.Text = Result.Address;
            NoteTextBox.Text = Result.Note;
        }

        public CustomerRecord Result { get; private set; }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("请填写客户名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                NameTextBox.Focus();
                return;
            }

            Result.Name = NameTextBox.Text.Trim();
            Result.Phone = string.IsNullOrWhiteSpace(PhoneTextBox.Text) ? null : PhoneTextBox.Text.Trim();
            Result.Address = string.IsNullOrWhiteSpace(AddressTextBox.Text) ? null : AddressTextBox.Text.Trim();
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

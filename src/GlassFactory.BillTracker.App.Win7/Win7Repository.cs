using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using GlassFactory.BillTracker.Domain.Enums;
using Newtonsoft.Json;

namespace GlassFactory.BillTracker.App.Win7
{
    public sealed class Win7Repository
    {
        private readonly string _dbPath;

        public Win7Repository(string dbPath)
        {
            _dbPath = dbPath;
            Initialize();
        }

        private SQLiteConnection CreateConnection()
        {
            return new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;");
        }

        public void Initialize()
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var connection = CreateConnection())
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
CREATE TABLE IF NOT EXISTS Customers(
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Phone TEXT NULL,
    Address TEXT NULL,
    Note TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Orders(
    Id TEXT PRIMARY KEY,
    OrderNo TEXT NOT NULL UNIQUE,
    DateTime TEXT NOT NULL,
    CustomerId TEXT NOT NULL,
    PaymentMethod INTEGER NOT NULL,
    OrderStatus INTEGER NOT NULL,
    TotalAmount REAL NOT NULL,
    Note TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
";
                    command.ExecuteNonQuery();
                }
            }
        }

        public List<CustomerRecord> GetCustomers(string keyword = null)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Name, Phone, Address, Note FROM Customers";
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        command.CommandText += " WHERE Name LIKE @Keyword OR IFNULL(Phone,'') LIKE @Keyword";
                        command.Parameters.AddWithValue("@Keyword", "%" + keyword.Trim() + "%");
                    }

                    command.CommandText += " ORDER BY Name";
                    using (var reader = command.ExecuteReader())
                    {
                        var result = new List<CustomerRecord>();
                        while (reader.Read())
                        {
                            result.Add(new CustomerRecord
                            {
                                Id = Guid.Parse(reader.GetString(0)),
                                Name = reader.GetString(1),
                                Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Address = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Note = reader.IsDBNull(4) ? null : reader.GetString(4)
                            });
                        }

                        return result;
                    }
                }
            }
        }

        public void SaveCustomer(CustomerRecord customer)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var now = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
                    if (customer.Id == Guid.Empty)
                    {
                        customer.Id = Guid.NewGuid();
                        command.CommandText = @"INSERT INTO Customers(Id, Name, Phone, Address, Note, CreatedAt, UpdatedAt)
VALUES(@Id, @Name, @Phone, @Address, @Note, @CreatedAt, @UpdatedAt)";
                        command.Parameters.AddWithValue("@CreatedAt", now);
                    }
                    else
                    {
                        command.CommandText = @"UPDATE Customers SET Name=@Name, Phone=@Phone, Address=@Address, Note=@Note, UpdatedAt=@UpdatedAt WHERE Id=@Id";
                    }

                    command.Parameters.AddWithValue("@Id", customer.Id.ToString());
                    command.Parameters.AddWithValue("@Name", customer.Name.Trim());
                    command.Parameters.AddWithValue("@Phone", (object)customer.Phone ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Address", (object)customer.Address ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Note", (object)customer.Note ?? DBNull.Value);
                    command.Parameters.AddWithValue("@UpdatedAt", now);
                    command.ExecuteNonQuery();
                }
            }
        }

        public bool DeleteCustomer(Guid customerId)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var check = connection.CreateCommand())
                {
                    check.CommandText = "SELECT COUNT(1) FROM Orders WHERE CustomerId=@CustomerId";
                    check.Parameters.AddWithValue("@CustomerId", customerId.ToString());
                    var count = Convert.ToInt32(check.ExecuteScalar(), CultureInfo.InvariantCulture);
                    if (count > 0)
                    {
                        return false;
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM Customers WHERE Id=@Id";
                    command.Parameters.AddWithValue("@Id", customerId.ToString());
                    command.ExecuteNonQuery();
                }
            }

            return true;
        }

        public List<OrderRecord> GetOrders(Guid? customerId, string keyword)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT o.Id, o.OrderNo, o.DateTime, o.CustomerId, c.Name, o.PaymentMethod, o.OrderStatus, o.TotalAmount, o.Note
FROM Orders o
INNER JOIN Customers c ON c.Id = o.CustomerId
WHERE 1=1";

                    if (customerId.HasValue)
                    {
                        command.CommandText += " AND o.CustomerId = @CustomerId";
                        command.Parameters.AddWithValue("@CustomerId", customerId.Value.ToString());
                    }

                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        command.CommandText += " AND (o.OrderNo LIKE @Keyword OR c.Name LIKE @Keyword OR IFNULL(o.Note,'') LIKE @Keyword)";
                        command.Parameters.AddWithValue("@Keyword", "%" + keyword.Trim() + "%");
                    }

                    command.CommandText += " ORDER BY o.DateTime DESC";

                    using (var reader = command.ExecuteReader())
                    {
                        var result = new List<OrderRecord>();
                        while (reader.Read())
                        {
                            result.Add(new OrderRecord
                            {
                                Id = Guid.Parse(reader.GetString(0)),
                                OrderNo = reader.GetString(1),
                                DateTime = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                                CustomerId = Guid.Parse(reader.GetString(3)),
                                CustomerName = reader.GetString(4),
                                PaymentMethod = (PaymentMethod)reader.GetInt32(5),
                                OrderStatus = (OrderStatus)reader.GetInt32(6),
                                TotalAmount = Convert.ToDecimal(reader.GetDouble(7), CultureInfo.InvariantCulture),
                                Note = reader.IsDBNull(8) ? null : reader.GetString(8)
                            });
                        }

                        return result;
                    }
                }
            }
        }

        public void SaveOrder(OrderRecord order)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var now = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
                    if (order.Id == Guid.Empty)
                    {
                        order.Id = Guid.NewGuid();
                        if (string.IsNullOrWhiteSpace(order.OrderNo))
                        {
                            order.OrderNo = BuildOrderNo();
                        }

                        command.CommandText = @"INSERT INTO Orders(Id, OrderNo, DateTime, CustomerId, PaymentMethod, OrderStatus, TotalAmount, Note, CreatedAt, UpdatedAt)
VALUES(@Id, @OrderNo, @DateTime, @CustomerId, @PaymentMethod, @OrderStatus, @TotalAmount, @Note, @CreatedAt, @UpdatedAt)";
                        command.Parameters.AddWithValue("@CreatedAt", now);
                    }
                    else
                    {
                        command.CommandText = @"UPDATE Orders SET OrderNo=@OrderNo, DateTime=@DateTime, CustomerId=@CustomerId, PaymentMethod=@PaymentMethod, OrderStatus=@OrderStatus, TotalAmount=@TotalAmount, Note=@Note, UpdatedAt=@UpdatedAt WHERE Id=@Id";
                    }

                    command.Parameters.AddWithValue("@Id", order.Id.ToString());
                    command.Parameters.AddWithValue("@OrderNo", order.OrderNo.Trim());
                    command.Parameters.AddWithValue("@DateTime", order.DateTime.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("@CustomerId", order.CustomerId.ToString());
                    command.Parameters.AddWithValue("@PaymentMethod", (int)order.PaymentMethod);
                    command.Parameters.AddWithValue("@OrderStatus", (int)order.OrderStatus);
                    command.Parameters.AddWithValue("@TotalAmount", order.TotalAmount);
                    command.Parameters.AddWithValue("@Note", (object)order.Note ?? DBNull.Value);
                    command.Parameters.AddWithValue("@UpdatedAt", now);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteOrder(Guid orderId)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM Orders WHERE Id=@Id";
                    command.Parameters.AddWithValue("@Id", orderId.ToString());
                    command.ExecuteNonQuery();
                }
            }
        }

        public string ExportExcel(string outputPath, IReadOnlyList<OrderRecord> orders)
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Orders");
                sheet.Cell(1, 1).Value = "OrderNo";
                sheet.Cell(1, 2).Value = "DateTime";
                sheet.Cell(1, 3).Value = "CustomerName";
                sheet.Cell(1, 4).Value = "PaymentMethod";
                sheet.Cell(1, 5).Value = "OrderStatus";
                sheet.Cell(1, 6).Value = "TotalAmount";
                sheet.Cell(1, 7).Value = "Note";

                var row = 2;
                foreach (var order in orders)
                {
                    sheet.Cell(row, 1).Value = order.OrderNo;
                    sheet.Cell(row, 2).Value = order.DateTime;
                    sheet.Cell(row, 3).Value = order.CustomerName;
                    sheet.Cell(row, 4).Value = order.PaymentMethod.ToString();
                    sheet.Cell(row, 5).Value = order.OrderStatus.ToString();
                    sheet.Cell(row, 6).Value = order.TotalAmount;
                    sheet.Cell(row, 7).Value = order.Note ?? string.Empty;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? App.DataDir);
                workbook.SaveAs(outputPath);
                return outputPath;
            }
        }

        public string ExportJson(string outputPath, IReadOnlyList<OrderRecord> orders)
        {
            var payload = JsonConvert.SerializeObject(orders, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? App.DataDir);
            File.WriteAllText(outputPath, payload);
            return outputPath;
        }

        private static string BuildOrderNo()
        {
            return "W7-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
        }
    }
}

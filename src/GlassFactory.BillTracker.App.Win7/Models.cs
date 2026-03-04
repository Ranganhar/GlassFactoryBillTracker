using System;
using GlassFactory.BillTracker.Domain.Enums;

namespace GlassFactory.BillTracker.App.Win7
{
    public sealed class CustomerRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Note { get; set; }
    }

    public sealed class OrderRecord
    {
        public Guid Id { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public string Note { get; set; }
    }
}

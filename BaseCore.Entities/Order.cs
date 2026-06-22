using System;
using System.Collections.Generic;

namespace BaseCore.Entities
{
    public class Order
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public string Status { get; set; } = "WaitingDeposit";
        public string ShippingAddress { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string ShippingMethod { get; set; } = "Standard";
        public decimal PackagingFee { get; set; } = 0;
        public List<OrderDetail> OrderDetails { get; set; } = [];
    }
}
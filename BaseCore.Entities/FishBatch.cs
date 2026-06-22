using System;

namespace BaseCore.Entities
{
    public class FishBatch
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string OriginFarm { get; set; } = "";
        public DateTime ImportDate { get; set; }
        public string QuarantineStatus { get; set; } = "Pending"; // Pending | Passed | Failed
        public int InitialQuantity { get; set; }
        public int CurrentQuantity { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = "";
        public string CreatedByName { get; set; } = "";

        public virtual Product? Product { get; set; }
    }
}

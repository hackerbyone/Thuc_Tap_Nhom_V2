namespace BaseCore.Entities
{
    public class Product
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public decimal Price { get; set; }

        public decimal? PairPrice { get; set; }

        public int Stock { get; set; }

        public string? ImageUrl { get; set; }

        public string? Description { get; set; }

        public string? CareInstructions { get; set; }

        public string? Environment { get; set; }

        public decimal? TempMin { get; set; }
        public decimal? TempMax { get; set; }
        public decimal? PhMin { get; set; }
        public decimal? PhMax { get; set; }
        public string? Hardness { get; set; }
        public string? MaxSize { get; set; }
        public string? Diet { get; set; }
        public string? Compatibility { get; set; }

        public int MaleStock { get; set; }

        public int FemaleStock { get; set; }

        public int CategoryId { get; set; }

        public Category? Category { get; set; }
    }
}

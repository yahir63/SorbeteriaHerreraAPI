namespace HerreraSystem.Application.DTOs.InventoryMovementDtos
{
    public class InventoryMovementListItemDto
    {
        public int Id { get; set; }

        public int MovementTypeId { get; set; }

        public string MovementTypeName { get; set; } = null!;

        public DateTime? MovementDate { get; set; }

        public string? CreatedByUserName { get; set; }
        public List<string> ProductNames { get; set; } = new();
        public int TotalQuantity { get; set; }
        public List<string> SourceLocationNames { get; set; } = new();
        public List<string> DestinationLocationNames { get; set; } = new();
        public string? Notes { get; set; }
        public int? SaleId { get; set; }
        public int? OrderId { get; set; }
    }
}

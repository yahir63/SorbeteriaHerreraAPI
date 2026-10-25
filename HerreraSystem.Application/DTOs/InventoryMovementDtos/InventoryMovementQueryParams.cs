using HerreraSystem.Application.Common;

namespace HerreraSystem.Application.DTOs.InventoryMovementDtos
{
    public class InventoryMovementQueryParams : PaginationParams
    {
        public int? MovementTypeId { get; set; }
        public int? LocationId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Search { get; set; }
    }
}

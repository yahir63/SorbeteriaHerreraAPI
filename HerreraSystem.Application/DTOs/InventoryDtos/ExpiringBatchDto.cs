namespace HerreraSystem.Application.DTOs.InventoryDtos;

public class ExpiringBatchDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public int BatchId { get; set; }
    public string? BatchCode { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public int DaysToExpire { get; set; }
    public bool IsExpired { get; set; }
    public int TotalStock { get; set; }
    public List<ExpiringBatchLocationDto> StockByLocation { get; set; } = new();
}

public class ExpiringBatchLocationDto
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = null!;
    public int CurrentStock { get; set; }
}

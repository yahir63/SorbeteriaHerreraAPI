using System;
using System.Collections.Generic;
using System.Text;
using HerreraSystem.Application.Common;
using HerreraSystem.Application.DTOs.InventoryDtos;

namespace HerreraSystem.Application.Interfaces.Repositories
{
    public interface IInventoryRepository
    {
        Task<List<InventoryProductDto>> GetInventoryProductsAsync(
            string? search,
            int? lineId,
            int? flavorId,
            int? presentationId);

        Task<PagedResponse<InventoryProductDto>> GetAllAsync(
            string? search,
            int? lineId,
            int? flavorId,
            int? presentationId,
            PaginationParams paginationParams,
            bool? lowStock = null);

        Task<InventoryProductBatchesDto?> GetProductBatchesAsync(int productId);

        Task<InventoryBatchDetailDto?> GetBatchDetailAsync(int batchId);

        Task<PagedResponse<ExpiringBatchDto>> GetExpiringBatchesAsync(
            int days,
            string? search,
            int? lineId,
            int? presentationId,
            PaginationParams paginationParams);

        Task<InventoryStatsDto> GetStatsAsync(string period, int expiringDays = 15);

    }
}

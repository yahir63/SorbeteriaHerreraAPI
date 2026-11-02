using HerreraSystem.API.Configurations;
using HerreraSystem.API.Models;
using HerreraSystem.Application.DTOs.LogDtos;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HerreraSystem.API.Services;

public interface ILogService
{
    Task InsertLogAsync(SystemLog log);
    Task<LogPageResult> GetPagedAsync(LogQueryParams query);
    Task<LogDetailDto?> GetByIdAsync(string id);
}

public class MongoLogService : ILogService
{
    private readonly IMongoCollection<SystemLog> _collection;

    public MongoLogService(IOptions<MongoLogSettings> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<SystemLog>(settings.CollectionName);

        InicializarIndices(settings.RetentionDays);
    }

    private void InicializarIndices(int retentionDays)
    {
        // 1. Índice TTL (Time-To-Live) para purga automática
   var ttlKeys = Builders<SystemLog>.IndexKeys.Ascending(new StringFieldDefinition<SystemLog>("_ts"));     
   var ttlOptions = new CreateIndexOptions
        {
            ExpireAfter = TimeSpan.FromDays(retentionDays)
        };

        // 2. Índices de consulta frecuente para soporte técnico
        var traceKeys = Builders<SystemLog>.IndexKeys.Ascending(x => x.TraceId);
        var levelKeys = Builders<SystemLog>.IndexKeys.Ascending(x => x.Level);

        _collection.Indexes.CreateMany([
            new CreateIndexModel<SystemLog>(ttlKeys, ttlOptions),
            new CreateIndexModel<SystemLog>(traceKeys),
            new CreateIndexModel<SystemLog>(levelKeys)
        ]);
    }

    public async Task InsertLogAsync(SystemLog log)
    {
        try
        {
            await _collection.InsertOneAsync(log);
        }
        catch (Exception ex)
        {
        
            Console.WriteLine($"[CRITICAL] Fallo de persistencia en MongoDB: {ex.Message}");
        }
    }

    public async Task<LogPageResult> GetPagedAsync(LogQueryParams query)
    {
        var filters = Builders<SystemLog>.Filter;
        var conditions = new List<FilterDefinition<SystemLog>>();

        if (!string.IsNullOrWhiteSpace(query.Modulo))
            conditions.Add(filters.Regex(x => x.Request.Path,
                new BsonRegularExpression("^/api/" + System.Text.RegularExpressions.Regex.Escape(query.Modulo))));
        if (!string.IsNullOrWhiteSpace(query.Method))
            conditions.Add(filters.Eq(x => x.Request.Method, query.Method.ToUpperInvariant()));
        if (!string.IsNullOrWhiteSpace(query.UserId))
            conditions.Add(filters.Eq(x => x.UserId, query.UserId));
        if (query.Success.HasValue)
            conditions.Add(query.Success.Value
                ? filters.Lt(x => x.Response.StatusCode, 400)
                : filters.Gte(x => x.Response.StatusCode, 400));

        var (fromUtc, toUtcExclusive) = LogMapping.GetUtcDateBounds(query.FromDate, query.ToDate);
        if (fromUtc.HasValue) conditions.Add(filters.Gte(x => x.Timestamp, fromUtc.Value));
        if (toUtcExclusive.HasValue) conditions.Add(filters.Lt(x => x.Timestamp, toUtcExclusive.Value));

        var filter = conditions.Count == 0 ? filters.Empty : filters.And(conditions);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var total = await _collection.CountDocumentsAsync(filter);
        var logs = await _collection.Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new LogPageResult
        {
            TotalRecords = (int)Math.Min(total, int.MaxValue),
            Items = logs.Select(MapListItem).ToList()
        };
    }

    public async Task<LogDetailDto?> GetByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out _)) return null;
        var log = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (log is null) return null;

        var item = MapListItem(log);
        return new LogDetailDto
        {
            Id = item.Id,
            Timestamp = item.Timestamp,
            Level = item.Level,
            UserId = item.UserId,
            UserName = item.UserName,
            Rol = item.Rol,
            Method = item.Method,
            Path = item.Path,
            Modulo = item.Modulo,
            Tipo = item.Tipo,
            StatusCode = item.StatusCode,
            Success = item.Success,
            ElapsedMilliseconds = item.ElapsedMilliseconds,
            TraceId = log.TraceId ?? string.Empty,
            Environment = log.Environment ?? string.Empty,
            ClientIp = log.ClientIp ?? string.Empty,
            Device = log.Device ?? string.Empty,
            QueryString = log.Request?.QueryString ?? string.Empty,
            Payload = LogMapping.SanitizePayload(log.Request?.Payload),
            Status = log.Response?.Status ?? string.Empty,
            Exception = LogMapping.TruncateException(log.Exception?.Message)
        };
    }

    private static LogListItemDto MapListItem(SystemLog log) => LogMapping.CreateListItem(
        log.Id, log.Timestamp, log.Level, log.UserId, log.UserName, log.Rol,
        log.Request?.Method, log.Request?.Path, log.Response?.StatusCode ?? 0,
        log.Response?.ElapsedMilliseconds ?? 0);
}

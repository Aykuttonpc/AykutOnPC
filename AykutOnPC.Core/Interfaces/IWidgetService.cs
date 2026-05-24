using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IWidgetService
{
    Task<Widget?> CreateAsync(int boardId, int userId, CreateWidgetDto dto, CancellationToken ct = default);

    Task<bool> UpdateAsync(int widgetId, int userId, UpdateWidgetDto dto, CancellationToken ct = default);

    /// <summary>Batch update grid positions after a drag/resize. Returns count of rows changed.</summary>
    Task<int> UpdateLayoutAsync(int boardId, int userId, IReadOnlyList<WidgetLayoutDto> layout, CancellationToken ct = default);

    Task<bool> DeleteAsync(int widgetId, int userId, CancellationToken ct = default);

    // ─── Widget items (checklist / link entries) ─────────────────────

    Task<WidgetItem?> AddItemAsync(int widgetId, int userId, AddWidgetItemDto dto, CancellationToken ct = default);

    Task<bool> UpdateItemAsync(int widgetId, int itemId, int userId, UpdateWidgetItemDto dto, CancellationToken ct = default);

    Task<bool> DeleteItemAsync(int widgetId, int itemId, int userId, CancellationToken ct = default);

    Task<int> ReorderItemsAsync(int widgetId, int userId, IReadOnlyList<WidgetItemOrderDto> order, CancellationToken ct = default);

    // ─── Metric entries (chart widgets) ──────────────────────────────

    Task<MetricEntry?> AddMetricAsync(int widgetId, int userId, AddMetricEntryDto dto, CancellationToken ct = default);

    Task<List<MetricPointDto>> GetMetricsAsync(int widgetId, int userId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}

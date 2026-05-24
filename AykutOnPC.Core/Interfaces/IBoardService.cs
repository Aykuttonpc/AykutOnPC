using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IBoardService
{
    /// <summary>Lightweight list for the switcher (no widget payload).</summary>
    Task<List<BoardListItemDto>> ListAsync(int userId, bool includeArchived, CancellationToken ct = default);

    /// <summary>Full board with widgets + items, suitable for hydrating the dashboard.</summary>
    Task<BoardDetailDto?> GetDetailAsync(int boardId, int userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the user's default board id — first non-archived board ordered by SortOrder.
    /// If the user has no boards yet, creates one called "My Workspace" and returns its id.
    /// </summary>
    Task<int> EnsureDefaultBoardAsync(int userId, CancellationToken ct = default);

    Task<Board> CreateAsync(int userId, CreateBoardDto dto, CancellationToken ct = default);

    Task<bool> UpdateAsync(int boardId, int userId, UpdateBoardDto dto, CancellationToken ct = default);

    /// <summary>Soft archive (sets ArchivedAtUtc). Use for sprint close + temporary hide.</summary>
    Task<bool> ArchiveAsync(int boardId, int userId, CancellationToken ct = default);

    /// <summary>Hard delete — cascades to widgets/items/metrics. Use only for explicit "delete" intent.</summary>
    Task<bool> DeleteAsync(int boardId, int userId, CancellationToken ct = default);
}

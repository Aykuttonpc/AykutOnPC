using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;

namespace AykutOnPC.Core.Interfaces;

public interface IPbiService
{
    /// <summary>All Pbis + labels in one round-trip. Optional filter applied client-side too,
    /// but pre-filtering server-side keeps payload small.</summary>
    Task<WorkspaceSnapshotDto> GetSnapshotAsync(bool includeDone, CancellationToken ct = default);

    Task<Pbi?> CreateAsync(CreatePbiDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int pbiId, UpdatePbiDto dto, CancellationToken ct = default);

    /// <summary>Move/reorder by drag/drop. Triggers CompletedAtUtc stamp/clear on Done transitions.</summary>
    Task<bool> MoveAsync(int pbiId, MovePbiDto dto, CancellationToken ct = default);

    Task<bool> DeleteAsync(int pbiId, CancellationToken ct = default);
}

public interface ILabelService
{
    Task<List<LabelDto>> ListAsync(CancellationToken ct = default);
    Task<Label?> CreateAsync(CreateLabelDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int labelId, UpdateLabelDto dto, CancellationToken ct = default);

    /// <summary>Deletes the label and its junction rows. Pbis remain — they just lose the chip.</summary>
    Task<bool> DeleteAsync(int labelId, CancellationToken ct = default);
}

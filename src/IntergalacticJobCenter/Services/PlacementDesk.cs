using IntergalacticJobCenter.Functional;
using IntergalacticJobCenter.Lookup;
using IntergalacticJobCenter.Native;
using IntergalacticJobCenter.Persistence;

namespace IntergalacticJobCenter.Services;

public sealed record RecordedPlacement(LedgerEntry Entry, PlacementLedger Ledger);

public static class PlacementDesk
{
    public static Result<RecordedPlacement, AppError> PlaceAndRecord(
        ApplicantRegistry registry,
        PlacementRequest request,
        FeeAdapter fees,
        PlacementLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fees);
        ArgumentNullException.ThrowIfNull(ledger);

        return PlacementService.Place(registry, request)
            .Bind(receipt => fees.Calculate(receipt.Score, receipt.State.Application.Applicant.Tentacles)
                .Map(fee => LedgerEntry.From(receipt, fee)))
            .Bind(entry => ledger.Add(entry).Map(updated => new RecordedPlacement(entry, updated)));
    }
}

namespace IntergalacticJobCenter.Reports;

public sealed record PricedRow(ShortlistRow Row, int FeeCents);

public sealed record ReportBundle(
    System.Collections.Immutable.ImmutableArray<PricedRow> Shortlist,
    System.Collections.Immutable.ImmutableArray<RiskBandRow> Risk);

public sealed record ExportSummary(ReportBundle Bundle, int RowsExported, string Path);

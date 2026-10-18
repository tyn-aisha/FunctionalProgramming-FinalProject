namespace IntergalacticJobCenter.Workflow;

public abstract record ApplicationState
{
    public ValidatedAlienApplication Application { get; }

    private ApplicationState(ValidatedAlienApplication application)
    {
        Application = application;
    }

    public sealed record Pending : ApplicationState
    {
        internal Pending(ValidatedAlienApplication application) : base(application) { }
    }

    public sealed record Approved : ApplicationState
    {
        public string ApprovedBy { get; }

        internal Approved(ValidatedAlienApplication application, string approvedBy) : base(application)
        {
            ApprovedBy = approvedBy;
        }
    }

    public sealed record Rejected : ApplicationState
    {
        public string Reason { get; }

        internal Rejected(ValidatedAlienApplication application, string reason) : base(application)
        {
            Reason = reason;
        }
    }

    public sealed record Confirmed : ApplicationState
    {
        public string ApprovedBy { get; }
        public DateOnly StartDate { get; }

        internal Confirmed(ValidatedAlienApplication application, string approvedBy, DateOnly startDate)
            : base(application)
        {
            ApprovedBy = approvedBy;
            StartDate = startDate;
        }
    }
}

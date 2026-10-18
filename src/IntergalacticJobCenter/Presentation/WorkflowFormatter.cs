using System.Globalization;
using IntergalacticJobCenter.Workflow;

namespace IntergalacticJobCenter.Presentation;

public static class WorkflowFormatter
{
    public static string Describe(ApplicationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state switch
        {
            ApplicationState.Pending p =>
                $"Pending ({p.Application.Applicant.Name}, score {p.Application.Score})",
            ApplicationState.Approved a =>
                $"Approved by {a.ApprovedBy}",
            ApplicationState.Rejected r =>
                $"Rejected: {r.Reason}",
            ApplicationState.Confirmed c =>
                $"Confirmed, starts {c.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} (approved by {c.ApprovedBy})",
            _ => throw new InvalidOperationException($"Unhandled state {state.GetType().Name}.")
        };
    }

    public static string Describe(TransitionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            TransitionResult.Transitioned t =>
                $"OK      {t.Previous.GetType().Name} -> {t.Current.GetType().Name}: {Describe(t.Current)}",
            TransitionResult.AlreadyApplied a =>
                $"NO-OP   already applied, state stays: {Describe(a.Current)}",
            TransitionResult.Failed f =>
                $"FAILED  [{f.Error}] {f.Message} State unchanged: {Describe(f.Unchanged)}",
            _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}.")
        };
    }

    public static string Describe(ApplicationCreation creation)
    {
        ArgumentNullException.ThrowIfNull(creation);

        return creation switch
        {
            ApplicationCreation.Created c =>
                $"Created {c.Application.ApplicationId}: {c.Application.Applicant.Name}, score {c.Application.Score}, job {c.Application.Job}",
            ApplicationCreation.CreationRejected r =>
                "Rejected:" + string.Concat(r.Reasons.Select(reason => Environment.NewLine + "  - " + reason)),
            _ => throw new InvalidOperationException($"Unhandled creation {creation.GetType().Name}.")
        };
    }
}

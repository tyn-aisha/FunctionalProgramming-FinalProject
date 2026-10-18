using System.Text;
using IntergalacticJobCenter.Models;
using IntergalacticJobCenter.Policies;
using IntergalacticJobCenter.Workflow;

namespace IntergalacticJobCenter.Presentation;

public static class WorkflowDemo
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    public static string Render()
    {
        var sb = new StringBuilder();
        var policy = ScoringPolicies.OfficeSafety;

        sb.AppendLine("=== Factory: Blarg under Office Safety Policy ===");
        var blarg = Build("Blarg", "Blargonian", 10, 8, 55, 70);
        sb.AppendLine(WorkflowFormatter.Describe(ValidatedAlienApplication.Create("APP-002", blarg, policy)));
        sb.AppendLine();

        sb.AppendLine("=== Factory: Zorp under Office Safety Policy ===");
        var zorp = Build("Zorp", "Zorgian", 6, 2, 10, 90);
        var creation = ValidatedAlienApplication.Create("APP-001", zorp, policy);
        sb.AppendLine(WorkflowFormatter.Describe(creation));
        sb.AppendLine();

        if (creation is ApplicationCreation.Created created)
        {
            sb.AppendLine($"=== Workflow for {created.Application.ApplicationId} (today = 2026-10-18) ===");

            var commands = new WorkflowCommand[]
            {
                new WorkflowCommand.Confirm(Today.AddDays(7), Today),
                new WorkflowCommand.Approve("Officer Kim"),
                new WorkflowCommand.Approve("Officer Kim"),
                new WorkflowCommand.Approve("Officer Lee"),
                new WorkflowCommand.Confirm(Today.AddDays(-1), Today),
                new WorkflowCommand.Confirm(Today.AddDays(7), Today),
                new WorkflowCommand.Confirm(Today.AddDays(7), Today),
                new WorkflowCommand.Reject("Changed our mind")
            };

            var run = ApplicationWorkflow.Replay(ApplicationWorkflow.Start(created.Application), commands);

            for (var i = 0; i < commands.Length; i++)
            {
                sb.AppendLine($"{i + 1}. {commands[i]}");
                sb.AppendLine($"   {WorkflowFormatter.Describe(run.Steps[i])}");
            }

            sb.AppendLine();
            sb.AppendLine($"Final state: {WorkflowFormatter.Describe(run.Final)}");
        }

        return sb.ToString();
    }

    private static AlienApplicant? Build(
        string name, string race, int tentacles, int eyes, int aggression, int compatibility) =>
        AlienApplicant.Create(name, race, tentacles, eyes, aggression, compatibility) is ApplicantCreation.Valid valid
            ? valid.Applicant
            : null;
}

using IntergalacticJobCenter.Functional;

namespace IntergalacticJobCenter.Tests;

public class ResultTests
{

    [Fact]
    public void Result_SuccessCarriesValue_FailureCarriesError()
    {
        var success = Result<int, string>.Success(5);
        var failure = Result<int, string>.Failure("broken rule");

        Assert.Equal(5, Assert.IsType<Result<int, string>.Ok>(success).Value);
        Assert.Equal("broken rule", Assert.IsType<Result<int, string>.Err>(failure).Error);
        Assert.True(success.IsOk);
        Assert.False(failure.IsOk);
    }

    [Fact]
    public void Result_ContradictoryStatesCannotBeBuilt()
    {

        Assert.Throws<ArgumentNullException>(() => Result<string, string>.Success(null!));
        Assert.Throws<ArgumentNullException>(() => Result<int, string>.Failure(null!));

        Assert.IsNotType<Result<int, string>.Ok>(Result<int, string>.Failure("e"));
    }

    [Fact]
    public void AppError_RequiresSubjectAndMessage()
    {
        Assert.Throws<ArgumentException>(() => new AppError.Domain(ErrorCode.ApplicantNotFound, " ", "message"));
        Assert.Throws<ArgumentException>(() => new AppError.Operational(ErrorCode.FileNotFound, "file.csv", ""));
    }

    [Fact]
    public void Map_SuccessReceivesTheValueAndReturnsTheNewValue()
    {
        var received = new List<int>();

        var mapped = Result<int, string>.Success(3).Map(x =>
        {
            received.Add(x);
            return $"value-{x * 2}";
        });

        Assert.Equal(new[] { 3 }, received);
        Assert.Equal("value-6", Assert.IsType<Result<string, string>.Ok>(mapped).Value);
    }

    [Fact]
    public void Bind_SuccessReceivesTheValue_AndKeepsTheNextResult()
    {
        var received = new List<int>();

        var bound = Result<int, string>.Success(3).Bind(x =>
        {
            received.Add(x);
            return Result<string, string>.Success($"next-{x}");
        });

        Assert.Equal(new[] { 3 }, received);
        Assert.Equal("next-3", Assert.IsType<Result<string, string>.Ok>(bound).Value);
    }

    [Fact]
    public void Bind_PreservesAFailureReturnedByTheNextOperation()
    {
        var bound = Result<int, string>.Success(3)
            .Bind(_ => Result<string, string>.Failure("second step refused"));

        Assert.Equal("second step refused", Assert.IsType<Result<string, string>.Err>(bound).Error);
    }

    [Fact]
    public void FailurePropagation_MapAndBindDoNotInvokeTheirFunctions()
    {
        var mapCalls = 0;
        var bindCalls = 0;
        var failure = Result<int, string>.Failure("first step failed");

        var mapped = failure.Map(x =>
        {
            mapCalls++;
            return x + 1;
        });
        var bound = failure.Bind(x =>
        {
            bindCalls++;
            return Result<int, string>.Success(x + 1);
        });

        Assert.Equal(0, mapCalls);
        Assert.Equal(0, bindCalls);
        Assert.Equal("first step failed", Assert.IsType<Result<int, string>.Err>(mapped).Error);
        Assert.Equal("first step failed", Assert.IsType<Result<int, string>.Err>(bound).Error);
    }

    [Fact]
    public void FailurePropagation_ALongChainStopsAtTheFirstFailure()
    {
        var executed = new List<string>();

        var result = Result<int, string>.Success(1)
            .Map(x => { executed.Add("map-1"); return x + 1; })
            .Bind(x => { executed.Add("bind-fails"); return Result<int, string>.Failure("stopped here"); })
            .Map(x => { executed.Add("map-2"); return x + 1; })
            .Bind(x => { executed.Add("bind-2"); return Result<int, string>.Success(x + 1); });

        Assert.Equal(new[] { "map-1", "bind-fails" }, executed);
        Assert.Equal("stopped here", Assert.IsType<Result<int, string>.Err>(result).Error);
    }

    [Fact]
    public void Match_HandlesExactlyTheEstablishedCase()
    {
        var okText = Result<int, string>.Success(4).Match(v => $"ok {v}", e => $"err {e}");
        var errText = Result<int, string>.Failure("bad").Match(v => $"ok {v}", e => $"err {e}");

        Assert.Equal("ok 4", okText);
        Assert.Equal("err bad", errText);
    }
}

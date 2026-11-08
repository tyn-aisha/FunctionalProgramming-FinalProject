using IntergalacticJobCenter.Functional;

namespace IntergalacticJobCenter.Native;

public sealed class FeeAdapter
{
    public const int MaxScore = 100;
    public const int MaxTentacles = 64;

    private readonly Func<int, int, int> _nativeCall;

    public FeeAdapter(Func<int, int, int> nativeCall)
    {
        ArgumentNullException.ThrowIfNull(nativeCall);
        _nativeCall = nativeCall;
    }

    public static FeeAdapter Native { get; } = new(NativeMethods.PlacementFeeCents);

    public Result<int, AppError> Calculate(int score, int tentacles)
    {
        if (score < 0 || score > MaxScore)
        {
            return Outcomes.Domain<int>(ErrorCode.InvalidInput, "score",
                $"Score {score} is outside the allowed range 0-{MaxScore}.");
        }

        if (tentacles < 0 || tentacles > MaxTentacles)
        {
            return Outcomes.Domain<int>(ErrorCode.InvalidInput, "tentacles",
                $"Tentacles {tentacles} is outside the allowed range 0-{MaxTentacles}.");
        }

        try
        {
            var fee = _nativeCall(score, tentacles);

            return fee >= 0
                ? Outcomes.Ok(fee)
                : Outcomes.Operational<int>(ErrorCode.NativeCallFailed, "ijc_placement_fee_cents",
                    $"The native function rejected validated input (score {score}, tentacles {tentacles}) and returned {fee}.");
        }
        catch (DllNotFoundException ex)
        {
            return Outcomes.Operational<int>(ErrorCode.NativeLibraryUnavailable, "ijc_native",
                $"The native library 'ijc_native' could not be loaded ({ex.Message}). Build it with native/build.sh or native/build.cmd.");
        }
        catch (BadImageFormatException ex)
        {
            return Outcomes.Operational<int>(ErrorCode.NativeLibraryUnavailable, "ijc_native",
                $"The native library 'ijc_native' has the wrong format or architecture ({ex.Message}).");
        }
        catch (EntryPointNotFoundException ex)
        {
            return Outcomes.Operational<int>(ErrorCode.NativeCallFailed, "ijc_placement_fee_cents",
                $"The export 'ijc_placement_fee_cents' was not found in the native library ({ex.Message}).");
        }
    }
}

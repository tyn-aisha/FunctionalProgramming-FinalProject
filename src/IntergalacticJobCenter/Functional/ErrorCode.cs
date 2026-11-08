namespace IntergalacticJobCenter.Functional;

public enum ErrorCode
{

    ApplicantNotFound,
    PolicyNotFound,
    ApplicationRejected,
    TransitionRejected,
    InvalidInput,
    DuplicateApplication,
    ApplicantAlreadyPlaced,

    FileNotFound,
    FileUnreadable,
    MalformedContent,
    InvalidPath,
    UnsupportedFormat,
    FileWriteFailed,
    NativeLibraryUnavailable,
    NativeCallFailed
}

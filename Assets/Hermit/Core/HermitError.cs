using System;

namespace Hermit.Core
{
    public enum HermitErrorKind
    {
        Network,
        InvalidCredentials,
        SessionExpired,
        Backend,
        Parsing,
        Unknown
    }

    /// <summary>
    /// Carries both a message safe to show a player (UserMessage) and one that
    /// is not (TechnicalDetail, for HermitLog only). Networking implementations
    /// throw HermitException instead of letting raw UnityWebRequest/parsing
    /// exceptions escape — callers always end up with one of these, never a
    /// stack trace.
    /// </summary>
    public readonly struct HermitError
    {
        public HermitErrorKind Kind { get; }
        public string UserMessage { get; }
        public string TechnicalDetail { get; }

        public HermitError(HermitErrorKind kind, string userMessage, string technicalDetail)
        {
            Kind = kind;
            UserMessage = userMessage;
            TechnicalDetail = technicalDetail;
        }
    }

    public sealed class HermitException : Exception
    {
        public HermitError Error { get; }

        public HermitException(HermitError error) : base(error.TechnicalDetail)
        {
            Error = error;
        }
    }

    /// <summary>Minimal result wrapper — not a framework, just enough to avoid
    /// unhandled exceptions reaching UI/debug code.</summary>
    public readonly struct HermitResult<T>
    {
        public bool Success { get; }
        public T Value { get; }
        public HermitError Error { get; }

        private HermitResult(bool success, T value, HermitError error)
        {
            Success = success;
            Value = value;
            Error = error;
        }

        public static HermitResult<T> Ok(T value) => new HermitResult<T>(true, value, default);
        public static HermitResult<T> Fail(HermitError error) => new HermitResult<T>(false, default, error);
    }
}

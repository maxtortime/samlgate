namespace Samlgate;

/// <summary>An expected failure with a message meant for the user (printed without a stack trace).</summary>
public sealed class SamlgateException : Exception
{
    public SamlgateException(string message) : base(message)
    {
    }

    public SamlgateException(string message, Exception inner) : base(message, inner)
    {
    }
}

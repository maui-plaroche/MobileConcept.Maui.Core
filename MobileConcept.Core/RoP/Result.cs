namespace MobileConcept.Core.RoP;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error message.
/// Implements the Railway-Oriented Programming (RoP) pattern for functional error handling.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
/// <param name="IsSuccess">Indicates whether the operation succeeded.</param>
/// <param name="Value">The value returned on success, or default if the operation failed.</param>
/// <param name="Error">The error message if the operation failed, or null if it succeeded.</param>
public record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The value to wrap in a successful result.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the value.</returns>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing why the operation failed.</param>
    /// <returns>A failed <see cref="Result{T}"/> containing the error message.</returns>
    public static Result<T> Failure(string error) => new(false, default, error);
}
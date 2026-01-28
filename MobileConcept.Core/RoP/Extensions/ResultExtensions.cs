using System;

namespace MobileConcept.Core.RoP.Extensions;

/// <summary>
/// Extension methods for <see cref="Result{T}"/> enabling Railway-Oriented Programming (RoP) composition.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Chains a function that returns a <see cref="Result{U}"/> to the current result.
    /// If the current result is successful, applies the function to its value.
    /// If the current result is a failure, propagates the error without calling the function.
    /// </summary>
    /// <typeparam name="T">The type of the input result value.</typeparam>
    /// <typeparam name="U">The type of the output result value.</typeparam>
    /// <param name="result">The current result to bind.</param>
    /// <param name="func">The function to apply if the result is successful.</param>
    /// <returns>The result of applying the function, or a failure with the original error.</returns>
    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> func)
        => result.IsSuccess ? func(result.Value!) : Result<U>.Failure(result.Error!);

    /// <summary>
    /// Transforms the value inside a successful result using the provided function.
    /// If the current result is successful, applies the function and wraps the output in a new successful result.
    /// If the current result is a failure, propagates the error without calling the function.
    /// </summary>
    /// <typeparam name="T">The type of the input result value.</typeparam>
    /// <typeparam name="U">The type of the transformed value.</typeparam>
    /// <param name="result">The current result to map.</param>
    /// <param name="func">The transformation function to apply if the result is successful.</param>
    /// <returns>A new result containing the transformed value, or a failure with the original error.</returns>
    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> func)
        => result.IsSuccess ? Result<U>.Success(func(result.Value!)) : Result<U>.Failure(result.Error!);
}
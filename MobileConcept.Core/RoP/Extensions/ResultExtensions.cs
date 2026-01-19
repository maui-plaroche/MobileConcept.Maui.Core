using System;

namespace MobileConcept.Core.RoP.Extensions;

public static class ResultExtensions
{
    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> func)
        => result.IsSuccess ? func(result.Value!) : Result<U>.Failure(result.Error!);
    
    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> func)
        => result.IsSuccess ? Result<U>.Success(func(result.Value!)) : Result<U>.Failure(result.Error!);
}
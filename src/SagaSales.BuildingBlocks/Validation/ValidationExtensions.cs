namespace SagaSales.BuildingBlocks.Validation;

using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

public static class ValidationExtensions
{
    public static IServiceCollection AddSagaSalesValidation(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies.Length > 0)
        {
            services.AddValidatorsFromAssemblies(assemblies);
        }

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}

/// <summary>
/// Runs every registered FluentValidation validator before the handler and converts
/// failures into the domain <see cref="SagaSales.Shared.Exceptions.ValidationException"/>.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validatorList = validators as IList<IValidator<TRequest>> ?? validators.ToList();
        if (validatorList.Count == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

        throw new SagaSales.Shared.Exceptions.ValidationException("Validation failed", errors);
    }
}

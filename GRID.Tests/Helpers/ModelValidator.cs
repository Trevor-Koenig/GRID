using System.ComponentModel.DataAnnotations;

namespace GRID.Tests.Helpers;

public static class ModelValidator
{
    /// <summary>
    /// Runs DataAnnotation validation on a model and returns the results.
    /// </summary>
    public static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    /// <summary>
    /// Returns true if validation passes with no errors.
    /// </summary>
    public static bool IsValid(object model) => Validate(model).Count == 0;

    /// <summary>
    /// Returns the error messages for a specific member name.
    /// </summary>
    public static IEnumerable<string> ErrorsFor(object model, string memberName) =>
        Validate(model)
            .Where(r => r.MemberNames.Contains(memberName))
            .Select(r => r.ErrorMessage ?? string.Empty);
}

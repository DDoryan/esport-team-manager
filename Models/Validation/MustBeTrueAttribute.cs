using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Web.Models.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is true;
    }
}
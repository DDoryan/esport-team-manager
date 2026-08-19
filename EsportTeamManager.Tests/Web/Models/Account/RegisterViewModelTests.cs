using EsportTeamManager.Web.Models.Account;
using System.ComponentModel.DataAnnotations;

namespace EsportTeamManager.Tests.Web.Models.Account;

public sealed class RegisterViewModelTests
{
    [Fact]
    public void Validate_WhenModelIsValid_ReturnsNoErrors()
    {
        RegisterViewModel model = new()
        {
            Email = "player@example.test",
            Pseudo = "Player",
            Tag = "A01",
            Password = "Test123!",
            ConfirmPassword = "Test123!",
            MinimumAgeConfirmed = true,
            TermsAccepted = true
        };

        List<ValidationResult> validationResults = [];

        bool isValid = Validator.TryValidateObject(model, new ValidationContext(model), validationResults, true);

        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void Validate_WhenModelIsInvalid_ReturnsErrorsForEveryInvalidField()
    {
        RegisterViewModel model = new()
        {
            Email = "invalid-email",
            Pseudo = "ab",
            Tag = "A-",
            Password = "short",
            ConfirmPassword = "different",
            MinimumAgeConfirmed = false,
            TermsAccepted = false
        };

        List<ValidationResult> validationResults = [];

        bool isValid = Validator.TryValidateObject(model, new ValidationContext(model), validationResults, true);

        Assert.False(isValid);
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(RegisterViewModel.Email)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(RegisterViewModel.Pseudo)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(RegisterViewModel.Tag)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(RegisterViewModel.Password)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(RegisterViewModel.ConfirmPassword)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(RegisterViewModel.MinimumAgeConfirmed)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(RegisterViewModel.TermsAccepted)));
    }
}
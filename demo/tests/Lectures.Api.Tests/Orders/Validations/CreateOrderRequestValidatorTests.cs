using FluentValidation.TestHelper;
using Lectures.Api.Orders.Requests;
using Lectures.Api.Orders.Validations;

namespace Lectures.Api.Tests.Orders.Validations;

public class CreateOrderRequestValidatorTests
{
    private static readonly CreateOrderRequestValidator Validator = new();

    // --- CustomerName ---

    [Fact]
    public void CustomerName_Empty_HasError()
    {
        var request = ValidRequest() with { CustomerName = "" };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerName);
    }

    [Fact]
    public void CustomerName_TooLong_HasError()
    {
        var request = ValidRequest() with { CustomerName = new string('A', 101) };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerName);
    }

    [Fact]
    public void CustomerName_AtMaxLength_NoError()
    {
        var request = ValidRequest() with { CustomerName = new string('A', 100) };
        var result = Validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.CustomerName);
    }

    [Fact]
    public void CustomerName_Valid_NoError()
    {
        var request = ValidRequest();
        var result = Validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.CustomerName);
    }

    // --- CustomerEmail ---

    [Fact]
    public void CustomerEmail_Empty_HasError()
    {
        var request = ValidRequest() with { CustomerEmail = "" };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerEmail);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@no-local.com")]
    public void CustomerEmail_InvalidFormat_HasError(string email)
    {
        var request = ValidRequest() with { CustomerEmail = email };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerEmail);
    }

    [Fact]
    public void CustomerEmail_Valid_NoError()
    {
        var request = ValidRequest();
        var result = Validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.CustomerEmail);
    }

    // --- ProductName ---

    [Fact]
    public void ProductName_Empty_HasError()
    {
        var request = ValidRequest() with { ProductName = "" };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ProductName);
    }

    [Fact]
    public void ProductName_TooLong_HasError()
    {
        var request = ValidRequest() with { ProductName = new string('P', 201) };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ProductName);
    }

    [Fact]
    public void ProductName_AtMaxLength_NoError()
    {
        var request = ValidRequest() with { ProductName = new string('P', 200) };
        var result = Validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.ProductName);
    }

    [Fact]
    public void ProductName_Valid_NoError()
    {
        var request = ValidRequest();
        var result = Validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.ProductName);
    }

    // --- Quantity ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_ZeroOrNegative_HasError(int quantity)
    {
        var request = ValidRequest() with { Quantity = quantity };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Quantity_ExceedsMax_HasError()
    {
        var request = ValidRequest() with { Quantity = 1001 };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Quantity_WithinRange_NoError(int quantity)
    {
        var request = ValidRequest() with { Quantity = quantity };
        var result = Validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
    }

    // --- UnitPrice ---

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void UnitPrice_ZeroOrNegative_HasError(decimal price)
    {
        var request = ValidRequest() with { UnitPrice = price };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Fact]
    public void UnitPrice_ExceedsMax_HasError()
    {
        var request = ValidRequest() with { UnitPrice = 100_000m };
        var result = Validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Fact]
    public void UnitPrice_AtMaxValue_NoError()
    {
        var request = ValidRequest() with { UnitPrice = 99_999.99m };
        var result = Validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Fact]
    public void UnitPrice_Valid_NoError()
    {
        var request = ValidRequest();
        var result = Validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.UnitPrice);
    }

    // --- Full valid request ---

    [Fact]
    public void ValidRequest_PassesAllRules()
    {
        var result = Validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // --- Helper ---

    private static CreateOrderRequest ValidRequest() =>
        new("John Doe", "john@example.com", "Widget", 5, 10m);
}

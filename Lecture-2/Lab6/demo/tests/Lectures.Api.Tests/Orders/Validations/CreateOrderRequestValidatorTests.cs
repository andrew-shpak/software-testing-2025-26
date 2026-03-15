using FluentValidation.TestHelper;
using Lectures.Api.Orders.Requests;
using Lectures.Api.Orders.Validations;

namespace Lectures.Api.Tests.Orders.Validations;

public class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _validator = new();

    // --- CustomerName ---

    [Fact]
    public void CustomerName_Empty_HasError()
    {
        var request = ValidRequest() with { CustomerName = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerName);
    }

    [Fact]
    public void CustomerName_TooLong_HasError()
    {
        var request = ValidRequest() with { CustomerName = new string('A', 101) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerName);
    }

    [Fact]
    public void CustomerName_Valid_NoError()
    {
        var request = ValidRequest();
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.CustomerName);
    }

    // --- CustomerEmail ---

    [Fact]
    public void CustomerEmail_Empty_HasError()
    {
        var request = ValidRequest() with { CustomerEmail = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerEmail);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@no-local.com")]
    public void CustomerEmail_InvalidFormat_HasError(string email)
    {
        var request = ValidRequest() with { CustomerEmail = email };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerEmail);
    }

    [Fact]
    public void CustomerEmail_Valid_NoError()
    {
        var request = ValidRequest();
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.CustomerEmail);
    }

    // --- ProductName ---

    [Fact]
    public void ProductName_Empty_HasError()
    {
        var request = ValidRequest() with { ProductName = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ProductName);
    }

    [Fact]
    public void ProductName_TooLong_HasError()
    {
        var request = ValidRequest() with { ProductName = new string('P', 201) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ProductName);
    }

    // --- Quantity ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_ZeroOrNegative_HasError(int quantity)
    {
        var request = ValidRequest() with { Quantity = quantity };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Quantity_ExceedsMax_HasError()
    {
        var request = ValidRequest() with { Quantity = 1001 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Quantity_WithinRange_NoError(int quantity)
    {
        var request = ValidRequest() with { Quantity = quantity };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
    }

    // --- UnitPrice ---

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void UnitPrice_ZeroOrNegative_HasError(decimal price)
    {
        var request = ValidRequest() with { UnitPrice = price };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Fact]
    public void UnitPrice_ExceedsMax_HasError()
    {
        var request = ValidRequest() with { UnitPrice = 100_000m };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Fact]
    public void UnitPrice_Valid_NoError()
    {
        var request = ValidRequest();
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.UnitPrice);
    }

    // --- Full valid request ---

    [Fact]
    public void ValidRequest_PassesAllRules()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // --- Helper ---

    private static CreateOrderRequest ValidRequest() =>
        new("John Doe", "john@example.com", "Widget", 5, 10m);
}

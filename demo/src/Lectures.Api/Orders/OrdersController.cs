using FluentValidation;
using Lectures.Api.Orders.Requests;
using Lectures.Api.Orders.Responses;
using Lectures.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Lectures.Api.Orders;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(
    IOrderService orderService,
    IValidator<CreateOrderRequest> validator) : ControllerBase
{
    [HttpGet]
    public async Task<Ok<IReadOnlyList<OrderResponse>>> GetAll()
    {
        var orders = await orderService.GetAllAsync();
        return TypedResults.Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<Results<Ok<OrderResponse>, NotFound>> GetById(int id)
    {
        try
        {
            var order = await orderService.GetByIdAsync(id);
            return TypedResults.Ok(order);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    [HttpPost]
    public async Task<Results<Created<OrderResponse>, ValidationProblem>> Create(
        [FromBody] CreateOrderRequest request)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        var order = await orderService.CreateAsync(request);
        return TypedResults.Created($"/api/orders/{order.Id}", order);
    }

    [HttpPut("{id:int}/status")]
    public async Task<Results<Ok<OrderResponse>, NotFound, Conflict<string>>> UpdateStatus(
        int id,
        [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            var order = await orderService.UpdateStatusAsync(id, request);
            return TypedResults.Ok(order);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<Results<NoContent, NotFound, Conflict<string>>> Delete(int id)
    {
        try
        {
            var deleted = await orderService.DeleteAsync(id);
            return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(ex.Message);
        }
    }
}

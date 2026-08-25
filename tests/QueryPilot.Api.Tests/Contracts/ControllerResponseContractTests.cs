using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using QueryPilot.Api.Features.Ai;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Customers;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Products;
using QueryPilot.Api.Features.Returns;
using Xunit;

namespace QueryPilot.Api.Tests.Contracts;

public sealed class ControllerResponseContractTests
{
    private static readonly Type[] ControllerTypes =
    [
        typeof(AiController),
        typeof(AnalyticsController),
        typeof(CategoriesController),
        typeof(CustomersController),
        typeof(OrdersController),
        typeof(ProductsController),
        typeof(ReturnsController)
    ];

    private static readonly HashSet<Type> EntityTypes =
    [
        typeof(Category),
        typeof(Customer),
        typeof(Order),
        typeof(OrderItem),
        typeof(Product),
        typeof(Return)
    ];

    [Fact]
    public void Controller_actions_never_expose_entity_types()
    {
        var exposedEntities = ControllerTypes
            .SelectMany(controller => controller.GetMethods(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes(inherit: true)
                .Any(attribute => attribute is HttpMethodAttribute))
            .Select(method => new
            {
                Method = $"{method.DeclaringType!.Name}.{method.Name}",
                Entity = FindEntityType(method.ReturnType)
            })
            .Where(result => result.Entity is not null)
            .ToArray();

        Assert.Empty(exposedEntities);
    }

    private static Type? FindEntityType(Type type)
    {
        if (EntityTypes.Contains(type))
        {
            return type;
        }

        if (type.IsArray)
        {
            return FindEntityType(type.GetElementType()!);
        }

        if (!type.IsGenericType)
        {
            return null;
        }

        return type.GetGenericArguments()
            .Select(FindEntityType)
            .FirstOrDefault(entity => entity is not null);
    }
}

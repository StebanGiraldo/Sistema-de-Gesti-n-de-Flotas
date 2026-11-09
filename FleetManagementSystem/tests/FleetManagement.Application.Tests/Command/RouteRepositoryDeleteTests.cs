using FleetManagement.Domain.Entities;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>Eliminar una ruta del repositorio en memoria: lo que usa «Deshacer» para quitar la ruta que creó una orden.</summary>
public class RouteRepositoryDeleteTests
{
    private readonly InMemoryDeliveryRouteRepository _routes = new();

    [Fact]
    public async Task Delete_RemovesThatRoute_AndOnlyThatRoute()
    {
        var gone = await _routes.AddAsync(new DeliveryRoute { Name = "Se va" });
        var stays = await _routes.AddAsync(new DeliveryRoute { Name = "Se queda" });

        await _routes.DeleteAsync(gone.Id);

        Assert.Null(await _routes.GetByIdAsync(gone.Id));
        Assert.Equal(stays.Id, Assert.Single(await _routes.GetAllAsync()).Id);
    }

    [Fact]
    public async Task Delete_OfARouteThatDoesNotExist_DoesNothing()
    {
        var route = await _routes.AddAsync(new DeliveryRoute { Name = "Ruta" });

        await _routes.DeleteAsync(Guid.NewGuid());

        Assert.Single(await _routes.GetAllAsync());
        Assert.NotNull(await _routes.GetByIdAsync(route.Id));
    }

    [Fact]
    public async Task Delete_Twice_IsHarmless()
    {
        var route = await _routes.AddAsync(new DeliveryRoute { Name = "Ruta" });

        await _routes.DeleteAsync(route.Id);
        await _routes.DeleteAsync(route.Id);

        Assert.Empty(await _routes.GetAllAsync());
    }

    [Fact]
    public async Task ARouteDeleted_CanBeAddedAgain_WithTheSameData()
    {
        var route = await _routes.AddAsync(new DeliveryRoute { Name = "Ruta" });
        await _routes.DeleteAsync(route.Id);

        await _routes.AddAsync(route);

        Assert.Equal("Ruta", (await _routes.GetByIdAsync(route.Id))!.Name);
    }
}

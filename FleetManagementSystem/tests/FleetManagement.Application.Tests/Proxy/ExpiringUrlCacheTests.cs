using FleetManagement.Application.Tests.Support;
using FleetManagement.Infrastructure.Navigation;
using Xunit;

namespace FleetManagement.Application.Tests.Proxy;

/// <summary>
/// Pruebas del almacén con vencimiento y tamaño máximo (<see cref="ExpiringUrlCache"/>) que usa el proxy: guardar y leer,
/// vencimiento exacto con un reloj manual, límite de entradas y seguridad entre hilos. Las claves se arman a mano
/// (<see cref="NavigationCacheKey"/> es un valor) para no depender de las reglas de redondeo, que se prueban aparte.
/// </summary>
public class ExpiringUrlCacheTests
{
    private static NavigationCacheKey Key(int number) => new($"clave-{number}");

    private static (ExpiringUrlCache Cache, ManualTimeProvider Time) Create(double timeToLiveMinutes = 10, int maxEntries = 500)
    {
        var time = new ManualTimeProvider();
        return (new ExpiringUrlCache(new NavigationCacheOptions { TimeToLiveMinutes = timeToLiveMinutes, MaxEntries = maxEntries }, time), time);
    }

    [Fact]
    public void TryGet_ForAKeyThatWasNeverStored_ReturnsFalse()
    {
        var (cache, _) = Create();

        Assert.False(cache.TryGet(Key(1), out var url));
        Assert.Equal(string.Empty, url);
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsTheStoredUrl_ForThatKeyOnly()
    {
        var (cache, _) = Create();

        cache.Set(Key(1), "url-1");
        cache.Set(Key(2), "url-2");

        Assert.True(cache.TryGet(Key(1), out var first));
        Assert.True(cache.TryGet(Key(2), out var second));
        Assert.False(cache.TryGet(Key(3), out _));
        Assert.Equal("url-1", first);
        Assert.Equal("url-2", second);
    }

    [Fact]
    public void Set_OnAnExistingKey_ReplacesTheUrl_AndRenewsTheLifetime()
    {
        var (cache, time) = Create(timeToLiveMinutes: 10);
        cache.Set(Key(1), "vieja");
        time.Advance(TimeSpan.FromMinutes(8));

        cache.Set(Key(1), "nueva");
        time.Advance(TimeSpan.FromMinutes(8)); // 16 min desde la primera, 8 desde la renovación

        Assert.True(cache.TryGet(Key(1), out var url));
        Assert.Equal("nueva", url);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void AnEntry_IsNotReturnedOnceItsLifetimeHasPassed_AndTheReadRemovesIt()
    {
        var (cache, time) = Create(timeToLiveMinutes: 5);
        cache.Set(Key(1), "url");
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.False(cache.TryGet(Key(1), out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void WithRoomForOnlyOneEntry_TheSecondKeyReplacesTheFirst()
    {
        var (cache, time) = Create(maxEntries: 1);
        cache.Set(Key(1), "url-1");
        time.Advance(TimeSpan.FromSeconds(1));

        cache.Set(Key(2), "url-2");

        Assert.False(cache.TryGet(Key(1), out _));
        Assert.True(cache.TryGet(Key(2), out _));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void RenewingAnExistingKeyOnAFullCache_DoesNotDiscardAnyOtherEntry()
    {
        var (cache, _) = Create(maxEntries: 2);
        cache.Set(Key(1), "url-1");
        cache.Set(Key(2), "url-2");

        cache.Set(Key(2), "url-2-nueva");

        Assert.True(cache.TryGet(Key(1), out _));
        Assert.True(cache.TryGet(Key(2), out var url));
        Assert.Equal("url-2-nueva", url);
    }

    [Fact]
    public void ConcurrentWritesOfManyDifferentKeys_NeverExceedTheMaximumSize()
    {
        const int maxEntries = 50;
        var (cache, _) = Create(maxEntries: maxEntries);

        Parallel.For(0, 5_000, index => cache.Set(Key(index), $"url-{index}"));

        Assert.Equal(maxEntries, cache.Count); // todas las claves están vigentes, así que se llena exactamente
    }

    [Fact]
    public void ConcurrentReadsAndWritesOfTheSameKeys_NeverReturnTheUrlOfAnotherKey()
    {
        var (cache, _) = Create();
        var wrong = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 10_000, index =>
        {
            var number = index % 25;
            cache.Set(Key(number), $"url-{number}");
            if (cache.TryGet(Key(number), out var url) && url != $"url-{number}") wrong.Add(url);
        });

        Assert.Empty(wrong);
        Assert.Equal(25, cache.Count);
    }
}

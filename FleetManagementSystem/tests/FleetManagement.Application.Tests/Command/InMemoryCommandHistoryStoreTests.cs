using FleetManagement.Application.Commands;
using FleetManagement.Infrastructure.Commands;
using Xunit;

namespace FleetManagement.Application.Tests.Command;

/// <summary>El almacén de historiales (Infrastructure): uno por usuario, creado cuando se pide.</summary>
public class InMemoryCommandHistoryStoreTests
{
    [Fact]
    public void ForTheSameUser_ReturnsTheSameHistory()
    {
        var store = new InMemoryCommandHistoryStore();

        Assert.Same(store.For("ana"), store.For("ana"));
    }

    [Fact]
    public void ForDifferentUsers_ReturnsDifferentHistories()
    {
        var store = new InMemoryCommandHistoryStore();

        Assert.NotSame(store.For("ana"), store.For("luis"));
    }

    [Theory]
    [InlineData("ANA")]
    [InlineData("  ana ")]
    public void TheUserKey_IgnoresCaseAndSurroundingSpaces(string key)
    {
        var store = new InMemoryCommandHistoryStore();

        Assert.Same(store.For("ana"), store.For(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankUser_IsRejected(string key)
        => Assert.Throws<ArgumentException>(() => new InMemoryCommandHistoryStore().For(key));

    [Fact]
    public void TheCapacity_GivenToTheStore_IsAppliedToEveryHistory()
    {
        var store = new InMemoryCommandHistoryStore(capacity: 2);
        var history = store.For("ana");

        foreach (var name in new[] { "A", "B", "C" })
            history.Record(new ScriptedCommand(name), DateTime.UtcNow);

        Assert.Equal(2, history.Undoable.Count);
    }
}

using MongoDB.Bson;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Xunit;

namespace Trading.Infrastructure.Tests;

public class MongoDbConfigrationTests
{
    [Fact]
    public void Entity_Properties_Should_Be_Serialized_As_SnakeCase()
    {
        // Arrange
        MongoDbConfigration.Configure();
        var user = new Strategy
        {
            UpdatedAt = DateTime.Now,
            HasOpenOrder = false,
            StrategyType = StrategyType.BottomBuy
        };

        // Act
        var bsonDoc = user.ToBsonDocument();

        // Assert
        Assert.Equal("updated_at", bsonDoc.GetElement("updated_at").Name);
        Assert.Equal("has_open_order", bsonDoc.GetElement("has_open_order").Name);
        Assert.Equal("strategy_type", bsonDoc.GetElement("strategy_type").Name);
    }
}

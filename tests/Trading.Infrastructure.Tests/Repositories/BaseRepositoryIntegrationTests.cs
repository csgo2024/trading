using MongoDB.Bson;
using MongoDB.Driver;
using Trading.Common.Models;
using Trading.Infrastructure.Repositories;
using Xunit;

namespace Trading.Infrastructure.Tests.Repositories;

public class BaseRepositoryIntegrationTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly BaseRepository<TestEntity> _repository;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    public BaseRepositoryIntegrationTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _domainEventDispatcher = fixture.DomainEventDispatcher;
        _repository = new BaseRepository<TestEntity>(_fixture.MongoContext!, _domainEventDispatcher);
    }

    [Fact]
    public async Task AddAsync_ShouldGenerateId_WhenEntityHasNoId()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test Entity" };

        // Act
        var result = await _repository.AddAsync(entity);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.Equal(entity.Name, result.Name);

        // Verify the entity was actually saved
        var savedEntity = await _repository.GetByIdAsync(result.Id);
        Assert.NotNull(savedEntity);
        Assert.Equal(result.Id, savedEntity.Id);
        Assert.Equal(entity.Name, savedEntity.Name);
    }

    [Fact]
    public async Task AddAsync_ShouldPreserveExistingId_WhenEntityHasId()
    {
        // Arrange
        var existingId = "507f1f77bcf86cd799439011";
        var entity = new TestEntity
        {
            Id = existingId,
            Name = "Test Entity"
        };

        // Act
        var result = await _repository.AddAsync(entity);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingId, result.Id);
        Assert.Equal(entity.Name, result.Name);

        // Verify the entity was actually saved
        var savedEntity = await _repository.GetByIdAsync(existingId);
        Assert.NotNull(savedEntity);
        Assert.Equal(existingId, savedEntity.Id);
        Assert.Equal(entity.Name, savedEntity.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenEntityDoesNotExist()
    {
        // Arrange
        var nonExistentId = "507f1f77bcf86cd799439011";

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateExistingEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "Original Name" };
        var savedEntity = await _repository.AddAsync(entity);

        savedEntity.Name = "Updated Name";

        // Act
        var updateResult = await _repository.UpdateAsync(savedEntity.Id, savedEntity);
        var updatedEntity = await _repository.GetByIdAsync(savedEntity.Id);

        // Assert
        Assert.True(updateResult);
        Assert.NotNull(updatedEntity);
        Assert.Equal("Updated Name", updatedEntity.Name);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "To Be Deleted" };
        var savedEntity = await _repository.AddAsync(entity);

        // Act
        var deleteResult = await _repository.DeleteAsync(savedEntity);
        var deletedEntity = await _repository.GetByIdAsync(savedEntity.Id);

        // Assert
        Assert.True(deleteResult);
        Assert.Null(deletedEntity);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    public async Task GetPagedResultAsync_ShouldReturnCorrectPage(int pageIndex, int pageSize)
    {
        // Arrange
        await _repository.EmptyAsync();
        var entities = new List<TestEntity>
        {
            new() { Name = "Test 1" },
            new() { Name = "Test 2" },
            new() { Name = "Test 3" },
            new() { Name = "Test 4" }
        };

        foreach (var entity in entities)
        {
            await _repository.AddAsync(entity);
        }

        var pagedRequest = new PagedRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        // Act
        var result = await _repository.GetPagedResultAsync(pagedRequest);

        // Assert
        Assert.Equal(pageSize, result.Items.Count);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(pageIndex, result.PageIndex);
        Assert.Equal(pageSize, result.PageSize);
    }

    [Fact]
    public async Task GetPagedResultAsync_WithFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _repository.EmptyAsync();

        var entities = new List<TestEntity>
        {
            new() { Name = "Apple" },
            new() { Name = "Banana" },
            new() { Name = "Apple Pie" }
        };

        foreach (var entity in entities)
        {
            await _repository.AddAsync(entity);
        }

        var filter = Builders<TestEntity>.Filter.Regex(x => x.Name, new BsonRegularExpression("^Apple"));
        var pagedRequest = new PagedRequest
        {
            PageIndex = 1,
            PageSize = 10,
            Filter = filter
        };

        // Act
        var result = await _repository.GetPagedResultAsync(pagedRequest);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Contains("Apple", item.Name));
    }

    [Fact]
    public async Task GetPagedResultAsync_WithSort_ShouldReturnSortedResults()
    {
        // Arrange
        await _repository.EmptyAsync();

        var entities = new List<TestEntity>
        {
            new() { Name = "C" },
            new() { Name = "A" },
            new() { Name = "B" }
        };

        foreach (var entity in entities)
        {
            await _repository.AddAsync(entity);
        }

        var sort = Builders<TestEntity>.Sort.Ascending(x => x.Name);
        var pagedRequest = new PagedRequest
        {
            PageIndex = 1,
            PageSize = 10,
            Sort = sort
        };

        // Act
        var result = await _repository.GetPagedResultAsync(pagedRequest);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal("A", result.Items.First().Name);
        Assert.Equal("C", result.Items.Last().Name);
    }

    [Fact]
    public async Task GetPagedResultAsync_WithInvalidPage_ShouldUseDefaultValues()
    {
        // Arrange
        await _repository.EmptyAsync();
        var entities = new List<TestEntity>
        {
            new() { Name = "Test 1" },
            new() { Name = "Test 2" }
        };

        foreach (var entity in entities)
        {
            await _repository.AddAsync(entity);
        }

        var pagedRequest = new PagedRequest
        {
            PageIndex = 0,
            PageSize = 0
        };

        // Act
        var result = await _repository.GetPagedResultAsync(pagedRequest);

        // Assert
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedResultAsync_WithEmptyCollection_ShouldReturnEmptyResult()
    {
        // Arrange
        await _repository.EmptyAsync();
        var pagedRequest = new PagedRequest
        {
            PageIndex = 1,
            PageSize = 10
        };

        // Act
        var result = await _repository.GetPagedResultAsync(pagedRequest);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(10, result.PageSize);
    }
}

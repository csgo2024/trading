using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Trading.Common.Models;
using Trading.Domain;
using Trading.Domain.IRepositories;

namespace Trading.Infrastructure.Repositories;

public class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly IMongoCollection<T> _collection;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public BaseRepository(IMongoDbContext context, IDomainEventDispatcher domainEventDispatcher)
    {
        _collection = context.GetCollection<T>();
        _domainEventDispatcher = domainEventDispatcher;
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, new InsertOneOptions(), cancellationToken);
        await _domainEventDispatcher.DispatchAsync(entity);
        return entity;
    }

    public async Task<bool> UpdateAsync(string id, T entity, CancellationToken cancellationToken = default)
    {
        var result = await _collection.ReplaceOneAsync(x => x.Id == id, entity, cancellationToken: cancellationToken);
        await _domainEventDispatcher.DispatchAsync(entity);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == entity.Id, cancellationToken);
        await _domainEventDispatcher.DispatchAsync(entity);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }
    public async Task<bool> EmptyAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Empty;
        var result = await _collection.DeleteManyAsync(filter, cancellationToken);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    public async Task<PagedResult<T>> GetPagedResultAsync(PagedRequest pagedRequest, CancellationToken cancellationToken = default)
    {
        var pageIndex = pagedRequest.PageIndex < 1 ? 1 : pagedRequest.PageIndex;
        var pageSize = pagedRequest.PageSize < 1 ? 10 : pagedRequest.PageSize;

        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var documentSerializer = serializerRegistry.GetSerializer<T>();
        var renderArgs = new RenderArgs<T>(documentSerializer, serializerRegistry);

        var filter = pagedRequest?.Filter as FilterDefinition<T> ?? Builders<T>.Filter.Empty;
        var sort = pagedRequest?.Sort as SortDefinition<T> ?? Builders<T>.Sort.Ascending(x => x.UpdatedAt);

        var skip = (pageIndex - 1) * pageSize;

        var pipeline = new BsonDocument[]
        {
            new BsonDocument("$match", filter.Render(renderArgs)),
            new BsonDocument("$facet", new BsonDocument
                {
                    {
                        "paginatedResults", new BsonArray
                        {
                            new BsonDocument("$sort", sort.Render(renderArgs)),
                            new BsonDocument("$skip", skip),
                            new BsonDocument("$limit", pageSize)
                        }
                    },
                    {
                        "totalCount", new BsonArray
                        {
                            new BsonDocument("$count", "count")
                        }
                    }
                }
            )
        };

        var result = await _collection.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken).FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            return new PagedResult<T>(new List<T>(), pageIndex, pageSize, 0);
        }

        var items = BsonSerializer.Deserialize<List<T>>(
            result.GetValue("paginatedResults").AsBsonArray.ToJson());

        var totalCountArray = result.GetValue("totalCount").AsBsonArray;
        var totalCount = totalCountArray.Any() ? totalCountArray[0]["count"].AsInt32 : 0;

        return new PagedResult<T>(items, pageIndex, pageSize, totalCount);
    }
}

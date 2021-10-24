using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot.Models
{
    public interface IMongoEntity
    {
        ObjectId ObjectId { get; }
        DateTime CreatedAt { get; }
    }
    public abstract class MongoEntity : IMongoEntity
    {
        [BsonId]
        public virtual ObjectId ObjectId { get; set; }

        [BsonIgnore]
        public virtual DateTime CreatedAt
            => ObjectId.CreationTime;

        public MongoEntity()
        {
            ObjectId = ObjectId.GenerateNewId();
        }
    }

    public abstract class MongoEntity<TType> : IMongoEntity where TType : MongoEntity<TType>
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }

        [BsonIgnore]
        public DateTime CreatedAt
            => ObjectId.CreationTime;

        [BsonIgnore]
        public IMongoCollection<TType> Collection { get; private set; }

        public MongoEntity(IMongoCollection<TType> col)
        {
            Collection = col;
            ObjectId = ObjectId.GenerateNewId();
        }

        public virtual Task ModifyAsync(Action<TType> func)
        {
            func((TType)this);

            return SaveAsync();
        }

        protected void SwitchCollections(IMongoCollection<TType> newCollection)
        {
            Collection = newCollection;
        }

        public virtual Task SaveAsync()
            => Collection.ReplaceOneAsync(x => x.ObjectId == this.ObjectId, (TType)this, new ReplaceOptions() { IsUpsert = true });

        public virtual Task<DeleteResult> DeleteAsync()
            => Collection.DeleteOneAsync(x => x.ObjectId == this.ObjectId);
    }

    public abstract class MongoCachedEntity<TType, TId> : IMongoEntity where TType : MongoCachedEntity<TType, TId>, IEntity<TId>
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }

        [BsonIgnore]
        public DateTime CreatedAt
            => ObjectId.CreationTime;

        [BsonIgnore]
        public IMongoCollection<TType> Collection { get; private set; }

        [BsonIgnore]
        public readonly BaseCache<TType, TId> Cache;

        public MongoCachedEntity(IMongoCollection<TType> col, BaseCache<TType, TId> cache)
        {
            Collection = col;
            Cache = cache;
            ObjectId = ObjectId.GenerateNewId();
        }

        public virtual Task ModifyAsync(Action<TType> func)
        {
            func((TType)this);
            Cache.TryAddOrUpdate((TType)this);
            return SaveAsync();
        }

        protected void SwitchCollections(IMongoCollection<TType> newCollection)
        {
            Collection = newCollection;
        }

        public virtual Task SaveAsync()
        {
            Cache.TryAddOrUpdate((TType)this);
            return Collection.ReplaceOneAsync(x => x.ObjectId == this.ObjectId, (TType)this, new ReplaceOptions() { IsUpsert = true });
        }

        public virtual Task<DeleteResult> DeleteAsync()
        {
            Cache.TryRemove(((TType)this).Id, out var _);
            return Collection.DeleteOneAsync(x => x.ObjectId == this.ObjectId);
        }
    }
}

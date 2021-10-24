using NugetBot.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public class BaseCache<TEntity, TId> where TEntity : class, IEntity<TId>
    {
        public int Size { get; set; }

        private readonly ConcurrentDictionary<TId, TEntity> _dict;
        private readonly ConcurrentQueue<TId> _queue;
        private object _lock = new object();

        public BaseCache(int concurrency, int size)
        {
            Size = size;

            _dict = new ConcurrentDictionary<TId, TEntity>(concurrency, size);
            _queue = new ConcurrentQueue<TId>();
        }

        public bool TryAddOrUpdate(TEntity entity)
        {
            return TryUpdate(entity, out var _) || TryAdd(entity);
        }

        public bool TryAdd(TEntity entity)
        {
            lock (_lock)
            {
                if (_queue.Contains(entity.Id) || _dict.ContainsKey(entity.Id))
                    return false;

                _queue.Enqueue(entity.Id);
                _dict.TryAdd(entity.Id, entity);

                DequeueOld();
                return true;
            }
        }

        public bool TryRemove(TId id, out TEntity removed)
        {
            removed = null;

            lock (_lock)
            {
                if (!_queue.Contains(id) || !_dict.ContainsKey(id))
                    return false;

                _queue.Remove(id);
                _dict.TryRemove(id, out removed);

                return true;
            }
        }

        public bool TryUpdate(TEntity entity, out TEntity old)
        {
            old = null;

            lock (_lock)
            {
                if (!_queue.Contains(entity.Id) || !_dict.ContainsKey(entity.Id))
                    return false;

                _queue.MoveToLast(entity.Id);

                old = _dict[entity.Id];

                _dict[entity.Id] = entity;

                return true;
            }
        }

        public bool TryGet(TId id, out TEntity entity)
            => _dict.TryGetValue(id, out entity);

        public bool Contains(TId id)
            => _queue.Contains(id);

        public void DequeueOld()
        {
            while (_queue.Count > Size && _queue.TryDequeue(out var id))
            {
                _dict.TryRemove(id, out var _);
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public static class QueueExtensions
    {
        public static void MoveToLast<T>(this ConcurrentQueue<T> queue, T itemToMove)
        {
            if (null == queue)
                throw new ArgumentNullException(nameof(queue));

            var list = queue.ToList();

            int index = list.IndexOf(itemToMove);

            if (index < 0)
                return; // Nothing to do

            list.Add(list[index]);
            list.RemoveAt(index);

            queue.Clear();

            foreach (var item in list)
                queue.Enqueue(item);
        }

        public static void Remove<T>(this ConcurrentQueue<T> queue, T itemToMove)
        {
            if (null == queue)
                throw new ArgumentNullException(nameof(queue));

            var list = queue.ToList();

            int index = list.IndexOf(itemToMove);

            if (index < 0)
                return; 

            list.RemoveAt(index);

            queue.Clear();

            foreach (var item in list)
                queue.Enqueue(item);
        }
    }
}

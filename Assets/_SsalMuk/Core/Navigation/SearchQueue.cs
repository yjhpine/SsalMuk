using System.Collections.Generic;

namespace SsalMuk.Core
{
    internal sealed class SearchQueue<T>
    {
        private readonly List<(T value, double priority, double tie, long sequence)> items = new List<(T, double, double, long)>();
        private long sequence;
        public int Count => items.Count;
        public void Push(T value, double priority, double tie = 0)
        {
            var item = (value, priority, tie, sequence++); int index = items.Count; items.Add(item);
            while (index > 0)
            {
                int parent = (index - 1) / 2; if (!Before(item, items[parent])) break;
                items[index] = items[parent]; index = parent;
            }
            items[index] = item;
        }
        public T Pop()
        {
            T result = items[0].value; var last = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
            if (items.Count == 0) return result;
            int index = 0;
            while (index * 2 + 1 < items.Count)
            {
                int child = index * 2 + 1;
                if (child + 1 < items.Count && Before(items[child + 1], items[child])) child++;
                if (!Before(items[child], last)) break;
                items[index] = items[child]; index = child;
            }
            items[index] = last; return result;
        }
        private static bool Before((T value, double priority, double tie, long sequence) a, (T value, double priority, double tie, long sequence) b) =>
            a.priority < b.priority || (a.priority == b.priority && (a.tie < b.tie || (a.tie == b.tie && a.sequence < b.sequence)));
    }
}

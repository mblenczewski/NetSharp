using System.Threading;

namespace NetSharp.Utils
{
    public class RingBuffer<T>
    {
        private readonly T[] buffer;

        private int currentIndex;

        public RingBuffer(int capacity)
        {
            buffer = new T[capacity];

            Capacity = capacity;
            Count = 0;
        }

        public int Capacity { get; }

        public int Count { get; }

        public T Pop()
        {
            T removedItem = buffer[currentIndex--];

            currentIndex = currentIndex < 0 ? currentIndex + Capacity : currentIndex;

            return removedItem;
        }

        public bool Push(T newItem, out T removedItem)
        {
            bool overwroteItem = false;
            removedItem = default;

            if (buffer[currentIndex] != null)
            {
                removedItem = buffer[currentIndex];
                overwroteItem = true;
            }

            buffer[currentIndex] = newItem;

            currentIndex = (currentIndex + 1) % Capacity;

            return overwroteItem;
        }
    }
}
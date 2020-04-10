using System.Collections.Concurrent;

namespace NetSharp.Utils
{
    internal class MyObjectPool<T> where T : class
    {
        internal delegate T CreateObjectDelegate();

        internal delegate bool KeepObjectPredicate(in T instance);

        internal delegate void ResetObjectDelegate(T instance);

        internal delegate void DestroyObjectDelegate(T instance);

        private readonly CreateObjectDelegate createObjectDelegate;
        private readonly KeepObjectPredicate rebufferObjectPredicate;
        private readonly ResetObjectDelegate resetObjectDelegate;
        private readonly DestroyObjectDelegate destroyObjectDelegate;

        private readonly ConcurrentBag<T> objectBuffer;

        internal MyObjectPool(in CreateObjectDelegate createDelegate, in ResetObjectDelegate resetDelegate, in DestroyObjectDelegate destroyDelegate, in KeepObjectPredicate keepObjectPredicate)
        {
            createObjectDelegate = createDelegate;

            resetObjectDelegate = resetDelegate;

            destroyObjectDelegate = destroyDelegate;

            rebufferObjectPredicate = keepObjectPredicate;

            objectBuffer = new ConcurrentBag<T>();
        }

        internal T Rent()
        {
            return objectBuffer.TryTake(out T result) ? result : createObjectDelegate();
        }

        internal void Return(T instance)
        {
            if (rebufferObjectPredicate(instance))
            {
                resetObjectDelegate(instance);

                objectBuffer.Add(instance);
            }
            else
            {
                destroyObjectDelegate(instance);
            }
        }
    }
}
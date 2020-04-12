using System.Collections.Concurrent;

namespace NetSharp.Utils
{
    //TODO document
    public class SlimObjectPool<T> where T : class
    {
        public delegate T CreateObjectDelegate();

        public delegate bool CanRebufferObjectPredicate(in T instance);

        public delegate void ResetObjectDelegate(T instance);

        public delegate void DestroyObjectDelegate(T instance);

        private readonly CreateObjectDelegate createObjectDelegate;
        private readonly CanRebufferObjectPredicate canObjectBeRebufferedPredicate;
        private readonly ResetObjectDelegate resetObjectDelegate;
        private readonly DestroyObjectDelegate destroyObjectDelegate;

        private readonly ConcurrentQueue<T> objectBuffer;

        public SlimObjectPool(in CreateObjectDelegate createDelegate, in ResetObjectDelegate resetDelegate,
            in DestroyObjectDelegate destroyDelegate, in CanRebufferObjectPredicate rebufferPredicate)
        {
            createObjectDelegate = createDelegate;

            resetObjectDelegate = resetDelegate;

            destroyObjectDelegate = destroyDelegate;

            canObjectBeRebufferedPredicate = rebufferPredicate;

            objectBuffer = new ConcurrentQueue<T>();
        }

        public T Rent()
        {
            return objectBuffer.TryDequeue(out T result) ? result : createObjectDelegate();
        }

        public void Return(T instance)
        {
            if (canObjectBeRebufferedPredicate(instance))
            {
                resetObjectDelegate(instance);

                objectBuffer.Enqueue(instance);
            }
            else
            {
                destroyObjectDelegate(instance);
            }
        }
    }
}
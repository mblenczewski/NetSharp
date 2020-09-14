using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NetSharp.Utils
{
    /// <summary>
    /// Provides a lightweight implementation of an object pool for classes.
    /// </summary>
    /// <typeparam name="T">
    /// The type of item stored in the pool.
    /// </typeparam>
    public sealed class SlimObjectPool<T> : IDisposable
    {
        private readonly CanReuseObjectPredicate canObjectBeRebufferedPredicate;
        private readonly CreateObjectDelegate createObjectDelegate;
        private readonly DestroyObjectDelegate destroyObjectDelegate;
        private readonly IProducerConsumerCollection<T> objectBuffer;
        private readonly ResetObjectDelegate resetObjectDelegate;

        /// <summary>
        /// Constructs a new instance of the <see cref="SlimObjectPool{T}" /> class.
        /// </summary>
        /// <param name="createDelegate">
        /// The delegate method to use to create new pooled object instances.
        /// </param>
        /// <param name="resetDelegate">
        /// The delegate method to use to reset used pooled object instances.
        /// </param>
        /// <param name="destroyDelegate">
        /// The delegate method to use to destroy pooled object instances that cannot be reused.
        /// </param>
        /// <param name="rebufferPredicate">
        /// The delegate method to use to decide whether an instance can be reused.
        /// </param>
        /// <param name="baseCollection">
        /// The underlying pooled object buffer to use.
        /// </param>
        public SlimObjectPool(in CreateObjectDelegate createDelegate, in ResetObjectDelegate resetDelegate,
            in DestroyObjectDelegate destroyDelegate, in CanReuseObjectPredicate rebufferPredicate,
            in IProducerConsumerCollection<T> baseCollection)
        {
            createObjectDelegate = createDelegate;

            resetObjectDelegate = resetDelegate;

            destroyObjectDelegate = destroyDelegate;

            canObjectBeRebufferedPredicate = rebufferPredicate;

            objectBuffer = baseCollection;
        }

        /// <summary>
        /// Constructs a new instance of the <see cref="SlimObjectPool{T}" /> class.
        /// </summary>
        /// <param name="createDelegate">
        /// The delegate method to use to create new pooled object instances.
        /// </param>
        /// <param name="resetDelegate">
        /// The delegate method to use to reset used pooled object instances.
        /// </param>
        /// <param name="destroyDelegate">
        /// The delegate method to use to destroy pooled object instances that cannot be reused.
        /// </param>
        /// <param name="rebufferPredicate">
        /// The delegate method to use to decide whether an instance can be reused.
        /// </param>
        public SlimObjectPool(in CreateObjectDelegate createDelegate, in ResetObjectDelegate resetDelegate,
            in DestroyObjectDelegate destroyDelegate, in CanReuseObjectPredicate rebufferPredicate)
            : this(in createDelegate, in resetDelegate, in destroyDelegate, in rebufferPredicate, new ConcurrentBag<T>())
        {
        }

        /// <summary>
        /// Delegate method to check whether the given <paramref name="instance" /> can and should be placed back into the pool. If <c>true</c> is
        /// returned, the <paramref name="instance" /> is reset and placed back into the pool. Otherwise, the instance is destroyed.
        /// </summary>
        /// <param name="instance">
        /// The instance to check.
        /// </param>
        /// <returns>
        /// Whether the given instance should be placed back into the pool.
        /// </returns>
        public delegate bool CanReuseObjectPredicate(ref T instance);

        /// <summary>
        /// Delegate method for creating fresh <typeparamref name="T" /> instances to be stored in the pool.
        /// </summary>
        /// <returns>
        /// A configured <typeparamref name="T" /> instance.
        /// </returns>
        public delegate T CreateObjectDelegate();

        /// <summary>
        /// Delegate method to destroy a used <paramref name="instance" /> which cannot be reused.
        /// </summary>
        /// <param name="instance">
        /// The instance to destroy.
        /// </param>
        public delegate void DestroyObjectDelegate(T instance);

        /// <summary>
        /// Delegate method to reset a used <paramref name="instance" /> before placing it back into the pool.
        /// </summary>
        /// <param name="instance">
        /// The instance which should be reset.
        /// </param>
        public delegate void ResetObjectDelegate(ref T instance);

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (T pooledObject in objectBuffer)
            {
                destroyObjectDelegate(pooledObject);
            }
        }

        /// <summary>
        /// Leases a new <typeparamref name="T" /> instance from the pool, and returns it.
        /// </summary>
        /// <returns>
        /// The <typeparamref name="T" /> instance which was fetched from the pool.
        /// </returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public T Rent()
        {
            bool rentedInstance = objectBuffer.TryTake(out T result);

            return rentedInstance ? result : createObjectDelegate();
        }

        /// <summary>
        /// Returns a previously leased <typeparamref name="T" /> instance to the pool.
        /// </summary>
        /// <param name="instance">
        /// The previously leased instance which should be returned.
        /// </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Return(T instance)
        {
            if (canObjectBeRebufferedPredicate(ref instance))
            {
                resetObjectDelegate(ref instance);

                objectBuffer.TryAdd(instance);

                //bool couldRebuffer = false;

                //while (!couldRebuffer)
                //{
                //    couldRebuffer = objectBuffer.TryAdd(instance);
                //}
            }
            else
            {
                destroyObjectDelegate(instance);
            }
        }
    }
}
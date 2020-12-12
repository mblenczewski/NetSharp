using System;
using System.Collections.Concurrent;

namespace NetSharp.Utils
{
    /// <summary>
    /// Provides a lightweight implementation of an object pool for classes.
    /// </summary>
    /// <typeparam name="T">
    /// The type of item stored in the pool.
    /// </typeparam>
    internal sealed class SlimObjectPool<T> : IDisposable
    {
        private readonly CreateObject createObject;

        private readonly DestroyObject destroyObject;

        private readonly IProducerConsumerCollection<T> objectBuffer;

        private readonly ResetObject resetObject;

        /// <summary>
        /// Initialises a new instance of the <see cref="SlimObjectPool{T}" /> class.
        /// </summary>
        /// <param name="create">
        /// The delegate method to use to create new pooled object instances.
        /// </param>
        /// <param name="reset">
        /// The delegate method to use to reset used pooled object instances.
        /// </param>
        /// <param name="destroy">
        /// The delegate method to use to destroy pooled object instances that cannot be reused.
        /// </param>
        internal SlimObjectPool(CreateObject create, ResetObject reset, DestroyObject destroy)
        {
            createObject = create;
            resetObject = reset;
            destroyObject = destroy;

            objectBuffer = new ConcurrentBag<T>();
        }

        /// <summary>
        /// Delegate method for creating fresh <typeparamref name="T" /> instances to be stored in the pool.
        /// </summary>
        /// <returns>
        /// A configured <typeparamref name="T" /> instance.
        /// </returns>
        internal delegate T CreateObject();

        /// <summary>
        /// Delegate method to destroy a used <paramref name="instance" /> which cannot be reused.
        /// </summary>
        /// <param name="instance">
        /// The instance to destroy.
        /// </param>
        internal delegate void DestroyObject(T instance);

        /// <summary>
        /// Delegate method to reset a used <paramref name="instance" /> before placing it back into the pool.
        /// </summary>
        /// <param name="instance">
        /// The instance which should be reset.
        /// </param>
        internal delegate void ResetObject(ref T instance);

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (T pooledObject in objectBuffer)
            {
                destroyObject(pooledObject);
            }
        }

        /// <summary>
        /// Leases a new <typeparamref name="T" /> instance from the pool, and returns it.
        /// </summary>
        /// <returns>
        /// The <typeparamref name="T" /> instance which was fetched from the pool.
        /// </returns>
        internal T Rent()
        {
            bool successfullyRentedInstance = objectBuffer.TryTake(out T instance);

            return successfullyRentedInstance ? instance : createObject();
        }

        /// <summary>
        /// Returns a previously leased <typeparamref name="T" /> instance to the pool.
        /// </summary>
        /// <param name="instance">
        /// The previously leased instance which should be returned.
        /// </param>
        internal void Return(T instance)
        {
            resetObject(ref instance);

            bool successfullyRebufferedInstance = objectBuffer.TryAdd(instance);

            if (!successfullyRebufferedInstance)
            {
                destroyObject(instance);
            }
        }
    }
}

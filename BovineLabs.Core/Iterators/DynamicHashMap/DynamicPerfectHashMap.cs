﻿// <copyright file="DynamicPerfectHashMap.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Core.Iterators
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using Unity.Burst.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;

    [DebuggerTypeProxy(typeof(DynamicPerfectHashMapDebuggerTypeProxy<,>))]
    public readonly unsafe struct DynamicPerfectHashMap<TKey, TValue> : IEnumerable<KVPair<TKey, TValue>>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly DynamicPerfectHashMapHelper<TKey, TValue>* helper;

        internal DynamicPerfectHashMap(DynamicBuffer<byte> buffer)
        {
            CheckSize(buffer);

            this.helper = buffer.AsHelperReadOnly<TKey, TValue>();
        }

        internal DynamicPerfectHashMapHelper<TKey, TValue>* Helper => this.helper;

        /// <summary> Gets and sets values by key. </summary>
        /// <remarks> Getting a key that is not present will throw. Setting a key that is not already present will add the key. </remarks>
        /// <param name="key"> The key to look up. </param>
        /// <value> The value associated with the key. </value>
        /// <exception cref="ArgumentException"> For getting, thrown if the key was not present. </exception>
        public TValue this[TKey key]
        {
            get
            {
                if (Hint.Unlikely(!this.TryGetValue(key, out var value)))
                {
                    this.ThrowKeyNotPresent(key);
                    return default;
                }

                return value;
            }
            set
            {
                if (!this.TryGetIndex(key, out var index))
                {
                    this.ThrowKeyNotPresent(key);
                }

                this.helper->Values[index] = value;
            }
        }

        /// <summary> Returns the value associated with a key. </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="item">Outputs the value associated with the key. Outputs default if the key was not present.</param>
        /// <returns>True if the key was present.</returns>
        public bool TryGetValue(TKey key, out TValue item)
        {
            if (!this.TryGetIndex(key, out var index))
            {
                item = default;
                return false;
            }

            var value = item = this.helper->Values[index];
            return UnsafeUtility.MemCmp(&value, &this.helper->NullValue, sizeof(TValue)) != 0;
        }

        public TValue GetNoCheck(TKey key)
        {
            if (!this.TryGetIndex(key, out var index))
            {
                this.ThrowKeyNotPresent(key);
                return default;
            }

            return this.helper->Values[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetIndex(TKey key, out int index)
        {
            index = this.IndexFor(key);
            return index >= 0 && index < this.helper->Size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexFor(TKey key) {
            return key.GetHashCode() & (this.helper->Size - 1);
        }

        /// <summary>
        /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
        /// </summary>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        public IEnumerator<KVPair<TKey, TValue>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
        /// </summary>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckSize(DynamicBuffer<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                throw new InvalidOperationException("Buffer not initialized");
            }

            if (buffer.Length < UnsafeUtility.SizeOf<DynamicPerfectHashMapHelper<TKey, TValue>>())
            {
                throw new InvalidOperationException("Buffer has data but is too small to be a header.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private void ThrowKeyNotPresent(TKey key)
        {
            throw new ArgumentException($"Key: {key} is not present.");
        }

    }

    internal sealed unsafe class DynamicPerfectHashMapDebuggerTypeProxy<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private readonly DynamicPerfectHashMapHelper<TKey, TValue>* helper;

        public DynamicPerfectHashMapDebuggerTypeProxy(DynamicPerfectHashMap<TKey, TValue> target)
        {
            this.helper = target.Helper;
        }

        public List<Pair<TKey, TValue>> Items
        {
            get
            {
                var result = new List<Pair<TKey, TValue>>();

                if (this.helper == null)
                {
                    return result;
                }

                var keys = this.helper->Keys;
                var values = this.helper->Values;
                var size = this.helper->Size;

                for (var i = 0; i < size; ++i)
                {
                    var value = values[i];

                    if (UnsafeUtility.MemCmp(&value, &this.helper->NullValue, sizeof(TValue)) != 0)
                    {
                        result.Add(new Pair<TKey, TValue>(keys[i], value));
                    }
                }

                return result;
            }
        }
    }
}

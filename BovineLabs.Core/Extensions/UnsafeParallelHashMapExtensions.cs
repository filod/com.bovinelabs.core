﻿// <copyright file="UnsafeParallelHashMapExtensions.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Core.Extensions
{
    using System;
    using System.Diagnostics;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public static class UnsafeParallelHashMapExtensions
    {
        public static unsafe void ClearAndAddBatchUnsafe<TKey, TValue>(
            [NoAlias] ref this UnsafeParallelHashMap<TKey, TValue> hashMap,
            [NoAlias] TKey* keys,
            [NoAlias] TValue* values,
            int length)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            hashMap.Clear();

            if (hashMap.Capacity < length)
            {
                hashMap.Capacity = length;
            }

            UnsafeUtility.MemCpy(hashMap.m_Buffer->keys, keys, length * UnsafeUtility.SizeOf<TKey>());
            UnsafeUtility.MemCpy(hashMap.m_Buffer->values, values, length * UnsafeUtility.SizeOf<TValue>());

            var buckets = (int*)hashMap.m_Buffer->buckets;
            var nextPtrs = (int*)hashMap.m_Buffer->next;

            var bucketCapacityMask = hashMap.m_Buffer->bucketCapacityMask;

            for (var idx = 0; idx < length; idx++)
            {
                var bucket = keys[idx].GetHashCode() & bucketCapacityMask;
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = idx;
            }

            hashMap.m_Buffer->allocatedIndexLength = length;
        }

        /// <summary>
        /// Clear a <see cref="NativeParallelHashMap{TKey,TValue}" /> then efficiently add a collection of keys and values to it.
        /// This is much faster than iterating and using Add.
        /// NOTE: this is not safe. It does not check for duplicates and must only be used when keys are gauranteed to be unique.
        /// </summary>
        /// <param name="hashMap"> The hashmap to clear and add to. </param>
        /// <param name="keys"> Collection of keys, the length should match the length of values. </param>
        /// <param name="values"> Collection of values, the length should match the length of keys. </param>
        /// <typeparam name="TKey"> The key type. </typeparam>
        /// <typeparam name="TValue"> The value type. </typeparam>
        public static unsafe void ClearAndAddBatchUnsafe<TKey, TValue>(
            [NoAlias] ref this UnsafeParallelHashMap<TKey, TValue> hashMap,
            [NoAlias] NativeSlice<TKey> keys,
            [NoAlias] NativeArray<TValue> values)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            CheckLengthsMatch(keys.Length, values.Length);

            var length = keys.Length;

            hashMap.Clear();

            if (hashMap.Capacity < length)
            {
                hashMap.Capacity = length;
            }

            UnsafeUtility.MemCpyStride(hashMap.m_Buffer->keys, UnsafeUtility.SizeOf<TKey>(), keys.GetUnsafeReadOnlyPtr(), keys.Stride, UnsafeUtility.SizeOf<TKey>(), length);
            UnsafeUtility.MemCpy(hashMap.m_Buffer->values, values.GetUnsafeReadOnlyPtr(), length * UnsafeUtility.SizeOf<TValue>());

            var buckets = (int*)hashMap.m_Buffer->buckets;
            var nextPtrs = (int*)hashMap.m_Buffer->next;
            var bucketCapacityMask = hashMap.m_Buffer->bucketCapacityMask;

            for (var idx = 0; idx < length; idx++)
            {
                var bucket = keys[idx].GetHashCode() & bucketCapacityMask;
                nextPtrs[idx] = buckets[bucket];
                buckets[bucket] = idx;
            }

            hashMap.m_Buffer->allocatedIndexLength = length;
        }

        public static unsafe bool TryGetFirstKeyValue<TKey, TValue>(ref this UnsafeParallelHashMap<TKey, TValue> map, out TKey key, out TValue value, ref int index)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return map.m_Buffer->TryGetFirstKeyValue<TKey, TValue>(out key, out value, ref index);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckLengthsMatch(int keys, int values)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (keys != values)
            {
                throw new ArgumentException("Key and value array don't match");
            }
#endif
        }
    }
}

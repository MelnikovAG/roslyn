﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// The collection of extension methods for the <see cref="Dictionary{TKey, TValue}"/> type
    /// </summary>
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// If the given key is not found in the dictionary, add it with the given value and return the value.
        /// Otherwise return the existing value associated with that key.
        /// </summary>
        public static TValue GetOrAdd<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue value)
            where TKey : notnull
        {
            if (dictionary.TryGetValue(key, out var existingValue))
            {
                return existingValue;
            }
            else
            {
                dictionary.Add(key, value);
                return value;
            }
        }

        /// <summary>
        /// If the given key is not found in the dictionary, add it with the result of invoking getValue and return the value.
        /// Otherwise return the existing value associated with that key.
        /// </summary>
        public static TValue GetOrAdd<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            Func<TValue> getValue)
            where TKey : notnull
        {
            if (dictionary.TryGetValue(key, out var existingValue))
            {
                return existingValue;
            }
            else
            {
                var value = getValue();
                dictionary.Add(key, value);
                return value;
            }
        }

#if !NETCOREAPP
        public static bool TryAdd<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue value)
            where TKey : notnull
        {
            if (dictionary.TryGetValue(key, out var _))
            {
                return false;
            }

            dictionary.Add(key, value);
            return true;
        }
#endif

        public static void AddPooled<K, V>(this Dictionary<K, ArrayBuilder<V>> dictionary, K key, V value)
            where K : notnull
        {
            if (!dictionary.TryGetValue(key, out var values))
            {
                values = ArrayBuilder<V>.GetInstance();
                dictionary[key] = values;
            }

            values.Add(value);
        }

        /// <summary>
        /// Converts the passed in dictionary to an <see cref="ImmutableSegmentedDictionary{TKey, TValue}"/>, where all
        /// the values in the passed builder will be converted to an <see cref="ImmutableArray{T}"/> using <see
        /// cref="ArrayBuilder{T}.ToImmutableAndFree"/>.  The <paramref name="dictionary"/> will be freed at the end of
        /// this method as well, and should not be used afterwards.
        /// </summary>
        public static ImmutableSegmentedDictionary<K, ImmutableArray<V>> ToImmutableSegmentedDictionaryAndFree<K, V>(this PooledDictionary<K, ArrayBuilder<V>> dictionary)
            where K : notnull
        {
            var result = ImmutableSegmentedDictionary.CreateBuilder<K, ImmutableArray<V>>();
            foreach (var (key, values) in dictionary)
            {
                result.Add(key, values.ToImmutableAndFree());
            }

            dictionary.Free();
            return result.ToImmutable();
        }
    }
}

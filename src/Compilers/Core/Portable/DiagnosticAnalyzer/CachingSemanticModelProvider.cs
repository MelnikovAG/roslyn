﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable RSEXPERIMENTAL001 // internal usage of experimental API

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Microsoft.CodeAnalysis.Diagnostics
{
    /// <summary>
    /// Provider that caches semantic models for requested trees, with a strong reference to the model.
    /// Clients using this provider are responsible for maintaining the lifetime of the entries in this cache,
    /// and should invoke <see cref="ClearCache(SyntaxTree, Compilation)"/> and <see cref="ClearCache(Compilation)"/> to clear entries when appropriate.
    /// For example, <see cref="CompilationWithAnalyzers"/> uses this provider to ensure that semantic model instances
    /// are shared between the compiler and analyzers for improved analyzer execution performance. The underlying
    /// <see cref="AnalyzerDriver"/> executing analyzers clears per-tree entries in the cache whenever a <see cref="CompilationUnitCompletedEvent"/>
    /// has been processed, indicating all relevant analyzers have executed on the corresponding syntax tree for the event.
    /// Similarly, it clears the entire compilation wide cache whenever a <see cref="CompilationCompletedEvent"/> has been processed,
    /// indicating all relevant analyzers have executed on the entire compilation.
    /// </summary>
    internal sealed class CachingSemanticModelProvider : SemanticModelProvider
    {
        // Provide access to CachingSemanticModelProvider through a singleton. The inner CWT is static
        // to avoid leak potential -- see https://github.com/dotnet/runtime/issues/12255.
        // CachingSemanticModelProvider.s_providerCache -> PerCompilationProvider -> Compilation -> CachingSemanticModelProvider
        public static CachingSemanticModelProvider Instance { get; } = new CachingSemanticModelProvider();

        private static readonly ConditionalWeakTable<Compilation, PerCompilationProvider>.CreateValueCallback s_createProviderCallback
            = new ConditionalWeakTable<Compilation, PerCompilationProvider>.CreateValueCallback(compilation => new PerCompilationProvider(compilation));

        private static readonly ConditionalWeakTable<Compilation, PerCompilationProvider> s_providerCache = new ConditionalWeakTable<Compilation, PerCompilationProvider>();

        private CachingSemanticModelProvider()
        {
        }

        public override SemanticModel GetSemanticModel(SyntaxTree tree, Compilation compilation, SemanticModelOptions options = default)
            => s_providerCache.GetValue(compilation, s_createProviderCallback).GetSemanticModel(tree, options);

        internal void ClearCache(SyntaxTree tree, Compilation compilation)
        {
            if (s_providerCache.TryGetValue(compilation, out var provider))
            {
                provider.ClearCachedSemanticModel(tree);
            }
        }

        internal void ClearCache(Compilation compilation)
        {
            s_providerCache.Remove(compilation);
        }

        private sealed class PerCompilationProvider
        {
            private readonly Compilation _compilation;
            private readonly ConcurrentDictionary<SyntaxTree, SemanticModel> _semanticModelsMap;

            // Cached delegate to avoid allocations in ConcurrentDictionary.GetOrAdd invocations.
            // We only care about caching semantic models for internal callers, which use the default 'ignoreAccessibility = false'.
            private readonly Func<SyntaxTree, SemanticModel> _createSemanticModel;

            public PerCompilationProvider(Compilation compilation)
            {
                _compilation = compilation;
                _semanticModelsMap = new ConcurrentDictionary<SyntaxTree, SemanticModel>();
                _createSemanticModel = tree => compilation.CreateSemanticModel(tree, options: default);
            }

            public SemanticModel GetSemanticModel(SyntaxTree tree, SemanticModelOptions options)
            {
                // We only care about caching semantic models for internal callers, which use the default 'ignoreAccessibility = false'.
                return options == SemanticModelOptions.None
                    ? _semanticModelsMap.GetOrAdd(tree, _createSemanticModel)
                    : _compilation.CreateSemanticModel(tree, options);
            }

            public void ClearCachedSemanticModel(SyntaxTree tree)
            {
                _semanticModelsMap.TryRemove(tree, out _);
            }
        }
    }
}

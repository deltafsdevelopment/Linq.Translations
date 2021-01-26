// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Runtime.CompilerServices;
    using System.Reflection;

namespace Microsoft.Linq.Translations
{
    /// <summary>
    /// Extension methods over IQueryable to turn on expression translation via a
    /// specified or default TranslationMap.
    /// </summary>
    public static class ExpressiveExtensions
    {
        /// <summary>
        /// Create a new <see cref="IQueryable{T}"/> based upon the
        /// <paramref name="source"/> with the translatable properties decomposed back
        /// into their expression trees ready for translation to a remote provider using
        /// the default <see cref="TranslationMap"/>.
        /// </summary>
        /// <typeparam name="T">Result type of the query.</typeparam>
        /// <param name="source">Source query to translate.</param>
        /// <returns><see cref="IQueryable{T}"/> containing translated query.</returns>
        public static IQueryable<T> WithTranslations<T>(this IQueryable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.CreateQuery<T>(WithTranslations(source.Expression));
        }

        /// <summary>
        /// Create a new <see cref="IQueryable{T}"/> based upon the
        /// <paramref name="source"/> with the translatable properties decomposed back
        /// into their expression trees ready for translation to a remote provider using
        /// a specific <paramref name="map"/>.
        /// </summary>
        /// <typeparam name="T">Result type of the query.</typeparam>
        /// <param name="source">Source query to translate.</param>
        /// <param name="map"><see cref="TranslationMap"/> used to translate property accesses.</param>
        /// <returns><see cref="IQueryable{T}"/> containing translated query.</returns>
        public static IQueryable<T> WithTranslations<T>(this IQueryable<T> source, TranslationMap map)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (map == null) throw new ArgumentNullException(nameof(map));

            return source.Provider.CreateQuery<T>(WithTranslations(source.Expression, map));
        }

        /// <summary>
        /// Create a new <see cref="Expression"/> tree based upon the
        /// <paramref name="expression"/> with translatable properties decomposed back
        /// into their expression trees ready for translation to a remote provider using
        /// the default <see cref="TranslationMap"/>.
        /// </summary>
        /// <param name="expression">Expression tree to translate.</param>
        /// <returns><see cref="Expression"/> tree with translatable expressions translated.</returns>
        public static Expression WithTranslations(Expression expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            return WithTranslations(expression, TranslationMap.DefaultMap);
        }

        /// <summary>
        /// Create a new <see cref="Expression"/> tree based upon the
        /// <paramref name="expression"/> with translatable properties decomposed back
        /// into their expression trees ready for translation to a remote provider using
        /// the default <see cref="TranslationMap"/>.
        /// </summary>
        /// <param name="expression">Expression tree to translate.</param>
        /// <param name="map"><see cref="TranslationMap"/> used to translate property accesses.</param>
        /// <returns><see cref="Expression"/> tree with translatable expressions translated.</returns>
        public static Expression WithTranslations(Expression expression, TranslationMap map)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            if (map == null) throw new ArgumentNullException(nameof(map));

            return new TranslatingVisitor(map).Visit(expression);
        }

        private static void EnsureTypeInitialized(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            try
            {
                // Ensure the static members are accessed class' ctor
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
            catch (TypeInitializationException)
            {
            }
        }

        /// <summary>
        /// Extends the expression visitor to translate properties to expressions
        /// according to the provided translation map.
        /// </summary>
        private class TranslatingVisitor : ExpressionVisitor
        {
            private readonly Stack<KeyValuePair<ParameterExpression, Expression>> bindings = new Stack<KeyValuePair<ParameterExpression, Expression>>();
            private readonly TranslationMap map;

            internal TranslatingVisitor(TranslationMap map)
            {
                this.map = map ?? throw new ArgumentNullException(nameof(map));
            }

            /// <summary>
            ///  Walk up the inheritance heirarchy searching for a compiled expression attached to a
            ///  property of the given name
            /// </summary>
            /// <param name="propName">Name of the property to search for</param>
            /// <param name="type">Type of the member to search against</param>
            /// <returns>Compiled expression if found or null if not</returns>
            private CompiledExpression FindCompiledExpression(String propName, Type type)
            {
                while (type != typeof(Object))
                {
                    CompiledExpression cp;
                    MemberInfo mi = type.GetProperty(propName, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    EnsureTypeInitialized(type);
                    if (mi != null && map.TryGetValue(mi, out cp))
                    {
                        return cp;
                    }
                    type = type.BaseType;
                }
                return null;
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node == null) throw new ArgumentNullException(nameof(node));
                // deltafsdevelopment 10 Nov 2015 - Fix to original code here so that the code searches for CompiledExpressions
                // right through the inheritance heirarchy to allow them to be defined on base classes and on overrides
                if (node.Expression != null)
                {
                    Type type = node.Expression.Type;
                    String propName = node.Member.Name;
                    CompiledExpression cp = FindCompiledExpression(propName, type);
                    if (cp != null)
                    {
                        return VisitCompiledExpression(cp, node.Expression);
                    }
                }
                return base.VisitMember(node);

            }

            private Expression VisitCompiledExpression(CompiledExpression ce, Expression expression)
            {
                bindings.Push(new KeyValuePair<ParameterExpression, Expression>(ce.BoxedGet.Parameters.Single(), expression));
                var body = Visit(ce.BoxedGet.Body);
                bindings.Pop();
                return body;
            }

            protected override Expression VisitParameter(ParameterExpression p)
            {
                var binding = bindings.FirstOrDefault(b => b.Key == p);
                return (binding.Value == null) ? base.VisitParameter(p) : Visit(binding.Value);
            }
        }
    }
}

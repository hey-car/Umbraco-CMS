﻿//Copyright (C) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Diagnostics;

namespace Umbraco.Core.Dynamics
{
    internal static class DynamicQueryable
    {
        public static IQueryable<T> Where<T>(this IQueryable<T> source, string predicate, params object[] values)
        {
            return (IQueryable<T>)Where((IQueryable)source, predicate, values);
        }

        public static IQueryable Where(this IQueryable source, string predicate, params object[] values)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");
            LambdaExpression lambda = DynamicExpression.ParseLambda(source.ElementType, typeof(bool), predicate, true, values);
            if (lambda.Parameters.Count > 0 && lambda.Parameters[0].Type == typeof(DynamicPublishedContent))
            {
                //source list is DynamicNode and the lambda returns a Func<object>
                IQueryable<DynamicPublishedContent> typedSource = source as IQueryable<DynamicPublishedContent>;
                var compiledFunc = lambda.Compile();
                Func<DynamicPublishedContent, object> func = null;
                Func<DynamicPublishedContent, bool> boolFunc = null;
                if (compiledFunc is Func<DynamicPublishedContent, object>)
                {
                    func = (Func<DynamicPublishedContent, object>)compiledFunc;
                }
                if (compiledFunc is Func<DynamicPublishedContent, bool>)
                {
                    boolFunc = (Func<DynamicPublishedContent, bool>)compiledFunc;
                }
                return typedSource.Where(delegate(DynamicPublishedContent node)
                {
                    object value = -1;
                    //value = func(node);
                    //I can't figure out why this is double func<>'d
                    try
                    {
                        if (func != null)
                        {
                            var firstFuncResult = func(node);
                            if (firstFuncResult is Func<DynamicPublishedContent, object>)
                            {
                                value = (firstFuncResult as Func<DynamicPublishedContent, object>)(node);
                            }
                            if (firstFuncResult is Func<DynamicPublishedContent, bool>)
                            {
                                value = (firstFuncResult as Func<DynamicPublishedContent, bool>)(node);
                            }
                            if (firstFuncResult is bool)
                            {
                                return (bool)firstFuncResult;
                            }
                            if (value is bool)
                            {
                                return (bool)value;
                            }
                        }
                        if (boolFunc != null)
                        {
                            return boolFunc(node);
                        }
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.Message);
                        return false;
                    }
                }).AsQueryable();
            }
            else
            {
                return source.Provider.CreateQuery(
                    Expression.Call(
                        typeof(Queryable), "Where",
                        new Type[] { source.ElementType },
                        source.Expression, Expression.Quote(lambda)));
            }
        }

        public static IQueryable Select(this IQueryable<DynamicPublishedContent> source, string selector, params object[] values)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (selector == null) throw new ArgumentNullException("selector");
            LambdaExpression lambda = DynamicExpression.ParseLambda(source.ElementType, typeof(object), selector, false, values);
            if (lambda.Parameters.Count > 0 && lambda.Parameters[0].Type == typeof(DynamicPublishedContent))
            {
                //source list is DynamicNode and the lambda returns a Func<object>
                IQueryable<DynamicPublishedContent> typedSource = source as IQueryable<DynamicPublishedContent>;
                var compiledFunc = lambda.Compile();
                Func<DynamicPublishedContent, object> func = null;
                if (compiledFunc is Func<DynamicPublishedContent, object>)
                {
                    func = (Func<DynamicPublishedContent, object>)compiledFunc;
                }
                return typedSource.Select(delegate(DynamicPublishedContent node)
                {
                    object value = null;
                    value = func(node);
                    if (value is Func<DynamicPublishedContent, object>)
                    {
                        var innerValue = (value as Func<DynamicPublishedContent, object>)(node);
                        return innerValue;
                    }
                    return value;
                }).AsQueryable();
            }
            else
            {
                return source.Provider.CreateQuery(
                    Expression.Call(
                        typeof(Queryable), "Select",
                        new Type[] { source.ElementType, lambda.Body.Type },
                        source.Expression, Expression.Quote(lambda)));
            }
        }

        public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, string ordering, params object[] values)
        {
            return (IQueryable<T>)OrderBy((IQueryable)source, ordering, values);
        }

        public static IQueryable OrderBy(this IQueryable source, string ordering, params object[] values)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (ordering == null) throw new ArgumentNullException("ordering");

            IQueryable<DynamicPublishedContent> typedSource = source as IQueryable<DynamicPublishedContent>;
            if (!ordering.Contains(","))
            {
                bool descending = false;
                if (ordering.IndexOf(" descending", StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    ordering = ordering.Replace(" descending", "");
                    descending = true;
                }
                if (ordering.IndexOf(" desc", StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    ordering = ordering.Replace(" desc", "");
                    descending = true;
                }

                LambdaExpression lambda = DynamicExpression.ParseLambda(source.ElementType, typeof(object), ordering, false, values);
                if (lambda.Parameters.Count > 0 && lambda.Parameters[0].Type == typeof(DynamicPublishedContent))
                {
                    //source list is DynamicNode and the lambda returns a Func<object>
                    Func<DynamicPublishedContent, object> func = (Func<DynamicPublishedContent, object>)lambda.Compile();
                    //get the values out
                    var query = typedSource.ToList().ConvertAll(item => new { node = item, key = EvaluateDynamicNodeFunc(item, func) });
                    if (query.Count == 0)
                    {
                        return source;
                    }
                    var types = from i in query
                                group i by i.key.GetType() into g
                                where g.Key != typeof(DynamicNull)
                                orderby g.Count() descending
                                select new { g, Instances = g.Count() };
                    var dominantType = types.First().g.Key;

                    // NH - add culture dependencies
                    StringComparer comp = StringComparer.Create(CultureInfo.CurrentCulture, true);

                    if (!descending)
                    {
                        // if the dominant type is a string we'll ensure that strings are sorted based on culture settings on node
                        if (dominantType.FullName == "System.String") 
                            return query.OrderBy(item => item.key.ToString(), comp).Select(item => item.node).AsQueryable();
                        else
                            return query.OrderBy(item => GetObjectAsTypeOrDefault(item.key, dominantType)).Select(item => item.node).AsQueryable();
                    }
                    else
                    {
                        if (dominantType.FullName == "System.String")
                            return query.OrderByDescending(item => item.key.ToString(), comp).Select(item => item.node).AsQueryable();
                        else
                            return query.OrderByDescending(item => GetObjectAsTypeOrDefault(item.key, dominantType)).Select(item => item.node).AsQueryable();
                    }
                }
            }

            bool isDynamicNodeList = false;
            if (typedSource != null)
            {
                isDynamicNodeList = true;
            }

            ParameterExpression[] parameters = new ParameterExpression[] {
                Expression.Parameter(source.ElementType, "") };
            ExpressionParser parser = new ExpressionParser(parameters, ordering, values);
            IEnumerable<DynamicOrdering> orderings = parser.ParseOrdering();
            Expression queryExpr = source.Expression;
            string methodAsc = "OrderBy";
            string methodDesc = "OrderByDescending";
            foreach (DynamicOrdering o in orderings)
            {
                if (!isDynamicNodeList)
                {
                    queryExpr = Expression.Call(
                        typeof(Queryable), o.Ascending ? methodAsc : methodDesc,
                        new Type[] { source.ElementType, o.Selector.Type },
                        queryExpr, Expression.Quote(Expression.Lambda(o.Selector, parameters)));
                }
                else
                {
                    //reroute each stacked Expression.Call into our own methods that know how to deal
                    //with DynamicNode
                    queryExpr = Expression.Call(
                            typeof(DynamicPublishedContentListOrdering),
                            o.Ascending ? methodAsc : methodDesc,
                            null,
                            queryExpr,
                            Expression.Quote(Expression.Lambda(o.Selector, parameters))
                        );
                }
                methodAsc = "ThenBy";
                methodDesc = "ThenByDescending";
            }
            if (isDynamicNodeList)
            {
                return typedSource.Provider.CreateQuery(queryExpr);
            }
            return source.Provider.CreateQuery(queryExpr);

        }
        private static object GetObjectAsTypeOrDefault(object value, Type type)
        {
            if (type.IsAssignableFrom(value.GetType()))
            {
                return (object)Convert.ChangeType(value, type);
            }
            else
            {
                if (type.IsValueType)
                {
                    return Activator.CreateInstance(type);
                }
                return null;
            }
        }
        private static object EvaluateDynamicNodeFunc(DynamicPublishedContent publishedContent, Func<DynamicPublishedContent, object> func)
        {
            object value = -1;
            var firstFuncResult = func(publishedContent);
            if (firstFuncResult is Func<DynamicPublishedContent, object>)
            {
                value = (firstFuncResult as Func<DynamicPublishedContent, object>)(publishedContent);
            }
            if (firstFuncResult.GetType().IsValueType || firstFuncResult is string)
            {
                value = firstFuncResult;
            }
            return value;
        }
        public static IQueryable Take(this IQueryable source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable), "Take",
                    new Type[] { source.ElementType },
                    source.Expression, Expression.Constant(count)));
        }

        public static IQueryable Skip(this IQueryable source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            return source.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable), "Skip",
                    new Type[] { source.ElementType },
                    source.Expression, Expression.Constant(count)));
        }

        public static IQueryable GroupBy(this IQueryable source, string keySelector, string elementSelector, params object[] values)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");
            if (elementSelector == null) throw new ArgumentNullException("elementSelector");
            LambdaExpression keyLambda = DynamicExpression.ParseLambda(source.ElementType, null, keySelector, true, values);
            LambdaExpression elementLambda = DynamicExpression.ParseLambda(source.ElementType, null, elementSelector, true, values);
            return source.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable), "GroupBy",
                    new Type[] { source.ElementType, keyLambda.Body.Type, elementLambda.Body.Type },
                    source.Expression, Expression.Quote(keyLambda), Expression.Quote(elementLambda)));
        }

        public static bool Any(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return (bool)source.Provider.Execute(
                Expression.Call(
                    typeof(Queryable), "Any",
                    new Type[] { source.ElementType }, source.Expression));
        }

        public static int Count(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException("source");
            return (int)source.Provider.Execute(
                Expression.Call(
                    typeof(Queryable), "Count",
                    new Type[] { source.ElementType }, source.Expression));
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using umbraco.interfaces;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web.Compilation;
using System.Linq.Expressions;
using System.Linq.Dynamic;
namespace umbraco.MacroEngines
{
    public class DynamicNodeList : DynamicObject, IEnumerable
    {
        public List<DynamicNode> Items { get; set; }

        public DynamicNodeList()
        {
            Items = new List<DynamicNode>();
        }
        public DynamicNodeList(IEnumerable<DynamicNode> items)
        {
            List<DynamicNode> list = items.ToList();
            list.ForEach(node => node.ownerList = this);
            Items = list;
        }
        public DynamicNodeList(IEnumerable<INode> items)
        {
            List<DynamicNode> list = items.Select(x => new DynamicNode(x)).ToList();
            list.ForEach(node => node.ownerList = this);
            Items = list;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var name = binder.Name;
            if (name == "Where")
            {
                string predicate = args.First().ToString();
                var values = args.Skip(1).ToArray();
                result = new DynamicNodeList(this.Where<DynamicNode>(predicate, values).ToList());
                return true;
            }
            if (name == "OrderBy")
            {
                result = new DynamicNodeList(this.OrderBy<DynamicNode>(args.First().ToString()).ToList());
                return true;
            }
            if (name == "Pluck" || name == "Select")
            {
                string predicate = args.First().ToString();
                var values = args.Skip(1).ToArray();
                var query = (IQueryable<object>)this.Select(predicate, values);
                object firstItem = query.FirstOrDefault();
                if (firstItem == null)
                {
                    result = new DynamicNull();
                }
                else
                {
                    var types = from i in query
                                group i by i.GetType() into g
                                orderby g.Count() descending
                                select new { g, Instances = g.Count() };
                    var dominantType = types.First().g.Key;
                    //remove items that are not the dominant type
                    //e.g. string,string,string,string,false[DynamicNull],string
                    var itemsOfDominantTypeOnly = query.ToList();
                    itemsOfDominantTypeOnly.RemoveAll(item => !item.GetType().IsAssignableFrom(dominantType));
                    if (dominantType == typeof(string))
                    {
                        result = (List<string>)itemsOfDominantTypeOnly.Cast<string>().ToList();
                    }
                    else if (dominantType == typeof(int))
                    {
                        result = (List<int>)itemsOfDominantTypeOnly.Cast<int>().ToList();
                    }
                    else if (dominantType == typeof(decimal))
                    {
                        result = (List<decimal>)itemsOfDominantTypeOnly.Cast<decimal>().ToList();
                    }
                    else if (dominantType == typeof(bool))
                    {
                        result = (List<bool>)itemsOfDominantTypeOnly.Cast<bool>().ToList();
                    }
                    else if (dominantType == typeof(DateTime))
                    {
                        result = (List<DateTime>)itemsOfDominantTypeOnly.Cast<DateTime>().ToList();
                    }
                    else
                    {
                        result = query.ToList();
                    }
                }
                return true;
            }
            try
            {
                //Property?
                result = Items.GetType().InvokeMember(binder.Name,
                                                  System.Reflection.BindingFlags.Instance |
                                                  System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.NonPublic |
                                                  System.Reflection.BindingFlags.GetProperty,
                                                  null,
                                                  Items,
                                                  args);
                return true;
            }
            catch (MissingMethodException)
            {
                try
                {
                    //Static or Instance Method?
                    result = Items.GetType().InvokeMember(binder.Name,
                                                  System.Reflection.BindingFlags.Instance |
                                                  System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.NonPublic |
                                                  System.Reflection.BindingFlags.Static |
                                                  System.Reflection.BindingFlags.InvokeMethod,
                                                  null,
                                                  Items,
                                                  args);
                    return true;
                }
                catch (MissingMethodException)
                {

                    try
                    {
                        result = ExecuteExtensionMethod(args, name, false);
                        return true;
                    }
                    catch (TargetInvocationException)
                    {
                        //We do this to enable error checking of Razor Syntax when a method e.g. ElementAt(2) is used.
                        //When the Script is tested, there's no Children which means ElementAt(2) is invalid (IndexOutOfRange)
                        //Instead, we are going to return an empty DynamicNode.
                        result = new DynamicNode();
                        return true;
                    }

                    catch
                    {
                        result = null;
                        return false;
                    }

                }


            }
            catch
            {
                result = null;
                return false;
            }

        }

        private object ExecuteExtensionMethod(object[] args, string name, bool argsContainsThis)
        {
            object result = null;

            MethodInfo methodToExecute = ExtensionMethodFinder.FindExtensionMethod(typeof(IEnumerable<DynamicNode>), args, name, false);
            if (methodToExecute == null)
            {
                methodToExecute = ExtensionMethodFinder.FindExtensionMethod(typeof(DynamicNodeList), args, name, false);
            }
            if (methodToExecute != null)
            {
                if (methodToExecute.GetParameters().First().ParameterType == typeof(DynamicNodeList))
                {
                    var genericArgs = (new[] { this }).Concat(args);
                    result = methodToExecute.Invoke(null, genericArgs.ToArray());
                }
                else
                {
                    var genericArgs = (new[] { Items }).Concat(args);
                    result = methodToExecute.Invoke(null, genericArgs.ToArray());
                }
            }
            else
            {
                throw new MissingMethodException();
            }
            if (result != null)
            {
                if (result is IEnumerable<INode>)
                {
                    result = new DynamicNodeList((IEnumerable<INode>)result);
                }
                if (result is IEnumerable<DynamicNode>)
                {
                    result = new DynamicNodeList((IEnumerable<DynamicNode>)result);
                }
                if (result is INode)
                {
                    result = new DynamicNode((INode)result);
                }
            }
            return result;
        }

        public IEnumerator GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        private IQueryable<T> Where<T>(string predicate, params object[] values)
        {
            return ((IQueryable<T>)Items.AsQueryable()).Where(predicate, values);
        }
        private IQueryable<T> OrderBy<T>(string key)
        {
            return ((IQueryable<T>)Items.AsQueryable()).OrderBy(key);
        }
        private IQueryable Select(string predicate, params object[] values)
        {
            return DynamicQueryable.Select(Items.AsQueryable(), predicate, values);
        }

        public void Add(DynamicNode node)
        {
            node.ownerList = this;
            this.Items.Add(node);
        }
        public void Remove(DynamicNode node)
        {
            if (this.Items.Contains(node))
            {
                node.ownerList = null;
                this.Items.Remove(node);
            }
        }
    }
}

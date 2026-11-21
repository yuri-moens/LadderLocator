using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LadderLocator
{
    public static class Cloner
    {
        /// <summary>
        /// Deep-clones an object graph via reflection so .NET 6+ types with private/abstract
        /// internal state (such as <see cref="Random"/>) clone cleanly without JSON serialization.
        /// </summary>
        public static T Clone<T>(T source)
        {
            if (ReferenceEquals(source, null))
                return default(T);

            var visited = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
            return (T)CloneObject(source, visited);
        }

        private static object CloneObject(object source, Dictionary<object, object> visited)
        {
            if (source == null)
                return null;

            Type type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
                return source;

            if (typeof(Delegate).IsAssignableFrom(type) || type.IsPointer || typeof(MemberInfo).IsAssignableFrom(type))
                return source;

            if (!type.IsValueType && visited.TryGetValue(source, out object existing))
                return existing;

            if (source is Array sourceArray)
            {
                Array clonedArray = (Array)sourceArray.Clone();
                visited[source] = clonedArray;

                Type elemType = type.GetElementType();
                if (elemType != null && !elemType.IsPrimitive && !elemType.IsEnum && elemType != typeof(string) && elemType != typeof(decimal))
                {
                    if (clonedArray.Rank == 1)
                    {
                        int length = clonedArray.Length;
                        int lower = clonedArray.GetLowerBound(0);
                        for (int i = lower; i < lower + length; i++)
                        {
                            object elem = clonedArray.GetValue(i);
                            if (elem != null)
                                clonedArray.SetValue(CloneObject(elem, visited), i);
                        }
                    }
                    else
                    {
                        int[] indices = new int[clonedArray.Rank];
                        CloneMultiDimArray(clonedArray, 0, indices, visited);
                    }
                }

                return clonedArray;
            }

            object clone = RuntimeHelpers.GetUninitializedObject(type);
            if (!type.IsValueType)
                visited[source] = clone;

            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    object value = field.GetValue(source);
                    if (value != null)
                        field.SetValue(clone, CloneObject(value, visited));
                }
            }

            return clone;
        }

        private static void CloneMultiDimArray(Array array, int dimension, int[] indices, Dictionary<object, object> visited)
        {
            int lower = array.GetLowerBound(dimension);
            int upper = array.GetUpperBound(dimension);
            for (int i = lower; i <= upper; i++)
            {
                indices[dimension] = i;
                if (dimension == array.Rank - 1)
                {
                    object elem = array.GetValue(indices);
                    if (elem != null)
                        array.SetValue(CloneObject(elem, visited), indices);
                }
                else
                {
                    CloneMultiDimArray(array, dimension + 1, indices, visited);
                }
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}

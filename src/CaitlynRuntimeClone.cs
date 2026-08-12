using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SodLolCaitlyn;

/// <summary>
/// Deep-clones managed runtime configuration graphs while deliberately keeping
/// UnityEngine.Object references shared. This is the behavior we want when a
/// Caitlyn proxy borrows vanilla animation/audio/ability assets but must own its
/// own mutable TriggerConfig/CastMethod/validator state.
/// </summary>
internal static class CaitlynRuntimeClone
{
    private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod(
        "MemberwiseClone",
        BindingFlags.Instance | BindingFlags.NonPublic);

    public static T DeepClone<T>(T source)
    {
        if (MemberwiseCloneMethod == null)
        {
            throw new MissingMethodException(typeof(object).FullName, "MemberwiseClone");
        }

        return (T)CloneObject(
            source,
            new Dictionary<object, object>(ReferenceComparer.Instance));
    }

    private static object CloneObject(
        object source,
        Dictionary<object, object> visited)
    {
        if (source == null)
        {
            return null;
        }

        Type type = source.GetType();
        if (IsImmutable(type) || source is UnityEngine.Object || source is Delegate || source is Type)
        {
            return source;
        }

        if (!type.IsValueType && visited.TryGetValue(source, out object existing))
        {
            return existing;
        }

        if (type.IsArray)
        {
            return CloneArray((Array)source, visited);
        }

        object clone;
        if (type.IsValueType)
        {
            clone = Activator.CreateInstance(type);
        }
        else
        {
            clone = MemberwiseCloneMethod.Invoke(source, null);
            visited[source] = clone;
        }

        CopyInstanceFields(source, clone, type, visited);
        return clone;
    }

    private static Array CloneArray(
        Array source,
        Dictionary<object, object> visited)
    {
        Type elementType = source.GetType().GetElementType();
        int rank = source.Rank;
        int[] lengths = new int[rank];
        int[] lowerBounds = new int[rank];
        for (int dimension = 0; dimension < rank; dimension++)
        {
            lengths[dimension] = source.GetLength(dimension);
            lowerBounds[dimension] = source.GetLowerBound(dimension);
        }

        Array clone = Array.CreateInstance(elementType, lengths, lowerBounds);
        visited[source] = clone;

        int[] indices = new int[rank];
        CopyArrayDimension(source, clone, visited, indices, dimension: 0);
        return clone;
    }

    private static void CopyArrayDimension(
        Array source,
        Array target,
        Dictionary<object, object> visited,
        int[] indices,
        int dimension)
    {
        int lower = source.GetLowerBound(dimension);
        int upper = source.GetUpperBound(dimension);
        for (int index = lower; index <= upper; index++)
        {
            indices[dimension] = index;
            if (dimension + 1 < source.Rank)
            {
                CopyArrayDimension(source, target, visited, indices, dimension + 1);
            }
            else
            {
                target.SetValue(CloneObject(source.GetValue(indices), visited), indices);
            }
        }
    }

    private static void CopyInstanceFields(
        object source,
        object target,
        Type type,
        Dictionary<object, object> visited)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo[] fields = current.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (field.IsStatic)
                {
                    continue;
                }

                object fieldValue = field.GetValue(source);
                object clonedValue = CloneObject(fieldValue, visited);

                try
                {
                    field.SetValue(target, clonedValue);
                }
                catch (Exception exception) when (
                    exception is FieldAccessException ||
                    exception is ArgumentException)
                {
                    if (IsSafeSharedValue(fieldValue))
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Could not isolate field {field.DeclaringType?.FullName}.{field.Name} while cloning {type.FullName}.",
                        exception);
                }
            }
        }
    }

    private static bool IsSafeSharedValue(object value)
    {
        if (value == null)
        {
            return true;
        }

        Type type = value.GetType();
        return IsImmutable(type) ||
               value is UnityEngine.Object ||
               value is Delegate ||
               value is Type;
    }

    private static bool IsImmutable(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid);
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}

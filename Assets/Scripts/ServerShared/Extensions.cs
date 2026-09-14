using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using MessagePack;
using CultMath;
using static CultMath.math;
using Random = CultMath.Random;
using Unity.Tiny;
using float2 = CultMath.float2;

public static class Extensions
{
    public static bool IsImplementationOf(this Type baseType, Type interfaceType)
    {
        return baseType.GetInterfaces().Any(interfaceType.Equals);
    }

    // Every loadable type assignable to this one. An assembly whose dependencies are missing (a Unity plugin
    // referencing UnityEngine outside Unity) still yields the types that do load.
    private static readonly Dictionary<Type, Type[]> ChildClasses = new Dictionary<Type, Type[]>();
    public static Type[] GetAllChildClasses(this Type type)
    {
        if (ChildClasses.TryGetValue(type, out var children)) return children;
        return ChildClasses[type] = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly =>
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }).Where(type.IsAssignableFrom).ToArray();
    }

    public static string SplitCamelCase(this string str) =>
        Regex.Replace(Regex.Replace(str, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");

    public static string FormatTypeName(this string typeName) =>
        (typeName.EndsWith("Data", StringComparison.InvariantCultureIgnoreCase)
            ? typeName.Substring(0, typeName.Length - 4)
            : typeName).SplitCamelCase();

    public static T MaxBy<T, U>(this IEnumerable<T> items, Func<T, U> selector) => items.Best(selector, 1);
    public static T MinBy<T, U>(this IEnumerable<T> items, Func<T, U> selector) => items.Best(selector, -1);

    private static T Best<T, U>(this IEnumerable<T> items, Func<T, U> selector, int sign)
    {
        using var e = items.GetEnumerator();
        if (!e.MoveNext()) throw new InvalidOperationException("Empty input sequence");
        var best = e.Current;
        var bestValue = selector(best);
        var comparer = Comparer<U>.Default;
        while (e.MoveNext())
        {
            var value = selector(e.Current);
            if (comparer.Compare(value, bestValue) * sign > 0)
            {
                best = e.Current;
                bestValue = value;
            }
        }
        return best;
    }

    public static T[] WeightedRandomElements<T>(this IEnumerable<T> collection, ref Random random, Func<T, float> weightFunction, int count)
    {
        var elements = collection as T[] ?? collection.ToArray();
        var weights = new Dictionary<T, float>(elements.Length);
        var totalWeight = 0f;
        foreach (var x in elements)
        {
            weights[x] = weightFunction(x);
            totalWeight += weights[x];
        }

        // Nothing can be drawn from a set whose weights all come to nothing. Returning an empty array says so,
        // where a full array of unset elements would read as "found these" and hand the caller nulls.
        if (totalWeight <= 0) return new T[0];

        var randomElements = new T[count];
        for (int i = 0; i < count; i++)
        {
            var targetWeight = random.NextFloat(totalWeight);
            var accumWeight = 0f;
            foreach (var x in elements)
            {
                accumWeight += weights[x];
                if (accumWeight > targetWeight)
                {
                    randomElements[i] = x;
                    break;
                }
            }
        }

        return randomElements;
    }
    
    // Thanks, https://stackoverflow.com/a/48599119
    public static bool ByteArrayCompare(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
    {
        return a1.SequenceEqual(a2);
    }

    public static bool ByteEquals(this byte[] a, byte[] b) => ByteArrayCompare(a, b);

    public static char Arrow(this ItemRotation rot)
    {
        switch (rot)
        {
            case ItemRotation.None:
                return '\u2191';
            case ItemRotation.Clockwise:
                return '\u2192';
            case ItemRotation.Reversed:
                return '\u2193';
            case ItemRotation.CounterClockwise:
                return '\u2190';
            default:
                throw new ArgumentOutOfRangeException(nameof(rot), rot, null);
        }
    }

    public static float2 Direction(this ItemRotation rotation)
    {
        switch (rotation)
        {
            case ItemRotation.None:
                return float2(0, 1);
            case ItemRotation.CounterClockwise:
                return float2(-1, 0);
            case ItemRotation.Reversed:
                return float2(0, -1);
            case ItemRotation.Clockwise:
                return float2(1, 0);
            default:
                throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null);
        }
    }

    public static float2 Rotate(this float2 v, ItemRotation rotation)
    {
        switch (rotation)
        {
            case ItemRotation.None:
                return v;
            case ItemRotation.CounterClockwise:
                return float2(-v.y, v.x);
            case ItemRotation.Reversed:
                return float2(-v.x, -v.y);
            case ItemRotation.Clockwise:
                return float2(v.y, -v.x);
            default:
                throw new ArgumentOutOfRangeException(nameof(rotation), rotation, null);
        }
    }

    private static Random? _random;
    //private static Random Random => (Random) (_random ??= new Random((uint) (DateTime.Now.Ticks%uint.MaxValue)));
    // public static T RandomElement<T>(this IEnumerable<T> enumerable) => enumerable.ElementAt(Random.NextInt(0, enumerable.Count()));
    public static float NextPowerDistribution(this ref Random random, float min, float max, float exp, float randexp) =>
        pow((pow(max, exp + 1) - pow(min, exp + 1)) * pow(random.NextFloat(), randexp) + pow(min, exp + 1), 1 / (exp + 1));
    // Box-Muller: two uniforms become a normal deviate. Used for part quality, where a manufacturer's mean is
    // its technology in a role and its deviation is quality control. Callers clamp to their own valid range.
    public static float NextGaussian(this ref Random random, float mean, float deviation)
    {
        var u1 = max(random.NextFloat(), 1e-6f);
        var u2 = random.NextFloat();
        return mean + deviation * sqrt(-2 * log(u1)) * cos(2 * PI * u2);
    }

    public static float NextUnbounded(this ref Random random) => 1 / (1 - random.NextFloat()) - 1;
    public static float NextUnbounded(this ref Random random, float bias, float power, float ceiling) => 1 / (1 - pow(min(random.NextFloat(), ceiling), 1 - pow(clamp(bias,0,.99f), 1 / power))) - 1;

    public static bool IsDefault<T>(this T value) where T : struct
    {
        bool isDefault = value.Equals(default(T));

        return isDefault;
    }
    
    public static bool IsNull<T, TU>(this KeyValuePair<T, TU> pair)
    {
        return pair.Equals(new KeyValuePair<T, TU>());
    }

    public static float Angle(this float2 from, float2 to)
    {
        var num = sqrt(lengthsq(from) * lengthsq(to));
        return num < 1.00000000362749E-15 ? 0.0f : acos(clamp(dot(from, to) / num, -1f, 1f)) * 57.29578f;
    }
}

// https://github.com/Burtsev-Alexey/net-object-deep-copy
public static class ObjectExtensions
{
    private static readonly MethodInfo CloneMethod = typeof(Object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

    public static bool IsPrimitive(this Type type)
    {
        if (type == typeof(String)) return true;
        return (type.IsValueType & type.IsPrimitive);
    }

    public static Object Copy(this Object originalObject)
    {
        return InternalCopy(originalObject, new Dictionary<Object, Object>(new ReferenceEqualityComparer()));
    }
    private static Object InternalCopy(Object originalObject, IDictionary<Object, Object> visited)
    {
        if (originalObject == null) return null;
        var typeToReflect = originalObject.GetType();
        if (IsPrimitive(typeToReflect)) return originalObject;
        if (visited.ContainsKey(originalObject)) return visited[originalObject];
        if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;
        var cloneObject = CloneMethod.Invoke(originalObject, null);
        if (typeToReflect.IsArray)
        {
            var arrayType = typeToReflect.GetElementType();
            if (IsPrimitive(arrayType) == false)
            {
                Array clonedArray = (Array)cloneObject;
                clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
            }

        }
        visited.Add(originalObject, cloneObject);
        CopyFields(originalObject, visited, cloneObject, typeToReflect);
        RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect);
        return cloneObject;
    }

    private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect)
    {
        if (typeToReflect.BaseType != null)
        {
            RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType);
            CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType, BindingFlags.Instance | BindingFlags.NonPublic, info => info.IsPrivate);
        }
    }

    private static void CopyFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy, Func<FieldInfo, bool> filter = null)
    {
        foreach (FieldInfo fieldInfo in typeToReflect.GetFields(bindingFlags))
        {
            if (filter != null && filter(fieldInfo) == false) continue;
            if (IsPrimitive(fieldInfo.FieldType)) continue;
            var originalFieldValue = fieldInfo.GetValue(originalObject);
            var clonedFieldValue = InternalCopy(originalFieldValue, visited);
            fieldInfo.SetValue(cloneObject, clonedFieldValue);
        }
    }
    public static T Copy<T>(this T original)
    {
        return (T)Copy((Object)original);
    }
}

public class ReferenceEqualityComparer : EqualityComparer<Object>
{
    public override bool Equals(object x, object y)
    {
        return ReferenceEquals(x, y);
    }
    public override int GetHashCode(object obj)
    {
        if (obj == null) return 0;
        return obj.GetHashCode();
    }
}
public static class ArrayExtensions
{
    public static void ForEach(this Array array, Action<Array, int[]> action)
    {
        if (array.LongLength == 0) return;
        ArrayTraverse walker = new ArrayTraverse(array);
        do action(array, walker.Position);
        while (walker.Step());
    }
}

internal class ArrayTraverse
{
    public int[] Position;
    private int[] maxLengths;

    public ArrayTraverse(Array array)
    {
        maxLengths = new int[array.Rank];
        for (int i = 0; i < array.Rank; ++i)
        {
            maxLengths[i] = array.GetLength(i) - 1;
        }
        Position = new int[array.Rank];
    }

    public bool Step()
    {
        for (int i = 0; i < Position.Length; ++i)
        {
            if (Position[i] < maxLengths[i])
            {
                Position[i]++;
                for (int j = 0; j < i; j++)
                {
                    Position[j] = 0;
                }
                return true;
            }
        }
        return false;
    }
}
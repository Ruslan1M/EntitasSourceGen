using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace EntitasSourceGen.Generator.Model;

/// <summary>ImmutableArray wrapper with structural equality, so incremental caching works.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array) => _array = array;

    public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    public int Length => AsImmutableArray().Length;
    public bool IsEmpty => Length == 0;
    public T this[int index] => AsImmutableArray()[index];

    public bool Equals(EquatableArray<T> other)
    {
        var a = AsImmutableArray();
        var b = other.AsImmutableArray();
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in AsImmutableArray())
            {
                hash = hash * 31 + (item is null ? 0 : item.GetHashCode());
            }
            return hash;
        }
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public ImmutableArray<T>.Enumerator GetEnumerator() => AsImmutableArray().GetEnumerator();
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)AsImmutableArray()).GetEnumerator();
}

internal static class EquatableArrayExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source) where T : IEquatable<T>
        => new(source.ToImmutableArray());
}

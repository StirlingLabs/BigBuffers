using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;

namespace StirlingLabs.Utilities
{
  [PublicAPI]
  [StructLayout(LayoutKind.Sequential)]
  public readonly unsafe struct SizedUtf8String
    : IEquatable<SizedUtf8String>,
      IComparable<SizedUtf8String>,
      IEquatable<Utf8String>,
      IComparable<Utf8String>
  {
    private readonly Utf8String _string;
    public readonly nuint Length;

    public sbyte* Pointer
    {
      [DebuggerStepThrough]
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _string.Pointer;
    }

    public SizedUtf8String(nuint length, Utf8String @string)
    {
      Length = length;
      _string = @string;
    }

    public SizedUtf8String(nuint length, sbyte* @string)
      : this(length, new Utf8String(@string)) { }

    public ref sbyte this[nint offset]
    {
      [System.Diagnostics.Contracts.Pure]
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref this[(nuint)offset];
    }
 
    public ref sbyte this[nuint offset]
    {
      [System.Diagnostics.Contracts.Pure]
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get {
        if (offset >= Length) throw new IndexOutOfRangeException();
        return ref Unsafe.AsRef<sbyte>(Pointer + offset);
      }
    }

    [DebuggerStepThrough]
    [System.Diagnostics.Contracts.Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(SizedUtf8String u)
      => new(u.Pointer, (int)u.Length);

    [DebuggerStepThrough]
    [System.Diagnostics.Contracts.Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<sbyte>(SizedUtf8String u)
      => new(u.Pointer, (int)u.Length);


    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.Contracts.Pure]
    public bool Equals(Utf8String other)
    {
      if (Pointer == other.Pointer)
        return true;

      return CompareTo(other) == 0;
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.Contracts.Pure]
    public int CompareTo(Utf8String other)
      => CompareTo((ReadOnlySpan<byte>)other);

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.Contracts.Pure]
    public bool Equals(SizedUtf8String other)
    {
      if (Pointer == other.Pointer)
        return Length == other.Length;

      return CompareTo(other) == 0;
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.Contracts.Pure]
    public bool Equals(ReadOnlySpan<byte> other)
    {
      var pOther = (sbyte*)Unsafe.AsPointer(ref Unsafe.AsRef(other.GetPinnableReference()));
      if (Pointer == pOther)
        return Length == (nuint)other.Length;

      return CompareTo(other) == 0;
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.Contracts.Pure]
    public int CompareTo(SizedUtf8String other)
      => CompareTo((ReadOnlySpan<byte>)other);

    public int CompareTo(ReadOnlySpan<byte> other)
    {
#if NET5_0_OR_GREATER
      var ownEnum = new Utf8RuneEnumerator(this);
      var otherEnum = new Utf8RuneEnumerator(other);
      for (;;)
      {
        var advancedOwn = ownEnum.MoveNext();
        var advancedOther = otherEnum.MoveNext();
        if (!advancedOwn)
          return !advancedOther
            ? 0 // same length
            : -1; // this is shorter

        if (!advancedOther)
          return 1; // this is longer 

        // compare runes
        var result = ownEnum.Current.CompareTo(otherEnum.Current);
        if (result != 0)
          return result; // difference in rune values
      }
#else
      return string.Compare(ToString(), other.ToString(), StringComparison.Ordinal);
#endif
    }

    public bool Free()
      => _string.Free();

    public static SizedUtf8String Create(string str)
      => new((nuint)Encoding.UTF8.GetByteCount(str), Utf8String.Create(str));


    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
      => Pointer == default
        ? null
        : _string.IsInterned
          // ReSharper disable once RedundantOverflowCheckingContext
          // ReSharper disable once ReplaceSubstringWithRangeIndexer
          ? _string.ToString().Substring(0, checked((int)Length))
          : ToNewString();

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ToNewString()
      // ReSharper disable once RedundantOverflowCheckingContext
      => new(Pointer, 0, checked((int)Length), Encoding.UTF8);

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SizedUtf8String Substring(nint offset, uint length)
      => new(length, Pointer + offset);

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SizedUtf8String Substring(nint offset, nuint length)
      => new(length, Pointer + offset);

    public static implicit operator SizedUtf8String(string s)
      => Create(s);
  }
}

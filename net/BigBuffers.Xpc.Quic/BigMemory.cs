using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using StirlingLabs.Native;
using StirlingLabs.Utilities;
using System_nuint = System.UIntPtr;

namespace BigBuffers.Xpc.Quic;

public sealed class BigMemory<T> : IDisposable where T : unmanaged
{
  public static unsafe nuint UnitSize => (nuint)sizeof(T);
  public unsafe BigMemory(nuint length = 0)
  {
    if (length > int.MaxValue)
      //_raw = new(NativeMemory.New<T>(length), length * UnitSize);
      throw new NotImplementedException();

    if (length > 0)
    {
      _owner = MemoryPool<T>.Shared.Rent((int)length);
      return;
    }

    _owner = null;
  }
  public unsafe BigMemory(Memory<T> external)
  {
    _owner = null;
    _slice = external;
  }

  public void Resize(nuint newLen)
  {
    if (_owner is null)
      throw new NotSupportedException("The memory is not owned and therefore will not be resized.");

    var curSize = Size;
    var curLen = Length;
    if (newLen > curLen)
    {
      if (newLen > curSize)
      {

        if (newLen > int.MaxValue)
        {
          // newLen > curLen
          // newLen > curSize
          // newLen > 2GB
          throw new NotImplementedException();
          /*
          var newRawLen = newLen * UnitSize;
          if (_owner is null) // curLen > 2GB
          {
            var old = _raw;
            var oldRawLen = _rawLength;
            _rawLength = newRawLen;
            _raw = new(NativeMemory.New<T>(newLen), newRawLen);
            ((BigSpan<T>)old).Slice(0, oldRawLen).CopyTo(_raw);
          }
          else
          {
            _rawLength = newRawLen;
            _raw = new(NativeMemory.New<T>(newLen), newRawLen);
            ((BigSpan<T>)_slice.Span).CopyTo(_raw);
            var owned = _owner;
            _owner = null;
            owned.Dispose();
            _slice = default;
          }
          */
        }
        else
        {
          if (curLen > int.MaxValue)
          {
            // newLen > curLen
            // newLen > curSize
            // newLen <= 2GB
            // curLen > 2GB
            throw new NotImplementedException();
          }
          else
          {
            // newLen > curLen
            // newLen > curSize
            // newLen <= 2GB
            // curLen <= 2GB
            var newLenInt = (int)newLen;
            var oldOwner = _owner;
            var oldSlice = _slice;
            _owner = MemoryPool<T>.Shared.Rent(newLenInt);
            _slice = _owner.Memory.Slice(0, newLenInt);
            if (oldOwner is null) return;
            oldSlice.CopyTo(_slice);
            oldOwner.Dispose();
          }
        }
      }
      else
      {
        if (curSize > int.MaxValue)
        {
          // newLen > curLen
          // newLen <= curSize
          // curSize > 2GB
          throw new NotImplementedException();
          /*
          _rawLength = newLen;
          */
        }
        else
        {
          if (newLen > int.MaxValue)
          {
            throw new NotImplementedException();
          }
          else
          {
            // newLen > curLen
            // newLen <= curSize
            // curSize <= 2GB
            // newLength <= 2GB
            var newLenInt = (int)newLen;
            // grow within curSize
            if (_owner is null)
              throw new NotImplementedException();
            Debug.Assert(_owner is not null);
            Debug.Assert(newLen < int.MaxValue);
            _slice = _owner.Memory!.Slice(0, newLenInt);
          }
        }
      }
    }
    else
    {
      if (newLen > curSize)
      {
        if (newLen > int.MaxValue)
        {
          // newLen <= curLen
          // newLen > curSize
          // newLen > 2GB
          throw new NotImplementedException();
        }
        else
        {
          // newLen <= curLen
          // newLen > curSize
          // newLen <= 2GB
          if (_owner is null)
            throw new NotImplementedException();
          var newLenInt = (int)newLen;
          _slice = _owner.Memory.Slice(0, newLenInt);
        }
      }
      else
      {

        if (newLen > int.MaxValue)
        {
          // newLen <= curLen
          // newLen <= curSize
          // newLen > 2GB
          throw new NotImplementedException();
        }
        else
        {
          // newLen <= curLen
          // newLen <= curSize
          // newLen <= 2GB
          if (_owner is null)
            throw new NotImplementedException();
          var newLenInt = (int)newLen;
          _slice = _owner.Memory.Slice(0, newLenInt);
        }
      }
    }
  }

  //public nuint ByteSize => _owner is not null ? (nuint)_owner.Memory.Length * UnitSize : _raw.ByteSize;
  public nuint ByteSize => (nuint)_owner!.Memory.Length * UnitSize;

  //public nuint Size => _owner is not null ? (nuint)_owner.Memory.Length : _raw.ByteSize / UnitSize;
  public nuint Size => (nuint)_owner!.Memory.Length;

  //public nuint Length => _owner is not null ? (nuint)_slice.Length : _rawLength;
  public nuint Length => (nuint)_slice.Length;

  private IMemoryOwner<T>? _owner;

  private Memory<T> _slice;

  //private RawMemory<T> _raw;

  //private nuint _rawLength = 0;

  //public BigSpan<T> BigSpan => _owner is not null ? _slice.Span : ((BigSpan<T>)_raw).Slice(0u, _rawLength);
  public BigSpan<T> BigSpan => _slice.Span;

  public ReadOnlyBigSpan<T> ReadOnlyBigSpan => BigSpan;

  public void Dispose() => _owner?.Dispose();

  public bool TryGetMemoryOwner(out IMemoryOwner<T>? owner)
  {
    owner = _owner;
    return true;
  }
  public bool TryGetMemory(out Memory<T> mem)
  {
    mem = _slice;
    return true;
  }

  public ref T GetPinnableReference()
    => ref BigSpan.GetPinnableReference();

  public void CopyFrom(ReadOnlySpan<byte> data)
  {
    var m = _slice = _owner!.Memory.Slice(0, data.Length);
    data.CopyTo(MemoryMarshal.AsBytes(m.Span));
  }
}

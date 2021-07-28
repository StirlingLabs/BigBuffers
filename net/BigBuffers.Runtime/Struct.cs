/*
 * Copyright 2014 Google Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace BigBuffers
{
  /// <summary>
  /// All structs in the generated code derive from this class, and add their own accessors.
  /// </summary>
  [PublicAPI]
  public readonly struct Struct : ISchemaModel, IEquatable<Struct>
  {
    private readonly ByteBufferResidentModel _byteBufferResidentModel;

    ref ByteBufferResidentModel ISchemaModel.ByteBufferOffset => ref _byteBufferResidentModel.UnsafeSelfReference();

    public ulong Offset
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _byteBufferResidentModel.Offset;
    }

    public ByteBuffer ByteBuffer
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _byteBufferResidentModel.ByteBuffer;
    }

    // Re-init the internal state with an external buffer {@code ByteBuffer} and an offset within.
    public Struct(ulong i, ByteBuffer byteBuffer) : this()
    {
      Debug.Assert(i <= long.MaxValue);
      _byteBufferResidentModel = new(byteBuffer, i);
    }

    public bool Equals(Struct other)
      => _byteBufferResidentModel.Equals(other._byteBufferResidentModel);

    public override bool Equals(object obj)
      => obj is Struct other && Equals(other);

    public override int GetHashCode()
      => _byteBufferResidentModel.GetHashCode();

    public static bool operator ==(Struct left, Struct right)
      => left.Equals(right);

    public static bool operator !=(Struct left, Struct right)
      => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ref Struct UnsafeSelfReference()
      => ref Unsafe.AsRef<Struct>(Unsafe.AsPointer(ref Unsafe.AsRef(this)));
  }
}

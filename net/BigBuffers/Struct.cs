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
  public struct Struct : ISchemaModel
  {
    private ByteBufferResidentModel _byteBufferResidentModel;

    ref ByteBufferResidentModel ISchemaModel.ByteBufferOffset => ref _byteBufferResidentModel.UnsafeSelfReference();

    public ulong Offset
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _byteBufferResidentModel.Offset;
      private set => _byteBufferResidentModel.Offset = value;
    }

    public ByteBuffer ByteBuffer
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _byteBufferResidentModel.ByteBuffer;
      private set => _byteBufferResidentModel.ByteBuffer = value;
    }

    // Re-init the internal state with an external buffer {@code ByteBuffer} and an offset within.
    public Struct(ulong i, ByteBuffer byteBuffer) : this()
    {
      Debug.Assert(i <= long.MaxValue);
      ByteBuffer = byteBuffer;
      Offset = i;
    }
    
  }
}

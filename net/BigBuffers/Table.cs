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
using System.Text;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

// @formatter:off
#if NETSTANDARD
using nuint = System.UIntPtr;
using nint = System.IntPtr;
#endif
// @formatter:on

namespace BigBuffers
{
  /// <summary>
  /// All tables in the generated code derive from this struct, and add their own accessors.
  /// </summary>
  [PublicAPI]
  public struct Table : ISchemaModel
  {
    private ByteBufferResidentModel _byteBufferResidentModel;

    ref ByteBufferResidentModel ISchemaModel.ByteBufferOffset => ref _byteBufferResidentModel.UnsafeSelfReference();

    public ulong Offset
    {
      get => _byteBufferResidentModel.Offset;
      private set => _byteBufferResidentModel.Offset = value;
    }

    public ByteBuffer ByteBuffer { get => _byteBufferResidentModel.ByteBuffer; private set => _byteBufferResidentModel.ByteBuffer = value; }

    // Re-init the internal state with an external buffer {@code ByteBuffer} and an offset within.
    public Table(ulong i, ByteBuffer byteBuffer) : this()
    {
      Debug.Assert(i <= long.MaxValue);
      ByteBuffer = byteBuffer;
      Offset = i;
    }

  }
}

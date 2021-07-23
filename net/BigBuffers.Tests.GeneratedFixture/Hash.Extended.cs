using System;
using System.IO;
using System.Security.Cryptography;
using StirlingLabs.Utilities;

namespace Generated
{
  public partial struct Hash
  {
    public static SHA256 hasher = SHA256.Create();
    
    public unsafe void Fill(ReadOnlyBigSpan<byte> data)
    {
      fixed (byte* pData = data)
      {
        using var ums = new UnmanagedMemoryStream(pData, (long)data.LongLength);
        hasher.ComputeHash(ums).CopyTo(Bytes());
      }
    }
  }
  
}

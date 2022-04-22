using System;
using System.Runtime.CompilerServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{

  public static class SchemaModel2
  {
    public static TResult __union<TResult>(ref this Model model, ulong offset)
      where TResult : class
    {
      if (!Type<TResult>.Is<string>())
        throw new NotImplementedException();

      return model.__string(offset) as TResult;
    }
  }
}

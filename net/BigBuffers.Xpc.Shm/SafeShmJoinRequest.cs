using System;
using System.Threading.Tasks;

namespace BigBuffers.Xpc.Shm {

  public readonly struct SafeShmJoinRequest {

    public readonly Func<SafeShmMessageBlock, bool> Inspector;

    public readonly TaskCompletionSource<SafeShmMessageBlock> Completion;

    public SafeShmJoinRequest(Func<SafeShmMessageBlock, bool> inspector, TaskCompletionSource<SafeShmMessageBlock> completion) {
      Inspector = inspector;
      Completion = completion;
    }

  }

}

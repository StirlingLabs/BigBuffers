namespace NngNative
{
  using static LibNng;
  public enum NngPipeEv
  {
    AddPre = NNG_PIPE_EV_ADD_PRE,
    AddPost = NNG_PIPE_EV_ADD_POST,
    RemPost = NNG_PIPE_EV_REM_POST,
  }
}

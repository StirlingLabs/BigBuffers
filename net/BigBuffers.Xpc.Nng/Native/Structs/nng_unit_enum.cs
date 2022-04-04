namespace NngNative
{
  public enum nng_unit_enum
  {
    NNG_UNIT_NONE = 0, // No special units
    NNG_UNIT_BYTES = 1, // Bytes, e.g. bytes sent, etc.
    NNG_UNIT_MESSAGES = 2, // Messages, one per message
    NNG_UNIT_MILLIS = 3, // Milliseconds
    NNG_UNIT_EVENTS = 4  // Some other type of event
  };
}
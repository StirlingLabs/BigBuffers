namespace NngNative
{
  public enum nng_stat_type_enum
  {
    NNG_STAT_SCOPE = 0, // Stat is for scoping, and carries no value
    NNG_STAT_LEVEL = 1, // Numeric "absolute" value, diffs meaningless
    NNG_STAT_COUNTER = 2, // Incrementing value (diffs are meaningful)
    NNG_STAT_STRING = 3, // Value is a string
    NNG_STAT_BOOLEAN = 4, // Value is a boolean
    NNG_STAT_ID = 5, // Value is a numeric ID
  };
}
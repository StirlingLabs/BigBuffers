namespace BigBuffers.Xpc.Shm {

  public struct SafeShmShelfHeader {

    public const uint AllocationSize = 524288;

    public const ushort Capacity = 65534;

    public const ushort Pages = 128;

    public const uint MagicValue = 0x314D6853; // 'ShM1'

    public uint Magic; // versioned protocol unique

    public uint ConnectionKey; // connection identifying nonce

    public uint Generation; // version counter

    public ushort Count;

    public void Initialize(uint connectionKey) {
      Magic = MagicValue;
      ConnectionKey = connectionKey;
      Generation = 0;
      Count = 0;
    }

  }

}

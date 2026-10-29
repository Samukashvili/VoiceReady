namespace VoiceReady.Core.Memory.Unreal;

/// <summary>
/// Engine-internal struct offsets for Unreal Engine 5.4+ shipping builds (packed FFieldVariant).
/// These only change on an engine upgrade, not on regular game updates.
/// </summary>
public sealed class UnrealLayout
{
    public int NamePoolBlocks { get; init; } = 0x10;

    public int NameStride { get; init; } = 2;

    public int ObjectClass { get; init; } = 0x10;

    public int ObjectName { get; init; } = 0x18;

    public int StructSuper { get; init; } = 0x40;

    public int StructChildProperties { get; init; } = 0x50;

    public int StructPropertiesSize { get; init; } = 0x58;

    public int FieldClass { get; init; } = 0x08;

    public int FieldNext { get; init; } = 0x18;

    public int FieldName { get; init; } = 0x20;

    public int PropertyElementSize { get; init; } = 0x34;

    public int PropertyOffset { get; init; } = 0x44;

    public int BoolPropertyFieldSize { get; init; } = 0x70;

    public int BytePropertyEnum { get; init; } = 0x70;

    public int StructPropertyStruct { get; init; } = 0x70;

    public int EnumPropertyEnum { get; init; } = 0x78;

    public int ArrayInner { get; init; } = 0x78;

    public int EnumNames { get; init; } = 0x40;
}

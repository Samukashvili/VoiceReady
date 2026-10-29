using System.Text;

namespace VoiceReady.Core.Memory.Unreal;

/// <summary>
/// Read-only access to Unreal Engine reflection data (FNamePool, UObject classes, FProperty chains).
/// Field offsets are looked up by name at runtime, so they keep working across game updates and on every player's machine.
/// </summary>
public sealed class UnrealReflection
{
    private readonly ProcessMemoryReader _reader;
    private readonly UnrealLayout _layout;
    private readonly IntPtr _namePool;
    private readonly Dictionary<uint, string?> _names = [];
    private readonly Dictionary<(IntPtr Struct, string Name), UnrealField?> _fields = [];
    private readonly Dictionary<IntPtr, IReadOnlyDictionary<long, string>> _enums = [];

    private UnrealReflection(ProcessMemoryReader reader, UnrealLayout layout, IntPtr namePool, IntPtr gWorld)
    {
        _reader = reader;
        _layout = layout;
        _namePool = namePool;
        GWorld = gWorld;
    }

    /// <summary>Address of the global UWorld* variable.</summary>
    public IntPtr GWorld { get; }

    public static UnrealReflection Create(
        ProcessMemoryReader reader,
        IReadOnlyList<PointerRootSignature> namePoolSignatures,
        IReadOnlyList<PointerRootSignature> gWorldSignatures,
        UnrealLayout layout)
    {
        var moduleName = reader.MainModuleName;
        var namePool = ResolveFirst(reader, moduleName, namePoolSignatures, "FNamePool");
        var gWorld = ResolveFirst(reader, moduleName, gWorldSignatures, "GWorld");
        var reflection = new UnrealReflection(reader, layout, namePool, gWorld);

        if (reflection.GetName(0) != "None")
        {
            throw new InvalidOperationException("FNamePool signature resolved, but name 0 is not \"None\".");
        }

        var world = reflection.ReadPointer(gWorld);
        var worldClass = world == IntPtr.Zero ? null : reflection.GetClassName(world);
        if (worldClass is not null && worldClass != "World")
        {
            throw new InvalidOperationException($"GWorld signature resolved to a {worldClass}, not a World.");
        }

        return reflection;
    }

    private static IntPtr ResolveFirst(
        ProcessMemoryReader reader,
        string moduleName,
        IReadOnlyList<PointerRootSignature> signatures,
        string label)
    {
        var errors = new List<string>();
        foreach (var signature in signatures)
        {
            try
            {
                return reader.ResolveSignature(moduleName, signature);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException(
            $"No {label} signature matched ({string.Join("; ", errors.DefaultIfEmpty("none configured"))}).");
    }

    public IntPtr ReadPointer(IntPtr address)
    {
        var buffer = new byte[8];
        return _reader.TryReadBytes(address, buffer) ? new IntPtr(BitConverter.ToInt64(buffer)) : IntPtr.Zero;
    }

    public bool TryReadBytes(IntPtr address, byte[] destination) => _reader.TryReadBytes(address, destination);

    public int? ReadInt32(IntPtr address)
    {
        var buffer = new byte[4];
        return _reader.TryReadBytes(address, buffer) ? BitConverter.ToInt32(buffer) : null;
    }

    public string? GetName(uint comparisonIndex)
    {
        if (_names.TryGetValue(comparisonIndex, out var cached))
        {
            return cached;
        }

        var block = comparisonIndex >> 16;
        var offset = (comparisonIndex & 0xFFFF) * _layout.NameStride;
        var blockAddress = ReadPointer(IntPtr.Add(_namePool, checked((int)(_layout.NamePoolBlocks + block * 8))));
        string? name = null;
        var header = new byte[2];
        if (blockAddress != IntPtr.Zero && _reader.TryReadBytes(IntPtr.Add(blockAddress, (int)offset), header))
        {
            var value = BitConverter.ToUInt16(header);
            var isWide = (value & 1) != 0;
            var length = value >> 6;
            if (length is > 0 and <= 1024)
            {
                var raw = new byte[isWide ? length * 2 : length];
                if (_reader.TryReadBytes(IntPtr.Add(blockAddress, (int)offset + 2), raw))
                {
                    name = isWide ? Encoding.Unicode.GetString(raw) : Encoding.Latin1.GetString(raw);
                }
            }
        }

        _names[comparisonIndex] = name;
        return name;
    }

    public string? GetObjectName(IntPtr uObject)
    {
        var index = ReadInt32(IntPtr.Add(uObject, _layout.ObjectName));
        return index is null ? null : GetName((uint)index.Value);
    }

    public IntPtr GetClass(IntPtr uObject) => ReadPointer(IntPtr.Add(uObject, _layout.ObjectClass));

    public string? GetClassName(IntPtr uObject)
    {
        var uClass = GetClass(uObject);
        return uClass == IntPtr.Zero ? null : GetObjectName(uClass);
    }

    /// <summary>Finds a reflected property by name on a UStruct/UClass or any of its super structs.</summary>
    public UnrealField? FindField(IntPtr uStruct, string name)
    {
        if (_fields.TryGetValue((uStruct, name), out var cached))
        {
            return cached;
        }

        UnrealField? found = null;
        for (var current = uStruct; current != IntPtr.Zero && found is null; current = ReadPointer(IntPtr.Add(current, _layout.StructSuper)))
        {
            var field = ReadPointer(IntPtr.Add(current, _layout.StructChildProperties));
            for (var guard = 0; field != IntPtr.Zero && guard < 4096; guard++)
            {
                var nameIndex = ReadInt32(IntPtr.Add(field, _layout.FieldName));
                if (nameIndex is not null && GetName((uint)nameIndex.Value) == name)
                {
                    found = ReadField(field, name);
                    break;
                }

                field = ReadPointer(IntPtr.Add(field, _layout.FieldNext));
            }
        }

        _fields[(uStruct, name)] = found;
        return found;
    }

    private UnrealField ReadField(IntPtr field, string name)
    {
        var fieldClass = ReadPointer(IntPtr.Add(field, _layout.FieldClass));
        var typeIndex = fieldClass == IntPtr.Zero ? null : ReadInt32(fieldClass);
        var typeName = typeIndex is null ? string.Empty : GetName((uint)typeIndex.Value) ?? string.Empty;
        var offset = ReadInt32(IntPtr.Add(field, _layout.PropertyOffset)) ?? -1;
        var elementSize = ReadInt32(IntPtr.Add(field, _layout.PropertyElementSize)) ?? 0;
        return new UnrealField(field, name, typeName, offset, elementSize);
    }

    public IntPtr GetArrayInner(UnrealField arrayField) => ReadPointer(IntPtr.Add(arrayField.Address, _layout.ArrayInner));

    public IntPtr GetStructOf(IntPtr structProperty) => ReadPointer(IntPtr.Add(structProperty, _layout.StructPropertyStruct));

    public UnrealField DescribeField(IntPtr field) =>
        ReadField(field, GetName((uint)(ReadInt32(IntPtr.Add(field, _layout.FieldName)) ?? 0)) ?? string.Empty);

    public int GetStructSize(IntPtr uStruct) => ReadInt32(IntPtr.Add(uStruct, _layout.StructPropertiesSize)) ?? 0;

    /// <summary>Returns value -> short name (e.g. "SC_StackUp") for an EnumProperty or enum-backed ByteProperty.</summary>
    public IReadOnlyDictionary<long, string> GetEnumNames(UnrealField enumField)
    {
        var uEnum = ReadPointer(IntPtr.Add(enumField.Address, enumField.TypeName == "ByteProperty" ? _layout.BytePropertyEnum : _layout.EnumPropertyEnum));
        if (uEnum == IntPtr.Zero)
        {
            return new Dictionary<long, string>();
        }

        if (_enums.TryGetValue(uEnum, out var cached))
        {
            return cached;
        }

        var names = new Dictionary<long, string>();
        var data = ReadPointer(IntPtr.Add(uEnum, _layout.EnumNames));
        var count = ReadInt32(IntPtr.Add(uEnum, _layout.EnumNames + 8)) ?? 0;
        var entry = new byte[16];
        for (var index = 0; index < Math.Min(count, 4096); index++)
        {
            if (!_reader.TryReadBytes(IntPtr.Add(data, index * 16), entry))
            {
                break;
            }

            var name = GetName(BitConverter.ToUInt32(entry, 0));
            if (name is not null)
            {
                var separator = name.LastIndexOf("::", StringComparison.Ordinal);
                names[BitConverter.ToInt64(entry, 8)] = separator < 0 ? name : name[(separator + 2)..];
            }
        }

        _enums[uEnum] = names;
        return names;
    }

    /// <summary>
    /// Follows a dotted property path such as "OwningGameInstance.LocalPlayers[0].PlayerController" from a UObject.
    /// Every segment except the last must be an object pointer (or an array of object pointers when indexed).
    /// Returns the address of the last property's value and its field description.
    /// </summary>
    public ReflectedValue ResolvePath(IntPtr rootObject, string path)
    {
        var current = rootObject;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            if (current == IntPtr.Zero)
            {
                return ReflectedValue.Missing($"null object before '{segments[index]}'");
            }

            var (name, arrayIndex) = ParseSegment(segments[index]);
            var uClass = GetClass(current);
            var field = uClass == IntPtr.Zero ? null : FindField(uClass, name);
            if (field is null)
            {
                return ReflectedValue.Broken($"field '{name}' not found on {GetClassName(current) ?? "unknown class"}");
            }

            var valueAddress = IntPtr.Add(current, field.Offset);
            if (arrayIndex is not null)
            {
                var count = ReadInt32(IntPtr.Add(valueAddress, 8)) ?? 0;
                if (arrayIndex.Value >= count)
                {
                    return ReflectedValue.Missing($"'{name}' has {count} elements");
                }

                valueAddress = IntPtr.Add(ReadPointer(valueAddress), arrayIndex.Value * 8);
            }

            if (index == segments.Length - 1)
            {
                return new ReflectedValue(valueAddress, field, null, false);
            }

            current = ReadPointer(valueAddress);
        }

        return ReflectedValue.Broken("empty path");
    }

    private static (string Name, int? Index) ParseSegment(string segment)
    {
        var bracket = segment.IndexOf('[');
        if (bracket < 0 || !segment.EndsWith(']'))
        {
            return (segment, null);
        }

        return (segment[..bracket], int.Parse(segment[(bracket + 1)..^1]));
    }

    public bool? ReadBool(ReflectedValue value)
    {
        var buffer = new byte[4];
        if (!_reader.TryReadBytes(IntPtr.Add(value.Field!.Address, _layout.BoolPropertyFieldSize), buffer))
        {
            return null;
        }

        var byteOffset = buffer[1];
        var fieldMask = buffer[3];
        var data = new byte[1];
        return _reader.TryReadBytes(IntPtr.Add(value.Address, byteOffset), data) ? (data[0] & fieldMask) != 0 : null;
    }
}

public sealed record UnrealField(IntPtr Address, string Name, string TypeName, int Offset, int ElementSize);

public sealed record ReflectedValue(IntPtr Address, UnrealField? Field, string? Error, bool IsBroken)
{
    public bool Found => Field is not null;

    /// <summary>The path is valid but an object along it is not present right now (menus, loading screens).</summary>
    public static ReflectedValue Missing(string error) => new(IntPtr.Zero, null, error, false);

    /// <summary>The path no longer matches the game's classes; callers should fall back.</summary>
    public static ReflectedValue Broken(string error) => new(IntPtr.Zero, null, error, true);
}

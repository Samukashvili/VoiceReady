using System.Text.RegularExpressions;
using VoiceReady.Core.Configuration;
using VoiceReady.Core.Memory;
using VoiceReady.Core.Memory.Unreal;

namespace VoiceReady.Core.Detection;

/// <summary>
/// Reads the command-menu state and selected team by walking Unreal reflection data by name
/// (GWorld -> ... -> SwatCommandWidget), instead of relying on hardcoded pointer offsets.
/// Results use the same state values as the legacy pointer map so the rest of the app is unchanged.
/// </summary>
public sealed class CommandMenuReflectionReader
{
    private const string ClosedStateName = "GameplayNoMenu";
    private const string EscapeMenuStateName = "EscapeMenu";

    private readonly UnrealReflection _reflection;
    private readonly ReflectionMap _map;
    private readonly int? _closedValue;
    private readonly int? _escapeMenuValue;
    private readonly IReadOnlyList<PageRule> _rules;
    private CommandPageLayout? _pageLayout;

    private CommandMenuReflectionReader(UnrealReflection reflection, ReflectionMap map, IEnumerable<KnownMenuState> knownStates)
    {
        _reflection = reflection;
        _map = map;
        var states = knownStates.ToArray();
        _closedValue = states.FirstOrDefault(state => state.Name == ClosedStateName)?.Value;
        _escapeMenuValue = states.FirstOrDefault(state => state.Name == EscapeMenuStateName)?.Value;
        _rules = states
            .SelectMany(state => state.CommandPages.Select(page => new PageRule(state.Name, state.Value, state.Submenu, page.Select(CompileToken).ToArray())))
            .ToArray();
    }

    /// <summary>The command page seen on the last menu read, for diagnostics (e.g. "StackUp,None,None,None,Cover").</summary>
    public string LastPage { get; private set; } = string.Empty;

    public static CommandMenuReflectionReader Create(ProcessMemoryReader reader, ReflectionMap map, IEnumerable<KnownMenuState> knownStates)
    {
        var reflection = UnrealReflection.Create(
            reader,
            map.NamePoolSignatures.Select(signature => signature.ToSignature()).ToArray(),
            map.GWorldSignatures.Select(signature => signature.ToSignature()).ToArray(),
            map.Layout);
        return new CommandMenuReflectionReader(reflection, map, knownStates);
    }

    public DirectStateRead ReadMenuState()
    {
        var world = _reflection.ReadPointer(_reflection.GWorld);
        if (world == IntPtr.Zero)
        {
            return DirectStateRead.NoValue("no world loaded");
        }

        if (!string.IsNullOrWhiteSpace(_map.EscapeMenuOpenPath) && _escapeMenuValue.HasValue)
        {
            var escapeMenu = _reflection.ResolvePath(world, _map.EscapeMenuOpenPath);
            if (escapeMenu.Found && _reflection.ReadBool(escapeMenu) == true)
            {
                return DirectStateRead.WithValue(_escapeMenuValue.Value, EscapeMenuStateName);
            }
        }

        var menuOpen = _reflection.ResolvePath(world, _map.MenuOpenPath);
        if (!menuOpen.Found)
        {
            return menuOpen.IsBroken ? DirectStateRead.Unavailable(menuOpen.Error!) : DirectStateRead.NoValue(menuOpen.Error!);
        }

        var isOpen = _reflection.ReadBool(menuOpen);
        if (isOpen is null)
        {
            return DirectStateRead.NoValue("could not read menu-open flag");
        }

        if (isOpen == false)
        {
            return _closedValue.HasValue
                ? DirectStateRead.WithValue(_closedValue.Value, ClosedStateName)
                : DirectStateRead.NoValue("menu closed");
        }

        var page = _reflection.ResolvePath(world, _map.CommandPagePath);
        if (!page.Found)
        {
            return page.IsBroken ? DirectStateRead.Unavailable(page.Error!) : DirectStateRead.NoValue(page.Error!);
        }

        var commands = ReadCommandPage(page);
        if (commands is null)
        {
            return DirectStateRead.Unavailable($"could not read '{_map.CommandTypeField}' entries of the command page");
        }

        var isSubmenu = IsSubmenu(world);
        LastPage = string.Join(",", commands) + (isSubmenu ? " (submenu)" : string.Empty);
        var match = _rules.FirstOrDefault(rule => rule.Matches(commands, isSubmenu));
        return match is null
            ? DirectStateRead.NoValue($"unmapped page {LastPage}")
            : DirectStateRead.WithValue(match.Value, $"{match.StateName} [{LastPage}]");
    }

    public DirectStateRead ReadTeamSelection()
    {
        var world = _reflection.ReadPointer(_reflection.GWorld);
        if (world == IntPtr.Zero)
        {
            return DirectStateRead.NoValue("no world loaded");
        }

        var team = _reflection.ResolvePath(world, _map.TeamSelectionPath);
        if (!team.Found)
        {
            return team.IsBroken ? DirectStateRead.Unavailable(team.Error!) : DirectStateRead.NoValue(team.Error!);
        }

        var buffer = new byte[Math.Clamp(team.Field!.ElementSize, 1, 4)];
        if (!_reflection.TryReadBytes(team.Address, buffer))
        {
            return DirectStateRead.NoValue("could not read team selection");
        }

        var value = buffer.Length == 1 ? buffer[0] : buffer.Length == 2 ? BitConverter.ToInt16(buffer) : BitConverter.ToInt32(buffer);
        return DirectStateRead.WithValue(value, team.Field.Name);
    }

    private bool IsSubmenu(IntPtr world)
    {
        if (string.IsNullOrWhiteSpace(_map.ParentCommandsPath))
        {
            return false;
        }

        var parents = _reflection.ResolvePath(world, _map.ParentCommandsPath);
        return parents.Found && (_reflection.ReadInt32(IntPtr.Add(parents.Address, 8)) ?? 0) > 0;
    }

    private string[]? ReadCommandPage(ReflectedValue page)
    {
        var layout = GetPageLayout(page.Field!);
        if (layout is null)
        {
            return null;
        }

        var count = _reflection.ReadInt32(IntPtr.Add(page.Address, 8)) ?? 0;
        if (count is < 0 or > 64)
        {
            return null;
        }

        var data = new byte[count * layout.ElementSize];
        if (count > 0 && !_reflection.TryReadBytes(_reflection.ReadPointer(page.Address), data))
        {
            return null;
        }

        var commands = new string[count];
        for (var index = 0; index < count; index++)
        {
            var value = data[index * layout.ElementSize + layout.CommandTypeOffset];
            commands[index] = layout.Names.TryGetValue(value, out var name) ? StripPrefix(name) : value.ToString();
        }

        return commands;
    }

    private CommandPageLayout? GetPageLayout(UnrealField pageField)
    {
        if (_pageLayout?.ArrayField == pageField.Address)
        {
            return _pageLayout;
        }

        var inner = _reflection.GetArrayInner(pageField);
        var elementStruct = inner == IntPtr.Zero ? IntPtr.Zero : _reflection.GetStructOf(inner);
        var commandType = elementStruct == IntPtr.Zero ? null : _reflection.FindField(elementStruct, _map.CommandTypeField);
        var elementSize = elementStruct == IntPtr.Zero ? 0 : _reflection.GetStructSize(elementStruct);
        if (commandType is null || elementSize <= commandType.Offset)
        {
            return null;
        }

        _pageLayout = new CommandPageLayout(pageField.Address, elementSize, commandType.Offset, _reflection.GetEnumNames(commandType));
        return _pageLayout;
    }

    private static string StripPrefix(string name) => name.StartsWith("SC_", StringComparison.Ordinal) ? name[3..] : name;

    private static Regex CompileToken(string token)
    {
        var alternatives = token.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(alternative => Regex.Escape(alternative).Replace(@"\*", ".*"));
        return new Regex($"^(?:{string.Join("|", alternatives)})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private sealed record CommandPageLayout(IntPtr ArrayField, int ElementSize, int CommandTypeOffset, IReadOnlyDictionary<long, string> Names);

    private sealed record PageRule(string StateName, int Value, bool? Submenu, Regex[] Tokens)
    {
        public bool Matches(string[] commands, bool isSubmenu)
        {
            if (commands.Length != Tokens.Length || (Submenu.HasValue && Submenu.Value != isSubmenu))
            {
                return false;
            }

            for (var index = 0; index < commands.Length; index++)
            {
                if (!Tokens[index].IsMatch(commands[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

/// <summary>
/// A state read from a direct (non-voting) source. <see cref="Available"/> is false when the source no longer
/// matches the game and the caller should fall back to legacy pointer paths.
/// </summary>
public sealed record DirectStateRead(bool Available, int? Value, string Detail)
{
    public static DirectStateRead WithValue(int value, string detail) => new(true, value, detail);

    public static DirectStateRead NoValue(string detail) => new(true, null, detail);

    public static DirectStateRead Unavailable(string detail) => new(false, null, detail);
}

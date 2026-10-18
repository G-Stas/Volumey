#nullable enable
namespace Volumey.Controls;

/// <summary>One cap of a hotkey box: a key's or a mouse button's name</summary>
public sealed record KeyCap(string Name, bool IsMouse);

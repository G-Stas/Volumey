#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using log4net;
using Volumey.Helper;

namespace Volumey.View.Converters;

public sealed partial class TrayIconConverter
{
	/// <summary>
	/// The glyphs of Windows' own volume icon, read from the file that holds them at the tray's size, so a set patched on this PC
	/// shows too. Windows draws them white and darkens them on a light taskbar: <see cref="ForLightTaskbar"/>.
	/// Each glyph is read once, also when it fails
	/// </summary>
	internal sealed class SystemIconProvider : TrayIconProvider
	{
		private const string ResourceFileName = "SndVolSSO.dll.mun";

		/// <summary>The resource ID of the icon group of each volume state in <see cref="ResourceFileName"/></summary>
		internal static readonly IReadOnlyDictionary<IconType, int> ResourceIds = new Dictionary<IconType, int>
		{
			[IconType.Mute] = 120,
			[IconType.Zero] = 121,
			[IconType.Low] = 122,
			[IconType.Mid] = 123,
			[IconType.High] = 124,
			[IconType.NoDevice] = 125
		};

		private static readonly ILog Log = LogManager.GetLogger(typeof(SystemIconProvider));

		/// <summary>The file with the icons: Windows keeps the resources of its system files in SystemResources</summary>
		internal static string GetResourceFilePath()
			=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemResources", ResourceFileName);

		/// <summary>
		/// The glyph for a light taskbar: a white glyph is inverted, as Windows does, and an icon with colors of its own (a
		/// customized set) is shown as it is
		/// </summary>
		internal static Icon? ForLightTaskbar(Icon? icon)
			=> icon != null && IconHelper.IsWhiteGlyph(icon) ? IconHelper.Inverted(icon) : icon;

		/// <summary>The glyph at <paramref name="size"/> x <paramref name="size"/>; null if the file or the icon isn't there (logged)</summary>
		internal static Icon? LoadFromSystem(IconType type, int size)
		{
			try
			{
				string file = GetResourceFilePath();
				//a negative index is a resource ID, not a position
				var icon = IconHelper.GetFromDllOfSize(file, -ResourceIds[type], size);
				if(icon == null)
					Log.Warn($"The system volume icon [{type}] isn't in [{file}]");
				return icon;
			}
			catch(Exception e)
			{
				Log.Error($"Failed to read the system volume icon [{type}]", e);
				return null;
			}
		}

		private readonly Dictionary<IconType, Lazy<Icon?>> icons;

		public SystemIconProvider() : this(type => LoadFromSystem(type, IconExtensions.GetTrayIconSize())) { }

		internal SystemIconProvider(Func<IconType, Icon?> load)
			=> icons = ResourceIds.Keys.ToDictionary(type => type, type => new Lazy<Icon?>(() => load(type)));

		protected override Icon? High => icons[IconType.High].Value;
		protected override Icon? Mid => icons[IconType.Mid].Value;
		protected override Icon? Low => icons[IconType.Low].Value;
		protected override Icon? Zero => icons[IconType.Zero].Value;
		protected override Icon? Mute => icons[IconType.Mute].Value;
		protected override Icon? NoDevice => icons[IconType.NoDevice].Value;
		protected override ILog Logger => Log;
	}
}

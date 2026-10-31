#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Volumey.Model;

namespace Volumey.DataProvider;

/// <summary>
/// Where Windows plays each app, and moving apps to other devices (<see cref="AppRouting"/>)
/// </summary>
public interface IAppRouting
{
	/// <inheritdoc cref="AppRouting.IsAvailable"/>
	bool IsAvailable { get; }

	/// <inheritdoc cref="AppRouting.MoveAsync"/>
	Task<MoveOutcome> MoveAsync(AudioProcessModel app, AppRoute to);

	/// <inheritdoc cref="AppRouting.RefreshAll"/>
	void RefreshAll();

	/// <inheritdoc cref="AppRouting.GetApps"/>
	IImmutableList<AudioProcessModel> GetApps();

	/// <inheritdoc cref="AppRouting.GetModels"/>
	IReadOnlyList<AudioProcessModel> GetModels(AudioProcessModel app);

	/// <inheritdoc cref="AppRouting.FindApp"/>
	AudioProcessModel? FindApp(uint processId);

	/// <inheritdoc cref="AppRouting.AppsChanged"/>
	event Action? AppsChanged;
}

namespace WafeControl.Core.ViewModels;

/// <summary>
/// How current the shown readings are. Worst first wins: no network, then no server, then the unit.
/// </summary>
public enum DataState
{
    /// <summary>
    /// Fresh data from an online unit (or nothing to judge yet).
    /// </summary>
    Live,

    /// <summary>
    /// The unit is online, but its last report is older than the stale threshold.
    /// </summary>
    Stale,

    /// <summary>
    /// The portal says the unit isn't connected.
    /// </summary>
    UnitOffline,

    /// <summary>
    /// Several refreshes in a row failed; the readings are the last known ones.
    /// </summary>
    ServerUnreachable,

    /// <summary>
    /// The device has no network.
    /// </summary>
    NoInternet,
}

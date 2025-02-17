﻿namespace SI.GameServer.Contract;

/// <summary>
/// Provides game server information.
/// </summary>
public sealed class HostInfo
{
    /// <summary>
    /// Server public name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Server hostname.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Port number for TCP-based connections.
    /// </summary>
    public int Port { get; set; }

    [System.Obsolete]
    public string PackagesPublicBaseUrl { get; set; }

    /// <summary>
    /// Base Urls that are considered valid for in-game content files.
    /// </summary>
    public string[] ContentPublicBaseUrls { get; set; }

    /// <summary>
    /// Server license text.
    /// </summary>
    public string License { get; set; }

    /// <summary>
    /// Maximum allowed package size in MB.
    /// </summary>
    public int MaxPackageSizeMb { get; set; } = 100;
}

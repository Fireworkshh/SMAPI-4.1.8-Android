using System;

namespace StardewModdingAPI.Events;

/// <summary>Events raised for multiplayer messages and connections.</summary>
public interface IMultiplayerEvents
{
    /// <summary>Raised after the mod context for a peer is received. This happens before the game approves the connection (<see cref="PeerConnected"/>), so the player doesn't yet exist in the game. This is the earliest point where messages can be sent to the peer via SMAPI.</summary>
    /// <param name="sender">The event sender. This isn't applicable to SMAPI events, and is always null.</param>
    /// <param name="e">The event data.</param>
    event EventHandler<PeerContextReceivedEventArgs> PeerContextReceived;

    /// <summary>Raised after a peer connection is approved by the game.</summary>
    /// <param name="sender">The event sender. This isn't applicable to SMAPI events, and is always null.</param>
    /// <param name="e">The event data.</param>
    event EventHandler<PeerConnectedEventArgs> PeerConnected;

    /// <summary>Raised after a mod message is received over the network.</summary>
    /// <param name="sender">The event sender. This isn't applicable to SMAPI events, and is always null.</param>
    /// <param name="e">The event data.</param>
    event EventHandler<ModMessageReceivedEventArgs> ModMessageReceived;

    /// <summary>Raised after the connection with a peer is severed.</summary>
    /// <param name="sender">The event sender. This isn't applicable to SMAPI events, and is always null.</param>
    /// <param name="e">The event data.</param>
    event EventHandler<PeerDisconnectedEventArgs> PeerDisconnected;
}

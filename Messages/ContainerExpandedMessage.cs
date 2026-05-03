namespace SquareClickerPointer.Messages;

// ═══════════════════════════════════════════════════════════════════════════════
//  ContainerExpandedMessage  —  broadcast when a container transitions to open
// ═══════════════════════════════════════════════════════════════════════════════
//
//  ACCORDION BEHAVIOR — why this message exists
//  ─────────────────────────────────────────────
//  When Container 2 expands, it publishes this message.  Every OTHER container that
//  is also subscribed receives it and collapses — giving the familiar accordion
//  pattern where only one panel is open at a time.
//
//  The key word is "every OTHER."  Each container checks:
//
//      if (message.ContainerId != myContainerId) IsExpanded = false;
//
//  Without an ID, a container cannot tell whether it published the message itself
//  or whether a sibling did.  Without that distinction it would immediately
//  collapse itself after expanding, creating an impossible-to-open accordion.
//
//  LOOSE COUPLING WIN
//  ───────────────────
//  Adding a 4th container:  it just subscribes to this message — zero changes to
//  existing containers.  Removing a container: zero changes to remaining ones.
//  None of the containers hold a reference to any other container.  They only share
//  knowledge of this message type, which is the entire point of pub/sub.
//
//  WHY a string ID rather than passing 'this' (a reference to the sender)?
//  ─────────────────────────────────────────────────────────────────────────
//  Passing the sender object would give subscribers a direct reference back to the
//  publisher, re-introducing the tight coupling pub/sub was designed to break.
//  A string ID carries just enough information for the comparison; nothing more.

/// <summary>
/// Published by <see cref="ViewModels.ExpandableContainerViewModel.ToggleExpandCommand"/>
/// when a container transitions from collapsed → expanded.
/// All OTHER subscribed containers receive this and collapse themselves.
/// </summary>
/// <param name="ContainerId">
/// The unique ID of the container that just opened.
/// Receiving containers skip collapsing if this matches their own ID.
/// </param>
public record ContainerExpandedMessage(string ContainerId);

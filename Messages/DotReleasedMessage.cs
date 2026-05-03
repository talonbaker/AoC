namespace SquareClickerPointer.Messages;

// ═══════════════════════════════════════════════════════════════════════════════
//  DotReleasedMessage — the "envelope" that travels over the message bus
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHAT IS A MESSAGE (in the pub/sub pattern)?
//  ─────────────────────────────────────────────
//  A message is a plain data object that carries the "what happened" information
//  from the publisher (the thing that detected the event) to any number of
//  subscribers (the things that want to react to it).
//
//  The critical property: the publisher does NOT know who the subscribers are,
//  and the subscribers do NOT know who published the message.  They only share
//  knowledge of this single type.  That is what makes the system loosely coupled.
//
//  WHY a C# record (not a class)?
//  ─────────────────────────────────
//  Records are:
//    • Immutable by default — once created, no one can change X or Y mid-flight.
//      A message sent over the bus should be a snapshot in time, not a mutable
//      object that a recipient might accidentally modify for other recipients.
//    • Value-semantic — two DotReleasedMessage instances with the same X and Y
//      are considered equal.  Useful in tests: Assert.Equal(expected, actual).
//    • Compact — the compiler generates a constructor, deconstruct, ToString(),
//      and Equals() automatically.  No boilerplate.
//
//  WHY a separate message type for "released" (vs. every mouse-move)?
//  ──────────────────────────────────────────────────────────────────
//  During a drag, the dot position changes dozens of times per second.  Publishing
//  a message on every pixel of movement would spam TriangleAlphaDataViewModel with
//  recalculations the user never sees.  "Released" means the user has committed a
//  final position — that is the meaningful event for downstream consumers.
//  If you ever need live-update subscribers, add a separate DotMovedMessage type
//  and send *that* from SetFromCanvasPoint().  Each subscriber can then choose
//  which granularity it cares about.

/// <summary>
/// Published by <see cref="ViewModels.PointControlViewModel.CommitPosition"/> when
/// the user releases the mouse after positioning the dot on the 2-D canvas.
/// </summary>
/// <param name="X">Committed X value in the 0–10 domain coordinate space.</param>
/// <param name="Y">Committed Y value in the 0–10 domain coordinate space.</param>
public record DotReleasedMessage(double X, double Y);

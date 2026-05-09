using CrashKonijn.Goap.Runtime;

namespace Game.GOAP.WorldKeys
{
    // Set by GOAP effects; intentionally not sensed from the world.
    // This makes GOAP pick the Chase action and keep it running until the goal is changed.
    [GoapId("game.goap.worldkey.is_chasing")]
    public class IsChasing : WorldKeyBase
    {
    }
}


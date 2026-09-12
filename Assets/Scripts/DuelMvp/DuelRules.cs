using System;

namespace ComeAndFight
{
    public enum DuelAction { None, Advance, Retreat, Thrust, Parry }

    [Serializable]
    public struct DuelState
    {
        public int aPosition, bPosition, aScore, bScore, turn, fireDepth;
        public bool suddenDeath;

        public static DuelState NewMatch() => new DuelState { aPosition = 2, bPosition = 6, turn = 1 };
    }

    public struct TurnResult
    {
        public DuelState state;
        public int aPoints, bPoints;
        public bool aFell, bFell, aBurned, bBurned, collision, parry, thrustHit, boutEnded, matchEnded;
        public string message;
    }
}

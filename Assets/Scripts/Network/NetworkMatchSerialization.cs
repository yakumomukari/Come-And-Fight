using Unity.Collections;
using Unity.Netcode;

namespace ComeAndFight.Networking
{
    public static class NetworkMatchSerialization
    {
        public const int MaxBeginTurnBytes = 128;
        public const int MaxResolveBytes = 256;

        public static void WriteBeginTurn(ref FastBufferWriter writer, int turnId, DuelState state, float seconds)
        {
            writer.WriteValueSafe(turnId); WriteState(ref writer, state); writer.WriteValueSafe(seconds);
        }

        public static void ReadBeginTurn(ref FastBufferReader reader, out int turnId, out DuelState state, out float seconds)
        {
            reader.ReadValueSafe(out turnId); ReadState(ref reader, out state); reader.ReadValueSafe(out seconds);
        }

        public static void WriteResolve(ref FastBufferWriter writer, int turnId, DuelAction actionA, DuelAction actionB, TurnResult result)
        {
            writer.WriteValueSafe(turnId); writer.WriteValueSafe((byte)actionA); writer.WriteValueSafe((byte)actionB);
            WriteState(ref writer, result.state); writer.WriteValueSafe(result.aPoints); writer.WriteValueSafe(result.bPoints);
            writer.WriteValueSafe(PackResultFlags(result));
            writer.WriteValueSafe(new FixedString128Bytes(result.message ?? string.Empty));
        }

        public static void ReadResolve(ref FastBufferReader reader, out int turnId, out DuelAction actionA, out DuelAction actionB, out TurnResult result)
        {
            reader.ReadValueSafe(out turnId); reader.ReadValueSafe(out byte a); reader.ReadValueSafe(out byte b);
            actionA = (DuelAction)a; actionB = (DuelAction)b; result = new TurnResult();
            ReadState(ref reader, out result.state); reader.ReadValueSafe(out result.aPoints); reader.ReadValueSafe(out result.bPoints);
            reader.ReadValueSafe(out ushort flags); UnpackResultFlags(ref result, flags);
            reader.ReadValueSafe(out FixedString128Bytes message); result.message = message.ToString();
        }

        static void WriteState(ref FastBufferWriter writer, DuelState state)
        {
            writer.WriteValueSafe(state.aPosition); writer.WriteValueSafe(state.bPosition);
            writer.WriteValueSafe(state.aScore); writer.WriteValueSafe(state.bScore);
            writer.WriteValueSafe(state.turn); writer.WriteValueSafe(state.fireDepth);
            byte flags = 0;
            if (state.suddenDeath) flags |= 1; if (state.aThrustRecovering) flags |= 2; if (state.bThrustRecovering) flags |= 4;
            writer.WriteValueSafe(flags);
        }

        static void ReadState(ref FastBufferReader reader, out DuelState state)
        {
            state = new DuelState();
            reader.ReadValueSafe(out state.aPosition); reader.ReadValueSafe(out state.bPosition);
            reader.ReadValueSafe(out state.aScore); reader.ReadValueSafe(out state.bScore);
            reader.ReadValueSafe(out state.turn); reader.ReadValueSafe(out state.fireDepth); reader.ReadValueSafe(out byte flags);
            state.suddenDeath = (flags & 1) != 0; state.aThrustRecovering = (flags & 2) != 0; state.bThrustRecovering = (flags & 4) != 0;
        }

        static ushort PackResultFlags(TurnResult result)
        {
            ushort flags = 0;
            if (result.aFell) flags |= 1 << 0; if (result.bFell) flags |= 1 << 1;
            if (result.aBurned) flags |= 1 << 2; if (result.bBurned) flags |= 1 << 3;
            if (result.collision) flags |= 1 << 4; if (result.parry) flags |= 1 << 5;
            if (result.thrustHit) flags |= 1 << 6; if (result.aWhiff) flags |= 1 << 7;
            if (result.bWhiff) flags |= 1 << 8; if (result.boutEnded) flags |= 1 << 9; if (result.matchEnded) flags |= 1 << 10;
            return flags;
        }

        static void UnpackResultFlags(ref TurnResult result, ushort flags)
        {
            result.aFell = (flags & 1 << 0) != 0; result.bFell = (flags & 1 << 1) != 0;
            result.aBurned = (flags & 1 << 2) != 0; result.bBurned = (flags & 1 << 3) != 0;
            result.collision = (flags & 1 << 4) != 0; result.parry = (flags & 1 << 5) != 0;
            result.thrustHit = (flags & 1 << 6) != 0; result.aWhiff = (flags & 1 << 7) != 0;
            result.bWhiff = (flags & 1 << 8) != 0; result.boutEnded = (flags & 1 << 9) != 0; result.matchEnded = (flags & 1 << 10) != 0;
        }

        public static string StateSummary(DuelState state) => $"A({state.aPosition},{state.aScore}) B({state.bPosition},{state.bScore}) turn={state.turn} fire={state.fireDepth}";
    }
}

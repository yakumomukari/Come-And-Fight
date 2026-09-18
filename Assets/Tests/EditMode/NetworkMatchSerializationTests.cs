#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace ComeAndFight.Tests
{
    public class NetworkMatchSerializationTests
    {
        [Test]
        public void ResolveRoundTrip_PreservesAuthoritativeStateAndFlags()
        {
            var expected = new TurnResult
            {
                state = new DuelState
                {
                    aPosition = 1, bPosition = 7, aScore = 2, bScore = 1, turn = 21,
                    fireDepth = 2, suddenDeath = true, aThrustRecovering = true, bThrustRecovering = false
                },
                aPoints = 2, bPoints = 1, aFell = true, bBurned = true, collision = true,
                parry = true, thrustHit = true, aWhiff = true, boutEnded = true, matchEnded = true,
                message = "权威结算消息"
            };
            var writer = new FastBufferWriter(Networking.NetworkMatchSerialization.MaxResolveBytes, Allocator.Temp);
            Networking.NetworkMatchSerialization.WriteResolve(ref writer, 42, DuelAction.Thrust, DuelAction.Parry, expected);
            var reader = new FastBufferReader(writer, Allocator.Temp);
            Networking.NetworkMatchSerialization.ReadResolve(ref reader, out int turnId, out DuelAction a, out DuelAction b, out TurnResult actual);

            Assert.AreEqual(42, turnId); Assert.AreEqual(DuelAction.Thrust, a); Assert.AreEqual(DuelAction.Parry, b);
            Assert.AreEqual(expected.state.aPosition, actual.state.aPosition); Assert.AreEqual(expected.state.bPosition, actual.state.bPosition);
            Assert.AreEqual(expected.state.aScore, actual.state.aScore); Assert.AreEqual(expected.state.bScore, actual.state.bScore);
            Assert.AreEqual(expected.state.turn, actual.state.turn); Assert.AreEqual(expected.state.fireDepth, actual.state.fireDepth);
            Assert.AreEqual(expected.state.suddenDeath, actual.state.suddenDeath);
            Assert.AreEqual(expected.state.aThrustRecovering, actual.state.aThrustRecovering);
            Assert.AreEqual(expected.state.bThrustRecovering, actual.state.bThrustRecovering);
            Assert.AreEqual(expected.aPoints, actual.aPoints); Assert.AreEqual(expected.bPoints, actual.bPoints);
            Assert.AreEqual(expected.aFell, actual.aFell); Assert.AreEqual(expected.bBurned, actual.bBurned);
            Assert.AreEqual(expected.collision, actual.collision); Assert.AreEqual(expected.parry, actual.parry);
            Assert.AreEqual(expected.thrustHit, actual.thrustHit); Assert.AreEqual(expected.aWhiff, actual.aWhiff);
            Assert.AreEqual(expected.boutEnded, actual.boutEnded); Assert.AreEqual(expected.matchEnded, actual.matchEnded);
            Assert.AreEqual(expected.message, actual.message);
            reader.Dispose(); writer.Dispose();
        }
    }
}
#endif

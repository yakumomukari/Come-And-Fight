#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace ComeAndFight.Tests
{
    public class TurnResolverTests
    {
        [Test]
        public void AdvanceIntoThrust_IsHit()
        {
            var s = DuelState.NewMatch(); s.aPosition = 3; s.bPosition = 5;
            var r = TurnResolver.Resolve(s, DuelAction.Advance, DuelAction.Thrust);
            Assert.AreEqual(1, r.bPoints);
            Assert.IsTrue(r.boutEnded);
        }

        [Test]
        public void RetreatAtEdge_FallsAndScoresOpponent()
        {
            var s = DuelState.NewMatch(); s.aPosition = 1;
            var r = TurnResolver.Resolve(s, DuelAction.Retreat, DuelAction.Parry);
            Assert.AreEqual(1, r.bPoints);
        }

        [Test]
        public void ParryPushesAttackerOffBoard()
        {
            var s = DuelState.NewMatch(); s.aPosition = 1; s.bPosition = 2;
            var r = TurnResolver.Resolve(s, DuelAction.Thrust, DuelAction.Parry);
            Assert.AreEqual(1, r.bPoints);
        }

        [Test]
        public void EqualScoresDoNotEndMatch()
        {
            var s = DuelState.NewMatch(); s.aScore = 2; s.bScore = 2;
            Assert.IsFalse(TurnResolver.HasWinner(s));
        }

        [Test]
        public void LeadAtTwoEndsMatch()
        {
            var s = DuelState.NewMatch(); s.aScore = 2; s.bScore = 1;
            Assert.IsTrue(TurnResolver.HasWinner(s));
        }

        [Test]
        public void TurnTwentyStartsSuddenDeath()
        {
            var s = DuelState.NewMatch(); s.turn = 20;
            var r = TurnResolver.Resolve(s, DuelAction.Parry, DuelAction.Parry);
            Assert.IsTrue(r.state.suddenDeath);
            Assert.AreEqual(1, r.state.fireDepth);
        }
    }
}
#endif

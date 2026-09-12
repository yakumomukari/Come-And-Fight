using System.Collections.Generic;

namespace ComeAndFight
{
    public static class TurnResolver
    {
        public const int MinCell = 1, MaxCell = 7, NormalTurnLimit = 20;

        public static TurnResult Resolve(DuelState old, DuelAction a, DuelAction b)
        {
            var r = new TurnResult { state = old };
            int ap = old.aPosition, bp = old.bPosition;
            int an = ap + Move(a, true), bn = bp + Move(b, false);

            // Two advances may not cross or share a cell.
            if (a == DuelAction.Advance && b == DuelAction.Advance && an >= bn)
            {
                an = ap; bn = bp; r.collision = true;
            }

            r.aFell = an < MinCell || an > MaxCell;
            r.bFell = bn < MinCell || bn > MaxCell;
            if (r.aFell || r.bFell) ScoreSurvivor(ref r, r.aFell, r.bFell, "坠落！");

            if (!r.boutEnded && !r.aFell && !r.bFell)
            {
                // A thrust targets the opponent's start or resolved cell one step ahead.
                bool aCanHit = bp == ap + 1 || bn == ap + 1;
                bool bCanHit = ap == bp - 1 || an == bp - 1;
                ResolveAttack(ref r, a, b, aCanHit, bCanHit, ref an, ref bn);
            }

            if (!r.boutEnded && !r.aFell && !r.bFell)
            {
                // Advancing into a stationary parry pushes it away.
                if (a == DuelAction.Advance && b == DuelAction.Parry && an >= bn)
                { an = bn; bn++; r.parry = true; }
                if (b == DuelAction.Advance && a == DuelAction.Parry && bn <= an)
                { bn = an; an--; r.parry = true; }
                bool af = an < MinCell, bf = bn > MaxCell;
                if (af || bf) { r.aFell = af; r.bFell = bf; ScoreSurvivor(ref r, af, bf, "推进坠落！"); }
            }

            if (!r.aFell) r.state.aPosition = Clamp(an);
            if (!r.bFell) r.state.bPosition = Clamp(bn);

            if (!r.boutEnded && old.suddenDeath)
            {
                r.aBurned = IsBurning(r.state.aPosition, old.fireDepth);
                r.bBurned = IsBurning(r.state.bPosition, old.fireDepth);
                if (r.aBurned || r.bBurned) ScoreSurvivor(ref r, r.aBurned, r.bBurned, "火焰吞噬！");
            }

            ApplyPoints(ref r);
            if (r.boutEnded) ResetBout(ref r);
            else AdvanceClock(ref r);
            r.matchEnded = HasWinner(r.state);
            if (r.collision) r.message = "双方相撞";
            else if (string.IsNullOrEmpty(r.message)) r.message = "行动落空";
            return r;
        }

        static void ResolveAttack(ref TurnResult r, DuelAction a, DuelAction b, bool ah, bool bh, ref int ap, ref int bp)
        {
            bool at = a == DuelAction.Thrust && ah, bt = b == DuelAction.Thrust && bh;
            if (at && b == DuelAction.Parry) { ap--; r.parry = true; at = false; if (ap < MinCell) { r.aFell = true; r.bPoints++; r.boutEnded = true; r.message = "B 招架反击！"; } }
            if (bt && a == DuelAction.Parry) { bp++; r.parry = true; bt = false; if (bp > MaxCell) { r.bFell = true; r.aPoints++; r.boutEnded = true; r.message = "A 招架反击！"; } }
            if (!r.boutEnded && (at || bt))
            {
                if (at) r.aPoints++;
                if (bt) r.bPoints++;
                r.thrustHit = true; r.boutEnded = true;
                r.message = at && bt ? "同时命中！" : at ? "A 击中！" : "B 击中！";
            }
        }

        static void ScoreSurvivor(ref TurnResult r, bool aDead, bool bDead, string message)
        {
            if (aDead && !bDead) r.bPoints++;
            if (bDead && !aDead) r.aPoints++;
            r.boutEnded = aDead != bDead;
            r.message = aDead && bDead ? "双方同时死亡，不计分" : message;
        }

        static void ApplyPoints(ref TurnResult r) { r.state.aScore += r.aPoints; r.state.bScore += r.bPoints; }
        static void ResetBout(ref TurnResult r) { r.state.aPosition = 2; r.state.bPosition = 6; r.state.turn = 1; r.state.suddenDeath = false; r.state.fireDepth = 0; }
        static void AdvanceClock(ref TurnResult r)
        {
            if (!r.state.suddenDeath && r.state.turn >= NormalTurnLimit) { r.state.suddenDeath = true; r.state.fireDepth = 1; r.message = "死斗开始！"; }
            else if (r.state.suddenDeath) r.state.fireDepth = System.Math.Min(3, r.state.fireDepth + 1);
            r.state.turn++;
        }
        static int Move(DuelAction action, bool isA) => action == DuelAction.Advance ? (isA ? 1 : -1) : action == DuelAction.Retreat ? (isA ? -1 : 1) : 0;
        static int Clamp(int p) => p < MinCell ? MinCell : p > MaxCell ? MaxCell : p;
        public static bool IsBurning(int p, int depth) => depth > 0 && (p <= depth || p >= 8 - depth);
        public static bool HasWinner(DuelState s) => (s.aScore >= 2 || s.bScore >= 2) && s.aScore != s.bScore;
    }
}

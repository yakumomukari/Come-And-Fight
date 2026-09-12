using UnityEngine;

namespace ComeAndFight
{
    public static class DuelBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartMvp()
        {
            if (Object.FindObjectOfType<DuelGame>() != null) return;
            new GameObject("Duel MVP").AddComponent<DuelGame>();
        }
    }
}

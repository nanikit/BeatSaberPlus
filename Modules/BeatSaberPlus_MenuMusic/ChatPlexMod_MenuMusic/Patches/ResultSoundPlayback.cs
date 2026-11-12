using System;
using HarmonyLib;
using UnityEngine;

namespace ChatPlexMod_MenuMusic.Patches
{
    [HarmonyPatch]
    internal static class ResultSoundPlayback
    {
        private static ResultsViewController m_Results;
        private static SongPreviewPlayer m_Player;
        private static AudioSource m_Source;
        private static AudioClip m_Clip;
        private static bool m_ActivatingResults;

        internal static bool IsPlaying(SongPreviewPlayer player)
        {
            return MMConfig.Instance.Enabled && player && player == m_Player
                && m_Source && m_Source.clip == m_Clip
                && (m_Source.isPlaying || player._isActiveChannelPaused);
        }

        internal static void Clear()
        {
            m_Results = null;
            m_Player = null;
            m_Source = null;
            m_Clip = null;
            m_ActivatingResults = false;
        }

        [HarmonyPatch(typeof(ResultsViewController), nameof(ResultsViewController.DidActivate))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void BeginResultActivation(ResultsViewController __instance, bool addedToHierarchy)
        {
            if (!addedToHierarchy || !MMConfig.Instance.Enabled)
                return;

            Clear();
            m_Results = __instance;
            m_Player = __instance._songPreviewPlayer;
            m_ActivatingResults = true;
        }

        [HarmonyPatch(typeof(ResultsViewController), nameof(ResultsViewController.DidActivate))]
        [HarmonyFinalizer]
        private static void EndResultActivation()
        {
            m_ActivatingResults = false;
        }

        [HarmonyPatch(typeof(ResultsViewController), nameof(ResultsViewController.DidDeactivate))]
        [HarmonyPrefix]
        private static void ReleaseResultSound(ResultsViewController __instance, bool removedFromHierarchy)
        {
            if (removedFromHierarchy && __instance == m_Results)
                Clear();
        }

        // The game normally crossfades to ambience before the clip reaches its end.
        [HarmonyPatch(typeof(SongPreviewPlayer), nameof(SongPreviewPlayer.CrossfadeTo),
            new[] { typeof(AudioClip), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(Action) })]
        [HarmonyPrefix]
        private static bool PreserveResultSound(SongPreviewPlayer __instance, bool isDefault)
        {
            return !isDefault || !IsPlaying(__instance);
        }

        [HarmonyPatch(typeof(SongPreviewPlayer), nameof(SongPreviewPlayer.CrossfadeTo),
            new[] { typeof(AudioClip), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(Action) })]
        [HarmonyPostfix]
        private static void CaptureResultSound(SongPreviewPlayer __instance, AudioClip audioClip, float duration, bool isDefault)
        {
            if (!m_ActivatingResults || __instance != m_Player || isDefault || duration <= 0f)
                return;

            int channel = __instance._activeChannel;
            if (channel < 0)
                return;
            var source = __instance._audioSourceControllers[channel].audioSource;
            if (!source.isPlaying || source.clip != audioClip)
                return;

            m_Source = source;
            m_Clip = audioClip;
        }
    }
}

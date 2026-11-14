using System;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Marmart Carts/Match Flow Profile", fileName = "MatchFlowProfile")]
public class MatchFlowProfile : ScriptableObject
{
    [Tooltip(
        "Exact authored match timeline. " +
        "A valid gameplay profile must contain exactly one EndGameWrap and it must be the final session."
    )]
    [SerializeField] private List<MatchFlowSession> sessions = new List<MatchFlowSession>();

    public IReadOnlyList<MatchFlowSession> Sessions => sessions;

    public float GetPlannedDuration()
    {
        float total = 0f;

        for (int i = 0; i < sessions.Count; i++)
        {
            MatchFlowSession session = sessions[i];
            if (session != null) total += session.GetPlannedDuration();
        }

        return total;
    }

    public int GetEndGameWrapCount()
    {
        int count = 0;

        for (int i = 0; i < sessions.Count; i++)
        {
            MatchFlowSession session = sessions[i];

            if (session != null && session.type == MatchFlowSessionType.EndGameWrap)
            {
                count++;
            }
        }

        return count;
    }

    public bool IsEndGameWrapLast()
    {
        if (sessions == null || sessions.Count == 0) return false;

        MatchFlowSession finalSession = sessions[sessions.Count - 1];
        return finalSession != null && finalSession.type == MatchFlowSessionType.EndGameWrap;
    }

    public bool HasValidTerminalEndGameWrap()
    {
        return GetEndGameWrapCount() == 1 && IsEndGameWrapLast();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameStateMachineCore;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Managers/GameState")]
public class GameStatesManager : RuntimeScriptableSingleton<GameStatesManager>
{
#if UNITY_EDITOR
    [MenuItem("GameStateMachine/PrintUpperState")]
    public static void PrintUpperState()
    {
        foreach (KeyValuePair<BaseGameState,Stack<BaseGameState>> pair in ActiveStateMachines)
            Debug.Log($"{pair.Key}:{pair.Value.Peek()}");
    }

    [MenuItem("GameStateMachine/PrintStateChain")]
    public static void PrintStateChain()
    {
        foreach (var pair in ActiveStateMachines)
        {
            StringBuilder stringBuilder = new StringBuilder();
            List<BaseGameState> states = new List<BaseGameState>(pair.Value);
            for (var index = states.Count - 1; index >= 0; index--)
            {
                BaseGameState baseGameState = states[index];
                stringBuilder.Append($"{baseGameState}->");
            }
            Debug.Log(stringBuilder);
        }
    }
#endif

    private static readonly Dictionary<BaseGameState, Stack<BaseGameState>> ActiveStateMachines =
        new Dictionary<BaseGameState, Stack<BaseGameState>>();

    public bool autoInitialize = true;
    public static bool AutoInitialize => Instance.autoInitialize;

    public static BaseGameState Current(BaseGameState root) => ActiveStateMachines[root].Peek();

    public static void Push<T>(BaseGameState root, GameState<T> gameState) where T : GameState<T>
    {
        if (!ActiveStateMachines.ContainsKey(root))
            ActiveStateMachines[root] = new Stack<BaseGameState>();
        ActiveStateMachines[root].Push(gameState);
    }
    
    public static BaseGameState Pop(BaseGameState root) => ActiveStateMachines[root].Pop();

}
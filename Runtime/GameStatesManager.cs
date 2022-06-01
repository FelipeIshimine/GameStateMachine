using System.Collections.Generic;
using System.Text;
using GameStateMachineCore;
using UnityEditor;
using UnityEngine;

public static class GameStatesManager
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

    public static Dictionary<BaseGameState, Stack<BaseGameState>> ActiveStateMachines => _activeStateMachines;
       
    private static Dictionary<BaseGameState, Stack<BaseGameState>> _activeStateMachines = new Dictionary<BaseGameState, Stack<BaseGameState>>();
    public static string GetStackName() => nameof(_activeStateMachines);
    public static BaseGameState Current(BaseGameState root) => ActiveStateMachines[root].Peek();

    public static void Push<T>(BaseGameState root, GameState<T> gameState) where T : GameState<T>
    {
        if (!ActiveStateMachines.ContainsKey(root))
            ActiveStateMachines[root] = new Stack<BaseGameState>();
        ActiveStateMachines[root].Push(gameState);
    }
    
    public static BaseGameState Pop(BaseGameState root) => ActiveStateMachines[root].Pop();

}
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace GameStateMachineCore
{
    public abstract class GameState<T> : BaseGameState where T : GameState<T>
    {
        public static T Instance { get; protected set; }

        public delegate void GameStateEvent(T state);
        public static event Action OnPreEnter;
        public static event Action OnPostEnter;
        public static event Action OnPreExit;
        public static event Action OnPostExit;
        
        private BaseGameState _currentState;
        public IState CurrentState => _currentState;

        private GameStateProxy _proxy;
        protected GameStateProxy Proxy
        {
            get
            {
                if (!_proxy)
                {
                    _proxy = new GameObject().AddComponent<GameStateProxy>();
                    _proxy.name = $"{typeof(T).Name} Proxy";
                    _proxy.Initialize(Instance);
                }
                return _proxy;
            }
        }
        
        public override void Exit()
        {

             if (_currentState == this)
                throw new Exception("Recursive State");

             Instance = null;
             
             OnPreExit?.Invoke();
             GameStatesManager.Pop(Root);
             _currentState?.Exit();
             OnPostExit?.Invoke();
             
             //Debug.Log($"|EXIT| <Color=brown>  {this} </color>");
        }

        public override void Enter()
        {
            if (_currentState == this)
                throw new Exception("Recursive State");

            OnPreEnter?.Invoke();
            Instance = this as T;
            
            GameStatesManager.Push(Root,this);
            
            OnPostEnter?.Invoke();
        }

        public override void SwitchState(BaseGameState nState)
        {
            //Debug.Log($"> <color=teal> {this.GetType().FullName}: </color>");
            Debug.Log($"> <color=teal> { this.GetType().FullName }: </color> <Color=brown> {_currentState?.GetType().Name} </color> => <Color=green> {nState.GetType().Name} </color>");
            if (_currentState != this)
                _currentState?.Exit();
            
            _currentState = nState;
            if (_currentState != null)
            {
                _currentState.Root = Root;
                _currentState.Enter();
            }
        }

        public override void ExitSubState()
        {
            Debug.Log($"ExitSubState: <color=red> {this} </color>");
            _currentState.Exit();
            _currentState = null;
        }
    }

    public abstract class BaseGameState : IState
    {
        private BaseGameState _root;
        public BaseGameState Root { get => _root ?? this; set => _root = value; }

        public static Action<float> OnInstantiationProgress;
        public abstract void Enter();
        public abstract void Exit();
        public abstract void SwitchState(BaseGameState nState);

        public abstract void ExitSubState();
    }

}
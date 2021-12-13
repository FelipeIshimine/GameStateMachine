using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameStateMachineCore
{
    public abstract class GameState<T> : BaseGameState where T : GameState<T>
    {
        public delegate void GameStateEvent(T state);
        
        public static event Action OnPreEnterAny;
        public static event Action OnPostEnterAny;
        public static event Action OnPreExitAny;
        public static event Action OnPostExitAny;
        
        public event Action OnPreEnter;
        public event Action OnPostEnter;
        public event Action OnPreExit;
        public event Action OnPostExit;
        
        private BaseGameState _currentState;
        public BaseGameState CurrentState => _currentState;

        private GameStateProxy _proxy;
        protected GameStateProxy Proxy
        {
            get
            {
                if (_proxy) return _proxy;
                _proxy = new GameObject().AddComponent<GameStateProxy>();
                _proxy.name = $"{typeof(T).Name} Proxy";
                _proxy.Initialize(this as T);
                return _proxy;
            }
        }

        public override void BaseEnter()
        {
            OnPreEnter?.Invoke();
            OnPreEnterAny?.Invoke();
            
            if (_currentState == this)
                throw new Exception("Recursive State");

            GameStatesManager.Push(Root,this);
            
            Enter();
            OnPostEnter?.Invoke();
            OnPostEnterAny?.Invoke();
        }

        public override void BaseExit()
        {
            OnPreExit?.Invoke();
            OnPreExitAny?.Invoke();
            
            if (_proxy != null)
                Object.Destroy(_proxy.gameObject);

            if (_currentState == this)
                throw new Exception("Recursive State");

            GameStatesManager.Pop(Root);
            _currentState?.BaseExit();
            
            Exit();
            OnPostExit?.Invoke();
            OnPostExitAny?.Invoke();
        }


        protected abstract void Enter();
        protected abstract void Exit();

        protected override void SwitchState(BaseGameState nState)
        {
            //Debug.Log($"> <color=teal> {this.GetType().FullName}: </color>");
            Debug.Log($"> <color=teal> { this.GetType().FullName }: </color> <Color=brown> {_currentState?.GetType().Name} </color> => <Color=green> {nState.GetType().Name} </color>");
            if (_currentState != this)
                _currentState?.BaseExit();
            
            _currentState = nState;
            if (_currentState == null) return;
            
            _currentState.Root = Root;
            _currentState.BaseEnter();
            
            base.SwitchState(nState);
        }

        public override void ExitSubState()
        {
            Debug.Log($"{this} ExitSubState: <color=red> {_currentState} </color>");
            _currentState.BaseExit();
            _currentState = null;
        }
    }

    public abstract class BaseGameState
    {
        private BaseGameState _root;
        public BaseGameState Root { get => _root ?? this; set => _root = value; }

        public static Action OnAnySwitch;
        public abstract void BaseEnter();
        public abstract void BaseExit();

        protected virtual void SwitchState(BaseGameState nState) => OnAnySwitch?.Invoke();

        public abstract void ExitSubState();

        /// <summary>
        /// Not compatible with WEBGL
        /// </summary>
        /// <param name="duration"></param>
        protected static async Task WaitAsync(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime)
                await Task.Yield();
        }
        /// <summary>
        /// Not compatible with WEBGL
        /// </summary>
        /// <param name="duration"></param>
        protected static async Task WaitRealtimeAsync(float duration) => await Task.Delay((int)(duration * 1000));

    }

}
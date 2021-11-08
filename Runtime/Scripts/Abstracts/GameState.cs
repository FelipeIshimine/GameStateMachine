﻿using System;
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
        public static T Instance { get; protected set; }

        public delegate void GameStateEvent(T state);
        public static event Action OnPreEnter;
        public static event Action OnPostEnter;
        public static event Action OnPreExit;
        public static event Action OnPostExit;
        
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
                _proxy.Initialize(Instance);
                return _proxy;
            }
        }

        public override void BaseEnter()
        {
            OnPreEnter?.Invoke();
            
            if (_currentState == this)
                throw new Exception("Recursive State");

            Instance = this as T;
            GameStatesManager.Push(Root,this);
            
            Enter();
            OnPostEnter?.Invoke();
        }

        public override void BaseExit()
        {
            OnPreExit?.Invoke();
            
            if (_proxy != null)
                Object.Destroy(_proxy.gameObject);

            if (_currentState == this)
                throw new Exception("Recursive State");

            Instance = null;
            GameStatesManager.Pop(Root);
            _currentState?.BaseExit();
            
            Exit();
            OnPostExit?.Invoke();
        }


        protected abstract void Enter();
        protected abstract void Exit();

        public override void SwitchState(BaseGameState nState)
        {
            //Debug.Log($"> <color=teal> {this.GetType().FullName}: </color>");
            Debug.Log($"> <color=teal> { this.GetType().FullName }: </color> <Color=brown> {_currentState?.GetType().Name} </color> => <Color=green> {nState.GetType().Name} </color>");
            if (_currentState != this)
                _currentState?.BaseExit();
            
            _currentState = nState;
            if (_currentState == null) return;
            
            _currentState.Root = Root;
            _currentState.BaseEnter();
        }

        public override void ExitSubState()
        {
            Debug.Log($"ExitSubState: <color=red> {this} </color>");
            _currentState.BaseExit();
            _currentState = null;
        }
    }

    public abstract class BaseGameState
    {
        private BaseGameState _root;
        public BaseGameState Root { get => _root ?? this; set => _root = value; }
        
        public abstract void BaseEnter();
        public abstract void BaseExit();
        
        public abstract void SwitchState(BaseGameState nState);

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
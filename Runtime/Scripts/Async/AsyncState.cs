using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public abstract class AsyncState
{
    public static event Action<AsyncState> OnAnySwitchState;
    public event Action<AsyncState,AsyncState> OnSwitchState;
    public static event Action<float> OnLoadProgress;

    public AsyncState Root { get; private set; } = null;

    private AsyncState _parent;
    
    [ShowInInspector] private AsyncState _subState;
    protected AsyncState SubState => _subState;

    protected InnerState State;

    public bool IsBusy => _subState is { IsBusy: true } || 
                          State == InnerState.Entering ||
                          State == InnerState.Exiting;

    public bool IsReady => _subState is { IsBusy: false } &&
                           State == InnerState.Active;

    private readonly AssetReference _singleSceneReference;

    private readonly AssetReference[] _sceneReferences = null;
    private readonly SceneInstance[] _sceneInstances = null;

    protected AsyncState() 
    {
        State = InnerState.Inactive;
    }

    protected AsyncState(AssetReference[] sceneReferences) : this()
    {
        _sceneReferences = sceneReferences;
        _sceneInstances = new SceneInstance[sceneReferences?.Length ?? 0];
    }

    protected AsyncState(AssetReference sceneReference, LoadSceneMode loadSceneMode) : this()
    {
        switch (loadSceneMode)
        {
            case LoadSceneMode.Single:
                _singleSceneReference = sceneReference;
                _sceneInstances = Array.Empty<SceneInstance>();
                break;
            case LoadSceneMode.Additive:
                _sceneReferences = new []{sceneReference};
                _sceneInstances = new SceneInstance[1];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(loadSceneMode), loadSceneMode, null);
        }
    }
    
    protected AsyncState(AssetReference sceneReference, params AssetReference[] sceneReferences) : this()
    {
        _singleSceneReference = sceneReference;
        _sceneReferences = sceneReferences;
        _sceneInstances = new SceneInstance[sceneReferences.Length];
    }

    protected enum InnerState
    {
        Inactive,
        Entering,
        Active,
        Exiting,
        Finished
    }

    protected void SwitchState(AsyncState nState)
    {
        async void Action() => await SwitchStateAsync(nState);
        new Task(Action).RunSynchronously();
    }

    protected async Task SwitchStateAsync(AsyncState nState)
    {
        AsyncState oldState = _subState;
        if (_subState != null)
            Debug.Log($"<color=green>{this}</color>: <color=red>{_subState}</color> => <color=white>{nState}</color>");

        if (_subState != null)
        {
            _subState.State = InnerState.Exiting;
            await _subState.SubStateExit();
            await _subState.Exit();
            await _subState.UnloadAdditiveScenesAsync();
            _subState.State = InnerState.Finished;
        }
        _subState = nState;
        if (_subState != null)
            await EnterStateAsync(_subState);

        OnSwitchState?.Invoke(oldState,_subState);
        OnAnySwitchState?.Invoke(Root);
    }

    private async Task EnterStateAsync(AsyncState state)
    {
        await Task.Yield();
        state.State = InnerState.Entering;
        state._parent = this;
        state.Root = Root;
        await state.LoadScenesAsync();
        if(!(state is NullState))
            state.GoToNull();
        
        await state.Enter();
        state.State = InnerState.Active;
    }

    public async Task InitializeAsRootAsync()
    {
        Root = this;
        await EnterStateAsync(this);
    }
    
    public void InitializeAsRoot() => new Task(RootInit).RunSynchronously();

    private async void RootInit() => await InitializeAsRootAsync();


    private async Task SubStateExit()
    {
        if (_subState == null) return;
        await _subState.SubStateExit();
        await _subState.Exit();
    }


    /// <summary>
    /// DO NOT call outside of AsyncStateMachine class
    /// </summary>
    protected virtual Task Enter() => Task.CompletedTask;

    /// <summary>
    /// DO NOT call outside of AsyncStateMachine class
    /// </summary>
    protected virtual Task Exit() => Task.CompletedTask;

    private async Task LoadScenesAsync()
    {
        int totalProgress = 0;

        if (_singleSceneReference != null) totalProgress++;
        if (_sceneReferences != null) totalProgress += _sceneReferences.Length;
        
        OnLoadProgress?.Invoke(0);
        if (_singleSceneReference != null)
        {
            Time.timeScale = 0;
            Debug.Log($"{this} Loading Single Scene Async");
            var asyncOp = Addressables.LoadSceneAsync(_singleSceneReference);
            var task = asyncOp.Task;
            while (!task.IsCompleted)
            {
                await Task.Yield();
                if(!task.IsCompleted)
                    OnLoadProgress?.Invoke(asyncOp.PercentComplete/totalProgress);
            }
            OnLoadProgress?.Invoke(1f/totalProgress);
            totalProgress--;
            Time.timeScale = 1;
        }
        
        if (_sceneReferences != null)
        {
            Time.timeScale = 0;
            //OnLoadProgress?.Invoke(0);
            for (var index = 0; index < _sceneReferences.Length; index++)
            {
                Debug.Log($"{this} Loading SceneAsync {index+1}/{_sceneReferences.Length} ");
                _sceneInstances[index] = await Addressables.LoadSceneAsync(_sceneReferences[index], LoadSceneMode.Additive).Task;
                OnLoadProgress?.Invoke(((float)index / totalProgress));
            }
            Time.timeScale = 1;
        }
        OnLoadProgress?.Invoke(1);
        await Task.Yield();
    }

    private async Task UnloadAdditiveScenesAsync()
    {
        if (_sceneReferences != null)
        {
            for (var index = 0; index < _sceneInstances.Length; index++)
            {
                Debug.Log($"{this} Unloading SceneAsync {index + 1}/{_sceneReferences.Length} ");
                
                await Addressables.UnloadSceneAsync(_sceneInstances[index], false).Task;
            }
        }
    }

    public Stack<AsyncState> GetStates()
    {
        Stack<AsyncState> asyncStates = new Stack<AsyncState>();
        GetStates(ref asyncStates);
        return asyncStates;
    }

    private void GetStates(ref Stack<AsyncState> stack)
    {
        stack.Push(this);
        _subState?.GetStates(ref stack);
    }

    [Button] protected void GoToNull() => SwitchState(new NullState());
}
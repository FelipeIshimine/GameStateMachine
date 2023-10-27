using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public abstract class AsyncState
{
    public static event Action<AsyncState> OnAnySwitchState;
    public event Action<AsyncState,AsyncState> OnSwitchState;

    public event Action<float> OnLoadProgress;

    public float SceneLoadProgress { get; set; }

    public AsyncState Root { get; private set; } = null;

    private AsyncState parent;
    
    [ShowInInspector] private AsyncState subState;
    protected AsyncState SubState => subState;

    protected InnerState State;

    public bool IsBusy => subState is { IsBusy: true } || 
                          State == InnerState.Entering ||
                          State == InnerState.Exiting;

    public bool IsReady => subState is { IsBusy: false } &&
                           State == InnerState.Active;

    private readonly AssetReference singleSceneReference;

    private readonly AssetReference[] sceneReferences = null;
    private readonly SceneInstance[] sceneInstances = null;
    public SceneInstance[] SceneInstances => sceneInstances;

    protected AsyncState() 
    {
        State = InnerState.Inactive;
    }

    protected AsyncState(AssetReference[] sceneReferences) : this()
    {
        this.sceneReferences = sceneReferences;
        sceneInstances = new SceneInstance[sceneReferences?.Length ?? 0];
    }

    protected AsyncState(AssetReference sceneReference, LoadSceneMode loadSceneMode) : this()
    {
        switch (loadSceneMode)
        {
            case LoadSceneMode.Single:
                singleSceneReference = sceneReference;
                sceneInstances = Array.Empty<SceneInstance>();
                break;
            case LoadSceneMode.Additive:
                sceneReferences = new []{sceneReference};
                sceneInstances = new SceneInstance[1];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(loadSceneMode), loadSceneMode, null);
        }
    }
    
    protected AsyncState(AssetReference sceneReference, params AssetReference[] sceneReferences) : this()
    {
        singleSceneReference = sceneReference;
        this.sceneReferences = sceneReferences;
        sceneInstances = new SceneInstance[sceneReferences.Length];
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

    protected async UniTask SwitchStateAsync(AsyncState nState)
    {
        AsyncState oldState = subState;
        if (subState != null)
            Debug.Log($"<color=green>{this}</color>: <color=red>{subState}</color> => <color=white>{nState}</color>");

        if (subState != null)
        {
            subState.State = InnerState.Exiting;
            await subState.SwitchStateAsync(null);
            await subState.Exit();
            await subState.UnloadAdditiveScenesAsync();
            subState.State = InnerState.Finished;
        }
        subState = nState;
        if (subState != null)
            await EnterStateAsync(subState);

        OnSwitchState?.Invoke(oldState,subState);
        OnAnySwitchState?.Invoke(Root);
    }

    private async UniTask EnterStateAsync(AsyncState state)
    {
        await Task.Yield();
        state.State = InnerState.Entering;
        state.parent = this;
        state.Root = Root;
        await state.LoadScenesAsync();
        if(!(state is NullState))
            state.GoToNull();
        
        await state.Enter();
        state.State = InnerState.Active;
    }

    public UniTask InitializeAsRootAsync()
    {
        Root = this;
        return EnterStateAsync(this);
    }
    
    //public void InitializeAsRoot() => InitializeAsRootAsync().AsTask().RunSynchronously();

    /// <summary>
    /// DO NOT call outside of AsyncStateMachine class
    /// </summary>
    protected virtual UniTask Enter() => UniTask.CompletedTask;

    /// <summary>
    /// DO NOT call outside of AsyncStateMachine class
    /// </summary>
    protected virtual UniTask Exit() => UniTask.CompletedTask;

    private async UniTask LoadScenesAsync()
    {
        float oldTimeScale = Time.timeScale;
        Time.timeScale = 0;
        int totalProgress = 0;

        if (singleSceneReference != null) totalProgress++;
        if (sceneReferences != null) totalProgress += sceneReferences.Length;

        UpdateSceneLoadProgress(0);
        if (singleSceneReference != null)
        {
            Debug.Log($"{this} Loading Single Scene Async");
            var asyncOp = Addressables.LoadSceneAsync(singleSceneReference);
            var task = asyncOp.Task;
            while (!task.IsCompleted)
            {
                await UniTask.NextFrame();
                if(!task.IsCompleted)
                    UpdateSceneLoadProgress(asyncOp.PercentComplete/totalProgress);
            }
            UpdateSceneLoadProgress(1f/totalProgress);
            totalProgress--;
        }
        
        if (sceneReferences != null)
        {
            //UpdateSceneLoadProgress(0);
            for (var index = 0; index < sceneReferences.Length; index++)
            {
                UpdateSceneLoadProgress(((index+.5f) / totalProgress));
                Debug.Log($"{this} Loading SceneAsync {index+.5f}/{sceneReferences.Length} ");
                sceneInstances[index] = await Addressables.LoadSceneAsync(sceneReferences[index], LoadSceneMode.Additive).Task;
                Debug.Log($"{this} Loading SceneAsync {index+1}/{sceneReferences.Length} ");
                UpdateSceneLoadProgress(((float)index / totalProgress));
            }
        }
        Time.timeScale = oldTimeScale;
        UpdateSceneLoadProgress(1);
        await UniTask.NextFrame();
    }

    private void UpdateSceneLoadProgress(float value)
    {
        SceneLoadProgress = value;
        OnLoadProgress?.Invoke(value);
    }

    private async Task UnloadAdditiveScenesAsync()
    {
        if (sceneReferences != null)
        {
            for (var index = 0; index < sceneInstances.Length; index++)
            {
                Debug.Log($"{this} Unloading SceneAsync {index + 1}/{sceneReferences.Length} ");
                await Addressables.UnloadSceneAsync(sceneInstances[index]).Task;
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
        subState?.GetStates(ref stack);
    }

    [Button] protected void GoToNull() => SwitchState(new NullState());
}
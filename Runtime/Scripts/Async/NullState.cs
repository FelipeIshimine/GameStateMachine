using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

public class NullState : AsyncState
{
    protected override UniTask Enter() => UniTask.CompletedTask;
    protected override UniTask Exit() => UniTask.CompletedTask;
}
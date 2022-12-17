using System.Threading.Tasks;

public class NullState : AsyncState
{
    protected override Task Enter() => Task.CompletedTask;
    protected override Task Exit() => Task.CompletedTask;
}
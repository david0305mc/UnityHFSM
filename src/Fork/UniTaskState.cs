using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class UniTaskState : UnityHFSM.State
{
    private readonly Func<CancellationToken, UniTask> onEnterAsync;
    private readonly Action<Exception> onException;
    private readonly CancellationToken externalCancellationToken;
    private CancellationTokenSource cts;

    public bool IsRunning => cts != null;

    public UniTaskState(
        Func<CancellationToken, UniTask> onEnterAsync = null,
        Action<UnityHFSM.State> onLogic = null,
        Action<UnityHFSM.State> onExit = null,
        Func<UnityHFSM.State, bool> canExit = null,
        bool needsExitTime = false,
        bool isGhostState = false,
        Action<Exception> onException = null,
        CancellationToken externalCancellationToken = default
    ) : base(
        onLogic: onLogic == null ? null : state => onLogic((UnityHFSM.State)state),
        onExit: onExit == null ? null : state => onExit((UnityHFSM.State)state),
        canExit: canExit == null ? null : state => canExit((UnityHFSM.State)state),
        needsExitTime: needsExitTime,
        isGhostState: isGhostState
    )
    {
        this.onEnterAsync = onEnterAsync;
        this.onException = onException;
        this.externalCancellationToken = externalCancellationToken;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        StartOnEnterAsync();
    }

    public override void OnExit()
    {
        CancelOnEnterAsync();
        base.OnExit();
    }

    public void Cancel()
    {
        CancelOnEnterAsync();
    }

    private void StartOnEnterAsync()
    {
        CancelOnEnterAsync();

        if (onEnterAsync == null)
        {
            return;
        }

        cts = externalCancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken)
            : new CancellationTokenSource();
        RunOnEnterAsync(cts, cts.Token).Forget();
    }

    private void CancelOnEnterAsync()
    {
        var source = cts;
        if (source == null)
        {
            return;
        }

        cts = null;

        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private async UniTask RunOnEnterAsync(CancellationTokenSource source, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            await onEnterAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
        finally
        {
            if (ReferenceEquals(cts, source))
            {
                cts = null;
            }

            source.Dispose();
        }
    }

    private void HandleException(Exception ex)
    {
        if (onException == null)
        {
            Debug.LogException(ex);
            return;
        }

        try
        {
            onException.Invoke(ex);
        }
        catch (Exception handlerException)
        {
            Debug.LogException(handlerException);
        }
    }
}
